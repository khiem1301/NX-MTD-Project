using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_168–170 — kiểm tra chéo trạng thái và nhật ký. 171 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3CrossCheckTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Post = HttpMethod.Post, Put = HttpMethod.Put, Get = HttpMethod.Get;

    [Fact]
    public async Task IT_B3_168_All_Stored_Codes_Are_Upper_Case()
    {
        Created(await Acc(Post, AC + "operations", SaveOp(B.Pr1.ProductId, "CD68", team: "Cutting")));
        Created(await Acc(Post, AC + "piece-rates", new { OperationId = P.Op2.OperationId, Rate = 2000m, EffectiveDate = new DateOnly(2027, 8, 1) }));
        (await Acc(Post, AC + $"piece-rates/{P.PrD1.PieceRateId}/submit")).Code.Should().Be(200);
        (await Adm(Post, A + $"piece-rates/{P.PrP1.PieceRateId}/approve", new { Note = "OK" })).Code.Should().Be(200);
        (await Adm(Post, A + $"piece-rates/{P.PrP9.PieceRateId}/reject", new { Reason = "Cao" })).Code.Should().Be(200);

        await using var db = NewDb();
        var values = new List<string>();
        values.AddRange(await db.Products.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.ProductComponents.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.ProductComponents.AsNoTracking().Select(x => x.ComponentType).Distinct().ToListAsync());
        values.AddRange(await db.ProductComponentMaterials.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.ProductMarkers.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.Operations.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.Operations.AsNoTracking().Select(x => x.TeamType).Distinct().ToListAsync());
        values.AddRange(await db.PieceRates.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());

        values.Should().OnlyContain(v => v == v.ToUpperInvariant(), "mọi giá trị lưu phải VIẾT HOA, không PascalCase");
        values.Distinct().Should().BeSubsetOf(["ACTIVE", "DISCONTINUED", "DRAFT", "PENDING_APPROVAL", "APPROVED", "REJECTED", "SEWING", "CUTTING", "PANEL", "TRIM"]);
        values.Should().Contain(["CUTTING", "PENDING_APPROVAL", "APPROVED", "REJECTED"]);
    }

    [Fact]
    public async Task IT_B3_169_Failed_Operations_Leave_No_Audit_And_No_Partial_Data()
    {
        var before = await Snap();
        var lastAudit = await LastAuditIdAsync();
        var id = B.Pr1.ProductId;

        var spec = await Adm(Put, A + $"products/{id}/spec", SpecBody(norms: [Norm("Thân trước", "Xanh", B.Ma1.MaterialId)]));
        var markers = await Adm(Post, A + $"products/{id}/import-data?orderId={B.Od1.OrderId}&parts=markers",
            Workbook(markers: [OMarker("V1", 1, 6.00m, 1.50m, row: 1), OMarker("V2", 1, 6.00m, 4.00m, row: 2)]));
        var final = await Acc(Post, AC + "operations", SaveOp(id, "CD100", final: true));
        var import = await Acc(Post, AC + $"operations/import-data?productId={id}",
            Sheet(OpRow(1, 20, "Hợp lệ 20"), OpRow(2, 22, "Lỗi", err: "Ô không đọc được")));
        var approved = await Acc(Put, AC + $"piece-rates/{P.PrA1.PieceRateId}", new { OperationId = P.Op1.OperationId, Rate = 9999m, EffectiveDate = new DateOnly(2026, 1, 1) });

        Failed(spec, 400);
        Failed(markers, 400, 409);
        Failed(final, 400, 409);
        Failed(import, 400);
        Failed(approved, 400, 409);
        await AssertNoNewAuditAsync(lastAudit);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_170_Rate_Chain_Is_Chronological_And_Visible_On_Product()
    {
        var lastAudit = await LastAuditIdAsync();
        var date = new DateOnly(2027, 9, 1);

        var create = await Acc(Post, AC + "piece-rates", new { OperationId = P.Op2.OperationId, Rate = 2600m, EffectiveDate = date });
        Created(create);
        var rate = (await RatesOfAsync(P.Op2.OperationId, date)).Single();
        var submit = await Acc(Post, AC + $"piece-rates/{rate.PieceRateId}/submit");
        var approve = await Adm(Post, A + $"piece-rates/{rate.PieceRateId}/approve", new { Note = "Chốt" });
        var product = await Adm(Get, A + $"products/{B.Pr1.ProductId}");

        submit.Code.Should().Be(200, submit.ToString());
        approve.Code.Should().Be(200, approve.ToString());
        product.Code.Should().Be(200, product.ToString());
        var final = await RateAsync(rate.PieceRateId);
        (final.Status, final.DecisionNote).Should().Be(("APPROVED", "Chốt"));
        final.SubmittedAt!.Value.Should().BeOnOrBefore(final.ApprovedAt!.Value);
        B1Json.Prop(product.Data, "history")!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        var rows = await AuditRowsAfterAsync(lastAudit);
        Note("Audit rows: " + string.Join("; ", rows.Select(a => $"{a.TableName}#{a.RecordId} {a.Action}")));
    }
}
