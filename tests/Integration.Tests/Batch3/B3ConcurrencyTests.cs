using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>
/// IT_B3_017, 045, 046, 066, 078, 088, 114, 115, 121, 139, 161, 162, 163, 171 — request song song / fault-injection trên connection độc lập
/// (dữ liệu S0 được commit, tự dọn sau test). Mỗi ca đua tự chạy 6 vòng bên trong hàm test.
/// </summary>
public sealed class B3ConcurrencyTests(ITestOutputHelper output) : B3ConcurrentTestBase(output)
{
    private const int Rounds = 6;

    private async Task Loop(Func<int, Task> round, int rounds = Rounds)
    {
        for (var i = 0; i < rounds; i++) await round(i);
    }

    private static object Rate(int opId, decimal rate, DateOnly date) => new { OperationId = opId, Rate = rate, EffectiveDate = date };

    private async Task<int> PendingRateAsync(int round, bool submit = true)
    {
        var date = new DateOnly(2028, 1, 1).AddDays(round);
        (await PostAsync(AC + "piece-rates", await TkAcc(), Rate(P.Op2.OperationId, 2000m + round, date))).Code.Should().BeOneOf(200, 201);
        await using var db = NewDb();
        var id = (await db.PieceRates.AsNoTracking().SingleAsync(r => r.OperationId == P.Op2.OperationId && r.EffectiveDate == date)).PieceRateId;
        if (submit) (await PostAsync(AC + $"piece-rates/{id}/submit", await TkAcc())).Code.Should().Be(200);
        return id;
    }

    private async Task<NX_MTD.Domain.Entities.PieceRate> RateAsync(int id)
    {
        await using var db = NewDb();
        return await db.PieceRates.AsNoTracking().SingleAsync(r => r.PieceRateId == id);
    }

    // ---------------------------------------------------------------- Product / spec

