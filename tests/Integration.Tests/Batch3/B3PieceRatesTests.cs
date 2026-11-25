using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_132–160, 164–167 — vòng đời đơn giá, UQ_PieceRate, trigger trg_PieceRate_Approved. 139, 161–163 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3PieceRatesTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post, Put = HttpMethod.Put;
    private const string Max = "9999999999999999.99";

    private Task<Api> Create(int opId, decimal rate, DateOnly date) => Acc(Post, AC + "piece-rates", new { OperationId = opId, Rate = rate, EffectiveDate = date });
    private Task<Api> Update(int id, int opId, decimal rate, DateOnly date) => Acc(Put, AC + $"piece-rates/{id}", new { OperationId = opId, Rate = rate, EffectiveDate = date });
    private Task<Api> Approve(int id, object? body = null) => Adm(Post, A + $"piece-rates/{id}/approve", body ?? new { Note = (string?)null });
    private Task<Api> Reject(int id, object body) => Adm(Post, A + $"piece-rates/{id}/reject", body);
    private Task<Api> SubmitMany(object body) => Acc(Post, AC + "piece-rates/submit-many", body);
    private Task<Api> ApproveMany(object body) => Adm(Post, A + "piece-rates/approve-many", body);
    private static decimal Dec(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    private static DateOnly Today => DateOnly.FromDateTime(VnNow());

    private async Task AssertRateUnchanged(PieceRate expected)
    {
        var now = await RateAsync(expected.PieceRateId);
        (now.Rate, now.Status, now.EffectiveDate, now.SubmittedAt, now.ApprovedAt, now.DecisionNote)
            .Should().Be((expected.Rate, expected.Status, expected.EffectiveDate, expected.SubmittedAt, expected.ApprovedAt, expected.DecisionNote));
    }

    // ---------------------------------------------------------------- 132–139 tạo

    [Fact]
    public async Task IT_B3_132_Create_Rate_Is_Draft_With_No_Decision_Fields()
    {
        var r = await Create(P.Op2.OperationId, 2200.50m, new DateOnly(2027, 3, 1));

        Created(r);
        var row = (await RatesOfAsync(P.Op2.OperationId, new DateOnly(2027, 3, 1))).Single();
        (row.Rate, row.Status).Should().Be((2200.50m, "DRAFT"));
        (row.SubmittedAt, row.ApprovedAt, row.DecisionNote).Should().Be(((DateTime?)null, (DateTime?)null, (string?)null));
    }

    [Fact]
    public async Task IT_B3_133_Same_Operation_And_Date_Returns_409()
    {
        var before = await Snap();
        var r = await Create(P.Op1.OperationId, 1600m, D1201);

        Failed(r, 409);
        (await RatesOfAsync(P.Op1.OperationId, D1201)).Should().ContainSingle();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_134_Recreate_Rate_On_Rejected_Date_Never_Duplicates_Row()
    {
        var r = await Create(P.Op2.OperationId, 1900m, new DateOnly(2026, 11, 1));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400, 409);
        var rows = await RatesOfAsync(P.Op2.OperationId, new DateOnly(2026, 11, 1));
        rows.Should().ContainSingle();
        Note($"lập lại đơn giá bị từ chối → {r.Code}; Status = {rows[0].Status}");
    }

    [Theory]
    [InlineData("IT_B3_135", "min")]
    [InlineData("IT_B3_135", "missing")]
    [InlineData("IT_B3_135", "badMonth")]
    public async Task IT_B3_135_Invalid_EffectiveDate_Returns_400(string caseId, string kind)
    {
        var before = await Snap();
        var op = P.Op2.OperationId;
        var r = kind switch
        {
            "min" => await Create(op, 100m, DateOnly.MinValue),
            "missing" => await Acc(Post, AC + "piece-rates", new { OperationId = op, Rate = 100m }),
            _ => await Acc(Post, AC + "piece-rates", $"{{\"operationId\":{op},\"rate\":100,\"effectiveDate\":\"2027-13-01\"}}"),
        };

        Failed(r, 400);
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_136", -1, 400)]
    [InlineData("IT_B3_136", 0, 201)]
    [InlineData("IT_B3_136", 1, 201)]
    public async Task IT_B3_136_EffectiveDate_Around_Today(string caseId, int offsetDays, int expected)
    {
        var r = await Create(P.Op2.OperationId, 2300m, Today.AddDays(offsetDays));

        r.ShouldNotBeServerError();
        if (expected == 201) Created(r); else r.Code.Should().Be(expected, r.ToString());
        var stored = await RatesOfAsync(P.Op2.OperationId, Today.AddDays(offsetDays));
        stored.Count.Should().Be(expected == 201 ? 1 : 0);
    }

    [Theory]
    [InlineData("IT_B3_137", "0", 400)]
    [InlineData("IT_B3_137", "-0.01", 400)]
    [InlineData("IT_B3_137", "0.01", 201)]
    [InlineData("IT_B3_137", Max, 201)]
    [InlineData("IT_B3_137", "10000000000000000", 400)]
    [InlineData("IT_B3_137", "100.555", 0)]
    [InlineData("IT_B3_137", "missing", 400)]
    public async Task IT_B3_137_Rate_Boundaries(string caseId, string rate, int expected)
    {
        var date = new DateOnly(2027, 6, 1);
        var r = rate == "missing"
            ? await Acc(Post, AC + "piece-rates", new { OperationId = P.Op2.OperationId, EffectiveDate = date })
            : await Create(P.Op2.OperationId, Dec(rate), date);

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 201, 400);
        else if (expected == 201) Created(r);
        else r.Code.Should().Be(expected, r.ToString());
        var stored = await RatesOfAsync(P.Op2.OperationId, date);
        if (r.Code >= 400) stored.Should().BeEmpty();
        else if (rate is "0.01" or Max) stored.Single().Rate.Should().Be(Dec(rate));
    }

    [Fact]
    public async Task IT_B3_138_Unknown_Or_Discontinued_Operation_Is_Rejected()
    {
        var before = await Snap();
        var missing = await Create(await MissingIdAsync(), 100m, new DateOnly(2027, 7, 1));
        var retired = await Create(P.Op5.OperationId, 100m, new DateOnly(2027, 7, 1));

        Failed(missing, 400, 404);
        Failed(retired, 400);
        await AssertSame(before);
    }

    // ---------------------------------------------------------------- 140–149 sửa, gửi duyệt

    [Fact]
    public async Task IT_B3_140_Update_Draft_Rate()
    {
        var r = await Update(P.PrD1.PieceRateId, P.Op1.OperationId, 1550m, new DateOnly(2026, 12, 15));

        r.Code.Should().Be(200, r.ToString());
        var row = await RateAsync(P.PrD1.PieceRateId);
        (row.Rate, row.EffectiveDate, row.Status).Should().Be((1550.00m, new DateOnly(2026, 12, 15), "DRAFT"));
    }

    [Fact]
    public async Task IT_B3_141_Approved_Rate_Cannot_Be_Edited()
    {
        var approved = await RateAsync(P.PrA1.PieceRateId);
        var r = await Update(P.PrA1.PieceRateId, P.Op1.OperationId, 9999m, new DateOnly(2026, 1, 1));

        Failed(r, 400, 409);
        await AssertRateUnchanged(approved);
    }

    [Fact]
    public async Task IT_B3_142_Editing_Pending_Rate_Is_Consistent()
    {
        var pending = await RateAsync(P.PrP1.PieceRateId);
        var r = await Update(P.PrP1.PieceRateId, P.Op2.OperationId, 2500m, D1201);

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 409);
        if (r.Code == 200) { var row = await RateAsync(P.PrP1.PieceRateId); (row.Rate, row.Status).Should().Be((2500m, "DRAFT")); }
        else await AssertRateUnchanged(pending);
    }

    [Fact]
    public async Task IT_B3_143_Update_To_Other_Product_Operation_Or_Taken_Date_Is_Rejected()
    {
        var draft = await RateAsync(P.PrD1.PieceRateId);
        var cross = await Update(P.PrD1.PieceRateId, P.Op9.OperationId, 1500m, D1201);
        var taken = await Update(P.PrD1.PieceRateId, P.Op1.OperationId, 1500m, new DateOnly(2026, 1, 1));

        Failed(cross, 400, 404);
        Failed(taken, 409);
        await AssertRateUnchanged(draft);
    }

    [Fact]
    public async Task IT_B3_144_Update_Unknown_Rate_Returns_404()
    {
        var before = await Snap();
        var missing = await Update(await MissingIdAsync(), P.Op1.OperationId, 100m, D1201);
        var notNumber = await Acc(Put, AC + "piece-rates/abc", new { OperationId = P.Op1.OperationId, Rate = 100m, EffectiveDate = D1201 });

        Failed(missing, 404);
        Failed(notNumber, 404);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_145_Submit_Draft_Moves_To_Pending()
    {
        var r = await Acc(Post, AC + $"piece-rates/{P.PrD1.PieceRateId}/submit");

        r.Code.Should().Be(200, r.ToString());
        var row = await RateAsync(P.PrD1.PieceRateId);
        row.Status.Should().Be("PENDING_APPROVAL");
        AssertCreatedNow(row.SubmittedAt!.Value);
    }

    [Fact]
    public async Task IT_B3_146_Submit_Wrong_State_Is_Rejected()
    {
        var approved = await RateAsync(P.PrA1.PieceRateId);
        var pending = await RateAsync(P.PrP1.PieceRateId);
        var onApproved = await Acc(Post, AC + $"piece-rates/{P.PrA1.PieceRateId}/submit");
        var onPending = await Acc(Post, AC + $"piece-rates/{P.PrP1.PieceRateId}/submit");
        var missing = await Acc(Post, AC + $"piece-rates/{await MissingIdAsync()}/submit");

        Failed(onApproved, 400, 409);
        Failed(onPending, 400, 409);
        Failed(missing, 404);
        await AssertRateUnchanged(approved);
        await AssertRateUnchanged(pending);
    }

    [Fact]
    public async Task IT_B3_147_Submit_Many_Drafts()
    {
        var second = await Create(P.Op2.OperationId, 2400m, new DateOnly(2027, 5, 1));
        Created(second);
        var d2 = (await RatesOfAsync(P.Op2.OperationId, new DateOnly(2027, 5, 1))).Single();

        var r = await SubmitMany(new { Ids = new[] { P.PrD1.PieceRateId, d2.PieceRateId } });

        r.Code.Should().Be(200, r.ToString());
        foreach (var id in new[] { P.PrD1.PieceRateId, d2.PieceRateId })
        {
            var row = await RateAsync(id);
            row.Status.Should().Be("PENDING_APPROVAL");
            row.SubmittedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task IT_B3_148_Submit_Many_With_Invalid_Item_Is_All_Or_Nothing_Or_Reported()
    {
        var draft = await RateAsync(P.PrD1.PieceRateId);
        var r = await SubmitMany(new { Ids = new[] { P.PrD1.PieceRateId, P.PrA1.PieceRateId, await MissingIdAsync() } });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 404, 409);
        if (r.Code != 200) await AssertRateUnchanged(draft);
        (await RateAsync(P.PrA1.PieceRateId)).Status.Should().Be("APPROVED");
    }

    [Theory]
    [InlineData("IT_B3_149", "empty", 400)]
    [InlineData("IT_B3_149", "null", 400)]
    [InlineData("IT_B3_149", "dup", 200)]
    public async Task IT_B3_149_Submit_Many_Ids_Edge_Cases(string caseId, string kind, int expected)
    {
        var body = kind switch
        {
            "empty" => (object)new { Ids = Array.Empty<int>() },
            "null" => new { Ids = (int[]?)null },
            _ => new { Ids = new[] { P.PrD1.PieceRateId, P.PrD1.PieceRateId } },
        };

        var r = await SubmitMany(body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        (await RateAsync(P.PrD1.PieceRateId)).Status.Should().Be(expected == 200 ? "PENDING_APPROVAL" : "DRAFT");
    }

    // ---------------------------------------------------------------- 150–160 duyệt / từ chối

    [Fact]
    public async Task IT_B3_150_Approve_Pending_Rate()
    {
        var r = await Approve(P.PrP1.PieceRateId, new { Note = "Đồng ý" });

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "approvalNote").Should().Be("Đồng ý");
        B1Json.Str(r.Data, "rejectReason").Should().BeNull();
        var row = await RateAsync(P.PrP1.PieceRateId);
        (row.Status, row.DecisionNote).Should().Be(("APPROVED", "Đồng ý"));
        AssertCreatedNow(row.ApprovedAt!.Value);
    }

    [Theory]
    [InlineData("IT_B3_151", "nullNote")]
    [InlineData("IT_B3_151", "emptyBody")]
    public async Task IT_B3_151_Approve_Note_Is_Optional(string caseId, string kind)
    {
        var id = kind == "nullNote" ? P.PrP1.PieceRateId : P.PrP9.PieceRateId;
        var r = await Approve(id, kind == "nullNote" ? new { Note = (string?)null } : new { });

        r.Code.Should().Be(200, r.ToString());
        var row = await RateAsync(id);
        row.DecisionNote.Should().BeNull();
        row.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task IT_B3_152_Only_Pending_Rates_Can_Be_Approved()
    {
        var before = await Snap();
        var draft = await Approve(P.PrD1.PieceRateId);
        var approved = await Approve(P.PrA1.PieceRateId);
        var rejected = await Approve(P.PrR1.PieceRateId);
        var missing = await Approve(await MissingIdAsync());

        Failed(draft, 400, 409);
        Failed(approved, 400, 409);
        Failed(rejected, 400, 409);
        Failed(missing, 404);
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_153", 500, 200)]
    [InlineData("IT_B3_153", 501, 400)]
    public async Task IT_B3_153_Approve_Note_Length(string caseId, int length, int expected)
    {
        var note = Viet(length);
        var r = await Approve(P.PrP1.PieceRateId, new { Note = note });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var row = await RateAsync(P.PrP1.PieceRateId);
        if (expected == 200) row.DecisionNote.Should().Be(note); else row.Status.Should().Be("PENDING_APPROVAL");
    }

    [Fact]
    public async Task IT_B3_154_Reject_Pending_Rate()
    {
        const string reason = "Đơn giá cao hơn thị trường";
        var r = await Reject(P.PrP1.PieceRateId, new { Reason = reason });

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "rejectReason").Should().Be(reason);
        B1Json.Str(r.Data, "approvalNote").Should().BeNull();
        var row = await RateAsync(P.PrP1.PieceRateId);
        (row.Status, row.DecisionNote, row.ApprovedAt).Should().Be(("REJECTED", reason, (DateTime?)null));
    }

    [Theory]
    [InlineData("IT_B3_155", "empty")]
    [InlineData("IT_B3_155", "null")]
    [InlineData("IT_B3_155", "noField")]
    [InlineData("IT_B3_155", "spaces")]
    public async Task IT_B3_155_Reject_Requires_Reason(string caseId, string kind)
    {
        var body = kind switch
        {
            "empty" => (object)new { Reason = "" },
            "null" => new { Reason = (string?)null },
            "noField" => new { },
            _ => new { Reason = "   " },
        };

        var r = await Reject(P.PrP1.PieceRateId, body);

        Failed(r, 400);
        (await RateAsync(P.PrP1.PieceRateId)).Status.Should().Be("PENDING_APPROVAL");
    }

    [Theory]
    [InlineData("IT_B3_156", 500, 200)]
    [InlineData("IT_B3_156", 501, 400)]
    public async Task IT_B3_156_Reject_Reason_Length(string caseId, int length, int expected)
    {
        var reason = Viet(length);
        var r = await Reject(P.PrP1.PieceRateId, new { Reason = reason });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var row = await RateAsync(P.PrP1.PieceRateId);
        if (expected == 200) row.DecisionNote.Should().Be(reason); else row.Status.Should().Be("PENDING_APPROVAL");
    }

    [Fact]
    public async Task IT_B3_157_Approved_Rate_Cannot_Be_Rejected()
    {
        var approved = await RateAsync(P.PrA1.PieceRateId);
        var r = await Reject(P.PrA1.PieceRateId, new { Reason = "Sai" });

        Failed(r, 400, 409);
        await AssertRateUnchanged(approved);
    }

    [Fact]
    public async Task IT_B3_158_Approve_Many_Across_Products()
    {
        var r = await ApproveMany(new { Ids = new[] { P.PrP1.PieceRateId, P.PrP9.PieceRateId }, Note = "Duyệt lô" });

        r.Code.Should().Be(200, r.ToString());
        foreach (var id in new[] { P.PrP1.PieceRateId, P.PrP9.PieceRateId })
        {
            var row = await RateAsync(id);
            (row.Status, row.DecisionNote).Should().Be(("APPROVED", "Duyệt lô"));
            row.ApprovedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task IT_B3_159_Approve_Many_With_Draft_Item_Never_Approves_Draft()
    {
        var pending = await RateAsync(P.PrP1.PieceRateId);
        var r = await ApproveMany(new { Ids = new[] { P.PrP1.PieceRateId, P.PrD1.PieceRateId }, Note = (string?)null });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 409);
        (await RateAsync(P.PrD1.PieceRateId)).Status.Should().Be("DRAFT");
        if (r.Code != 200) await AssertRateUnchanged(pending);
    }

    [Fact]
    public async Task IT_B3_160_Approve_Many_Invalid_Input()
    {
        var before = await Snap();
        var empty = await ApproveMany(new { Ids = Array.Empty<int>(), Note = (string?)null });
        var missing = await ApproveMany(new { Ids = new[] { await MissingIdAsync() }, Note = (string?)null });
        var longNote = await ApproveMany(new { Ids = new[] { P.PrP1.PieceRateId }, Note = Viet(501) });

        Failed(empty, 400);
        Failed(missing, 400, 404);
        Failed(longNote, 400);
        await AssertSame(before);
    }

    // ---------------------------------------------------------------- 164–167 truy vấn

    [Fact]
    public async Task IT_B3_164_Admin_Never_Sees_Drafts_Even_When_Filtering_For_Them()
    {
        var before = await Snap();
        var all = await Adm(Get, A + "piece-rates");
        var draft = await Adm(Get, A + "piece-rates?Status=Draft");

        all.Code.Should().Be(200, all.ToString());
        all.Items().Select(i => B1Json.Int(i, "pieceRateId")).Should().NotContain(P.PrD1.PieceRateId);
        all.Items().Select(i => B1Json.Str(i, "status")!.ToUpperInvariant().Replace("_", "")).Should().NotContain("DRAFT");
        draft.ShouldNotBeServerError();
        if (draft.Code == 200) draft.Items().Select(i => B1Json.Int(i, "pieceRateId")).Should().NotContain(P.PrD1.PieceRateId);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_165_Accountant_Filters_By_Status()
    {
        var before = await Snap();
        var all = await Acc(Get, AC + "piece-rates");
        var pending = await Acc(Get, AC + "piece-rates?Status=PendingApproval");
        var upper = await Acc(Get, AC + "piece-rates?Status=PENDING_APPROVAL");
        var junk = await Acc(Get, AC + "piece-rates?Status=Xyz");

        all.Code.Should().Be(200, all.ToString());
        pending.Code.Should().Be(200, pending.ToString());
        pending.Items().Select(i => B1Json.Int(i, "pieceRateId")!.Value).Should().BeEquivalentTo([P.PrP1.PieceRateId, P.PrP9.PieceRateId]);
        upper.ShouldNotBeServerError();
        junk.ShouldNotBeServerError();
        junk.Code.Should().BeOneOf(200, 400);
        if (junk.Code == 200) junk.Items().Should().BeEmpty();
        Note($"Accountant thấy bản nháp: {all.Items().Any(i => B1Json.Int(i, "pieceRateId") == P.PrD1.PieceRateId)}; PENDING_APPROVAL → {upper.Code}");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_166_Cross_Product_Filter_Returns_Empty()
    {
        var before = await Snap();
        var r = await Acc(Get, AC + $"piece-rates?ProductId={B.Pr1.ProductId}&OperationId={P.Op9.OperationId}");

        r.Code.Should().Be(200, r.ToString());
        r.Items().Should().BeEmpty();
        r.Body.Should().NotContain("2100");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_167_Operation_List_Carries_Rate_Info()
    {
        (await Approve(P.PrP1.PieceRateId, new { Note = "OK" })).Code.Should().Be(200);

        var r = await Acc(Get, AC + $"operations?productId={B.Pr1.ProductId}");

        r.Code.Should().Be(200, r.ToString());
        var op2 = r.Items().Single(i => B1Json.Str(i, "operationCode") == "CD02");
        new[] { "currentRate", "nextRate", "upcomingRate" }.Any(k => B1Json.Prop(op2, k) is { ValueKind: not System.Text.Json.JsonValueKind.Null }).Should().BeTrue();
        op2.GetRawText().Should().Contain("Sai giá", "RejectReason của PR_R1 lấy từ DecisionNote");
        (await RateAsync(P.PrR1.PieceRateId)).DecisionNote.Should().Be("Sai giá");
    }
}