    [Fact]
    public async Task IT_B3_017_Duplicate_And_Concurrent_Product_Codes()
    {
        var token = await TkAdm();
        var dup = await PostAsync(A + "products", token, new { ProductCode = "SP001", Description = (string?)null });
        dup.Code.Should().Be(409, dup.ToString());

        await Loop(async i =>
        {
            var code = $"SP3{i}1";
            var results = await RaceAsync(
                () => PostAsync(A + "products", token, new { ProductCode = code, Description = (string?)null }),
                () => PostAsync(A + "products", token, new { ProductCode = code, Description = (string?)null }));
            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
            await using var db = NewDb();
            (await db.Products.AsNoTracking().CountAsync(p => p.ProductCode == code)).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_045_Fault_On_Fabric_Insert_Rolls_Back_Whole_Spec()
    {
        var token = await TkAdm();
        var before = await CountsAsync();
        var body = new
        {
            Components = (object[])[Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId), Comp("Túi", "PANEL", 2, B.Pr1Pocket.ProductComponentId),
                Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId), Comp("Cổ", "PANEL", 1)],
            Variants = new[]
            {
                new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" },
                new { ProductVariantId = (int?)B.Pr1VarL.ProductVariantId, Color = "Đen", SizeCode = "L" },
                new { ProductVariantId = (int?)P.Pr1WhiteM.ProductVariantId, Color = "Trắng", SizeCode = "M" },
            },
            Norms = new[]
            {
                new { ComponentName = "Thân trước", Color = "Đen", MaterialId = B.Ma1.MaterialId, SpecNote = (string?)null },
                new { ComponentName = "Thân trước", Color = "Trắng", MaterialId = B.Ma2.MaterialId, SpecNote = (string?)null },
            },
            MarkerComponents = new[] { new { MarkerNo = 1, ComponentName = "Thân trước" } },
            MarkerColors = new[] { new { MarkerNo = 1, Color = "Đen" }, new { MarkerNo = 1, Color = "Trắng" } },
        };

        Api r = null!;
        await WithFaultTriggerAsync("ProductMarkerFabric", async () => r = await PutAsync(A + $"products/{B.Pr1.ProductId}/spec", token, body));

        Out.WriteLine($"[?] fault-injection → {r.Code}");
        r.Code.Should().BeGreaterThanOrEqualTo(400, r.ToString());
        r.Body.Should().NotContainAny("SqlException", "StackTrace", "it-b3 fault");
        (await CountsAsync()).Should().Be(before, "rollback sạch: không có Cổ, định mức cũ còn nguyên");
        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().AnyAsync(c => c.ProductId == B.Pr1.ProductId && c.ComponentName == "Cổ")).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B3_046_Two_Concurrent_Spec_Saves()
    {
        var token = await TkAdm();
        await Loop(async i =>
        {
            var (a, b) = ($"Cổ{i}", $"Vai{i}");
            var url = A + $"products/{B.Pr1.ProductId}/spec";
            var results = await RaceAsync(() => PutAsync(url, token, SpecBody(a)), () => PutAsync(url, token, SpecBody(b)));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1);
            await using var db = NewDb();
            var names = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).Select(c => c.ComponentName).ToListAsync();
            names.Count(n => n == a || n == b).Should().Be(1, "trạng thái cuối khớp đúng một request");
            var ids = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).Select(c => c.ProductComponentId).ToListAsync();
            (await db.ProductComponentMaterials.AsNoTracking().Where(n => n.ProductId == B.Pr1.ProductId).Select(n => n.ProductComponentId).ToListAsync())
                .Should().OnlyContain(c => ids.Contains(c));
            (await db.ProductMarkerComponents.AsNoTracking().Where(n => n.ProductId == B.Pr1.ProductId).Select(n => n.ProductComponentId).ToListAsync())
                .Should().OnlyContain(c => ids.Contains(c));
        });
    }

    [Fact]
    public async Task IT_B3_066_Two_Concurrent_Marker_Imports_Store_One_Marker()
    {
        var token = await TkAdm();
        await Loop(async i =>
        {
            var code = $"S3{i}";
            var url = A + $"products/{B.Pr1.ProductId}/import-data?orderId={B.Od1.OrderId}&parts=markers";
            var results = await RaceAsync(() => PostAsync(url, token, OMarkerWorkbook(code, 1)), () => PostAsync(url, token, OMarkerWorkbook(code, 1)));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            await using var db = NewDb();
            var markers = await db.ProductMarkers.AsNoTracking().Where(m => m.OrderId == B.Od1.OrderId && m.MarkerCode == code).ToListAsync();
            markers.Should().ContainSingle();
            (await db.ProductMarkerRatios.AsNoTracking().CountAsync(r => r.ProductMarkerId == markers[0].ProductMarkerId)).Should().Be(2, "ratio không bị nhân đôi");
        });
    }

    // ---------------------------------------------------------------- Operations

    [Fact]
    public async Task IT_B3_078_Two_Concurrent_Final_Steps_Only_One_Wins()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            await using (var db = NewDb())
                await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Operation SET Status = 'DISCONTINUED' WHERE ProductId = {0} AND IsFinalStep = 1 AND Status = 'ACTIVE'", B.Pr2.ProductId);
            var results = await RaceAsync(
                () => PostAsync(AC + "operations", token, OpBody(B.Pr2.ProductId, $"CF1{i}", true)),
                () => PostAsync(AC + "operations", token, OpBody(B.Pr2.ProductId, $"CF2{i}", true)));

            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code is 400 or 409).Should().Be(1, Summary(results));
            await using var check = NewDb();
            (await check.Operations.AsNoTracking().CountAsync(o => o.ProductId == B.Pr2.ProductId && o.IsFinalStep && o.Status == "ACTIVE")).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_088_Two_Concurrent_Creates_With_Same_Code()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            var code = $"CD2{i}0";
            var body = new
            {
                ProductId = B.Pr1.ProductId, OperationCode = code, OperationName = "Công đoạn IT", StationSequence = SeqOf(code), SkillLevel = (int?)null,
                StandardTimeSec = (decimal?)null, TeamType = "Sewing", IsFinalStep = false, GroupName = $"G88-{i}",
                ComponentIds = new[] { B.Pr1Pocket.ProductComponentId },
            };
            var results = await RaceAsync(() => PostAsync(AC + "operations", token, body), () => PostAsync(AC + "operations", token, body));

            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
            await using var db = NewDb();
            var ops = await db.Operations.AsNoTracking().Where(o => o.ProductId == B.Pr1.ProductId && o.OperationCode == code).ToListAsync();
            ops.Should().ContainSingle();
            (await db.OperationComponents.AsNoTracking().CountAsync(c => c.OperationId == ops[0].OperationId)).Should().Be(1);
            (await db.OperationGroups.AsNoTracking().CountAsync(g => g.ProductId == B.Pr1.ProductId && g.GroupName == $"G88-{i}")).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_114_Two_Concurrent_Operation_Imports_Never_Duplicate()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            var rows = Enumerable.Range(0, 3).Select(k => (object)new
            {
                RowNumber = k + 1, Stt = 100 + (i * 3) + k, Name = $"May {i}-{k}", SkillLevel = (int?)null, StandardTimeSec = (decimal?)null,
                Equipment = (string?)null, Rate = (decimal?)1000m + k, ParseError = (string?)null, GroupName = (string?)"Nhóm May",
                ComponentNames = new[] { "Túi" }, IsFinal = false,
            }).ToArray();
            var sheet = new { SheetName = "May", ProductText = (string?)null, HasRateColumn = true, Rows = rows, HasGroupColumn = true, HasComponentColumn = true };
            var url = AC + $"operations/import-data?productId={B.Pr1.ProductId}&effectiveDate=2027-01-01&submitRates=false";
            var results = await RaceAsync(() => PostAsync(url, token, sheet), () => PostAsync(url, token, sheet));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            await using var db = NewDb();
            var ops = await db.Operations.AsNoTracking().Where(o => o.ProductId == B.Pr1.ProductId && o.StationSequence >= 100 + (i * 3) && o.StationSequence < 103 + (i * 3)).ToListAsync();
            ops.Select(o => o.OperationCode).Should().OnlyHaveUniqueItems();
            ops.Should().HaveCount(3);
            (await db.OperationGroups.AsNoTracking().CountAsync(g => g.ProductId == B.Pr1.ProductId && g.GroupName == "Nhóm May")).Should().Be(1);
            foreach (var op in ops) (await db.PieceRates.AsNoTracking().CountAsync(r => r.OperationId == op.OperationId)).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_115_Fault_On_PieceRate_Insert_Rolls_Back_Import()
    {
        var token = await TkAcc();
        var before = await CountsAsync();
        var rows = new[] { 10, 11, 12 }.Select(stt => (object)new
        {
            RowNumber = stt, Stt = stt, Name = $"May túi {stt}", SkillLevel = (int?)null, StandardTimeSec = (decimal?)null, Equipment = (string?)null,
            Rate = (decimal?)1000m, ParseError = (string?)null, GroupName = (string?)"Nhóm May", ComponentNames = new[] { "Túi" }, IsFinal = false,
        }).ToArray();
        var sheet = new { SheetName = "May", ProductText = (string?)null, HasRateColumn = true, Rows = rows, HasGroupColumn = true, HasComponentColumn = true };

        Api r = null!;
        await WithFaultTriggerAsync("PieceRate", async () =>
            r = await PostAsync(AC + $"operations/import-data?productId={B.Pr1.ProductId}&effectiveDate=2027-01-01&submitRates=false", token, sheet));

        Out.WriteLine($"[?] fault-injection → {r.Code}");
        r.Code.Should().BeGreaterThanOrEqualTo(400, r.ToString());
        r.Body.Should().NotContainAny("SqlException", "StackTrace", "it-b3 fault");
        (await CountsAsync()).Should().Be(before, "không có Operation, OperationGroup hay OperationComponent mới");
    }

    // ---------------------------------------------------------------- OperationGroups / PieceRates

    [Fact]
    public async Task IT_B3_121_Two_Concurrent_Group_Creates_Only_One_Wins()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            var body = new { ProductId = B.Pr1.ProductId, GroupName = $"Nhóm X{i}" };
            var results = await RaceAsync(() => PostAsync(AC + "operation-groups", token, body), () => PostAsync(AC + "operation-groups", token, body));

            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
            await using var db = NewDb();
            (await db.OperationGroups.AsNoTracking().CountAsync(g => g.ProductId == B.Pr1.ProductId && g.GroupName == $"Nhóm X{i}")).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_139_Two_Concurrent_Rates_On_Same_Date_Only_One_Wins()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            var date = new DateOnly(2027, 4, 1).AddDays(i);
            var results = await RaceAsync(
                () => PostAsync(AC + "piece-rates", token, Rate(P.Op2.OperationId, 2100m, date)),
                () => PostAsync(AC + "piece-rates", token, Rate(P.Op2.OperationId, 2200m, date)));

            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
            await using var db = NewDb();
            (await db.PieceRates.AsNoTracking().CountAsync(r => r.OperationId == P.Op2.OperationId && r.EffectiveDate == date)).Should().Be(1);
        });
    }

    [Fact]
    public async Task IT_B3_161_Concurrent_Approve_And_Reject_Have_One_Winner()
    {
        var admin = await TkAdm();
        await Loop(async i =>
        {
            var id = await PendingRateAsync(i);
            var results = await RaceAsync(
                () => PostAsync(A + $"piece-rates/{id}/approve", admin, new { Note = "A" }),
                () => PostAsync(A + $"piece-rates/{id}/reject", admin, new { Reason = "R" }));

            AssertRaceNoServerError(results);
            results.Count(IsSuccess).Should().Be(1, Summary(results));
            results.Count(r => r.Code is 400 or 409).Should().Be(1, Summary(results));
            var row = await RateAsync(id);
            if (row.Status == "APPROVED") (row.DecisionNote, row.ApprovedAt != null).Should().Be(("A", true));
            else (row.Status, row.DecisionNote, row.ApprovedAt).Should().Be(("REJECTED", "R", (DateTime?)null));
        });
    }

    [Fact]
    public async Task IT_B3_162_Two_Concurrent_Approves_Write_Approval_Once()
    {
        var admin = await TkAdm();
        await Loop(async i =>
        {
            var id = await PendingRateAsync(i);
            var results = await RaceAsync(
                () => PostAsync(A + $"piece-rates/{id}/approve", admin, new { Note = "A1" }),
                () => PostAsync(A + $"piece-rates/{id}/approve", admin, new { Note = "A2" }));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409 || r.Code == 400, Summary(results));
            results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1);
            var row = await RateAsync(id);
            row.Status.Should().Be("APPROVED");
            row.ApprovedAt.Should().NotBeNull();
            row.DecisionNote.Should().BeOneOf("A1", "A2");
        });
    }

    [Fact]
    public async Task IT_B3_163_Concurrent_Update_And_Submit_Never_Leave_Inconsistent_State()
    {
        var token = await TkAcc();
        await Loop(async i =>
        {
            var id = await PendingRateAsync(i, submit: false);
            var date = new DateOnly(2028, 1, 1).AddDays(i);
            var results = await RaceAsync(
                () => PutAsync(AC + $"piece-rates/{id}", token, Rate(P.Op2.OperationId, 1777m, date)),
                () => PostAsync(AC + $"piece-rates/{id}/submit", token));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 400 || r.Code == 409, Summary(results));
            var row = await RateAsync(id);
            row.Status.Should().BeOneOf("DRAFT", "PENDING_APPROVAL");
            (row.Status == "PENDING_APPROVAL").Should().Be(row.SubmittedAt != null);
        });
    }

    // ---------------------------------------------------------------- 171

    [Fact]
    public async Task IT_B3_171_Triggers_Hold_Over_Ten_Rounds()
    {
        var admin = await TkAdm();
        var acc = await TkAcc();
        var approved = await RateAsync(P.PrA1.PieceRateId);
        var import = A + $"products/{B.Pr1.ProductId}/import-data?orderId={B.Od1.OrderId}&parts=markers";

        static void Quiet(params Api[] results)
        {
            AssertRaceNoServerError(results);
            foreach (var r in results) r.Body.Should().NotContainAny("trg_ProductMarker", "trg_PieceRate", "SqlException");
        }

        await Loop(async i =>
        {
            Quiet(await PostAsync(import, admin, OMarkerWidth("W171", 3.51m)));
            Quiet(await PostAsync(import, admin, OMarkerRatio("R171", "XXL")));
            Quiet(await PutAsync(AC + $"piece-rates/{P.PrA1.PieceRateId}", acc, Rate(P.Op1.OperationId, 9999m, new DateOnly(2026, 1, 1))));
            Quiet(await RaceAsync(
                () => PostAsync(A + $"piece-rates/{P.PrP1.PieceRateId}/approve", admin, new { Note = "A" }),
                () => PostAsync(A + $"piece-rates/{P.PrP1.PieceRateId}/reject", admin, new { Reason = "R" })));
        }, rounds: 10);

        await using var db = NewDb();
        (await db.ProductMarkers.AsNoTracking().AnyAsync(m => m.WidthM > 3.50m)).Should().BeFalse();
        (await db.ProductMarkerRatios.AsNoTracking().AnyAsync(r => !db.ProductMarkers.Any(m => m.ProductMarkerId == r.ProductMarkerId))).Should().BeFalse();
        (await db.ProductMarkers.AsNoTracking().AnyAsync(m => m.MarkerCode == "R171" || m.MarkerCode == "W171")).Should().BeFalse();
        var now = await RateAsync(P.PrA1.PieceRateId);
        (now.Rate, now.Status, now.ApprovedAt).Should().Be((approved.Rate, approved.Status, approved.ApprovedAt));
    }

    private static object OMarkerWidth(string code, decimal width) => OneMarker(code, width, new[] { new { SizeCode = "M", PiecesPerPly = 1 } });
    private static object OMarkerRatio(string code, string size) => OneMarker(code, 1.50m, new[] { new { SizeCode = size, PiecesPerPly = 1 } });

    private static object OneMarker(string code, decimal width, object ratios) => new
    {
        HasComponents = false, Components = Array.Empty<object>(), HasNorms = false, Norms = Array.Empty<object>(),
        HasMarkerGroups = false, MarkerGroups = Array.Empty<object>(), HasOrderMarkers = true,
        OrderMarkers = new[]
        {
            new
            {
                RowNumber = 1, ProductCode = (string?)null, MarkerNo = (int?)1, MarkerCode = code, LengthM = (decimal?)6.00m, WidthM = (decimal?)width,
                Status = "ACTIVE", Ratios = ratios, ParseError = (string?)null,
            },
        },
        SizeColumns = new[] { "M", "L" },
    };
}
