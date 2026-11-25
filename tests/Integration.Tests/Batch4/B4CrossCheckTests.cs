using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_167–172 — import đơn hàng (A1, A2), kiểm tra chéo Status HOA, audit khi lỗi, trigger lặp [Đ3][Đ4][Đ6].</summary>
public sealed class B4CrossCheckTests(ITestOutputHelper output) : B4TestBase(output)
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private object OrderImportDto() => new
    {
        SizeColumns = new[] { "M", "L" },
        Rows = new[]
        {
            new
            {
                RowNumber = 1, ProductCode = B.Pr1.ProductCode, Color = "Đen", CmtUnitPrice = (decimal?)25_000m,
                Quantities = new[] { new { SizeCode = "M", Qty = 10 }, new { SizeCode = "L", Qty = 5 } }, ParseError = (string?)null,
            },
        },
    };

    [Fact]
    public async Task IT_B4_167_Order_Import_Preview_Does_Not_Write()
    {
        var file = await OrderFileAsync((B.Pr1.ProductCode, "Đen", 25_000m, 10, 5));
        var before = await Snap();

        var preview = await UploadXlsxAsync(A + "customer-orders/import/preview", await TkAdm(), file, "order_ok.xlsx");
        var data = await Adm(Post, A + "customer-orders/import/preview-data", OrderImportDto());

        preview.Code.Should().Be(200, preview.ToString());
        data.Code.Should().Be(200, data.ToString());
        B1Json.Prop(data.Data, "rows").Should().NotBeNull();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_168_Order_Import_Preview_Rejects_Broken_Files_And_Reports_Row_Errors()
    {
        var token = await TkAdm();
        var url = A + "customer-orders/import/preview";
        var errFile = await OrderFileAsync((B.Pr1.ProductCode, "Đen", 25_000m, 10, 5), (B.Pr1.ProductCode, "Trắng", 25_000m, 4, 4), (B.Pr1.ProductCode, "Xám", -1m, 3, 3));
        var before = await Snap();

        Rejected(await UploadXlsxAsync(url, token, null), 400);
        Rejected(await UploadAsync(url, token, ZeroXlsx(), "zero.xlsx", Xlsx), 400);
        Rejected(await UploadAsync(url, token, PdfBytes(), "not_excel.pdf", "application/pdf"), 400);
        var err = await UploadXlsxAsync(url, token, errFile, "order_err.xlsx");

        err.Code.Should().Be(200, err.ToString());
        B1Json.Int(err.Data, "errorCount").Should().BeGreaterThan(0);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_169_All_Status_Columns_Are_Stored_Upper_Case()
    {
        Created(await Adm(Post, A + "customer-orders", OrderBody(Code())));
        (await Mgr(Put, MarkersUrl(D.Od1), SaveMarkers(MarkerIn("S1", 1, 6.00m, 1.50m, "Active")))).Code.Should().Be(200);
        Created(await Adm(Post, A + "order-materials", OmBody(D.Od1.OrderId, B.Ma1.MaterialId, 10m, 5m)));

        await using var db = NewDb();
        var orderStatuses = await db.CustomerOrders.AsNoTracking().Select(o => o.Status).Distinct().ToListAsync();
        var materialStatuses = await db.CustomerOrders.AsNoTracking().Select(o => o.MaterialStatus).Distinct().ToListAsync();
        var markerStatuses = await db.ProductMarkers.AsNoTracking().Select(m => m.Status).Distinct().ToListAsync();
        var sources = await db.OrderMaterials.AsNoTracking().Where(o => o.Source != null).Select(o => o.Source!).Distinct().ToListAsync();

        foreach (var value in orderStatuses.Concat(materialStatuses).Concat(markerStatuses).Concat(sources))
            value.Should().Be(value.ToUpperInvariant(), "mọi giá trị trạng thái lưu VIẾT HOA");
        markerStatuses.Should().BeSubsetOf(["ACTIVE", "DISCONTINUED"]);
        sources.Should().BeSubsetOf(["SUPPLIER", "CUSTOMER", "BOTH"]);
    }

    [Fact]
    public async Task IT_B4_170_Failed_Requests_Leave_No_Audit_Or_Partial_Data()
    {
        var bump = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", await UpdateBodyAsync(D.Od1, KeepOd1()));
        bump.Code.Should().Be(200, bump.ToString());
        var last = await LastAuditIdAsync();
        var before = await Snap();
        var staleRowVer = D.Od1.RowVer;
        await using (var db = NewDb())
        {
            db.OrderLineTeams.Add(new OrderLineTeam { OrderLineId = D.Ol1.OrderLineId, SewingTeamId = S0.TSew01.TeamId, AssignedAt = Clock.Now });
            await db.SaveChangesAsync();
        }

        before = await Snap();
        var token = await TkAdm();

        Rejected(await Adm(Post, A + "customer-orders", OrderBody(Code(), [Ln(B.Pr1.ProductId, "Đen", "M", 1), Ln(B.Pr1.ProductId, "Đen", "M", 2)])), 409, 400);
        Rejected(await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", await UpdateBodyAsync(D.Od1, KeepOd1(), staleRowVer)), 409);
        Rejected(await Mgr(Put, M + "order-line-teams", new { OrderLineId = D.Ol1.OrderLineId, TeamIds = new[] { S0.TSew02.TeamId, await NonExistingTeamIdAsync() } }), 400, 404);
        Rejected(await Mgr(Put, MarkersUrl(D.Od1), SaveMarkers(MarkerIn("S1", 1, 6.00m, 1.50m, ratios: [RatioIn("XXL", 1)]))), 400, 409);
        Rejected(await Adm(Post, A + "order-materials/batch", new { OrderId = D.Od1.OrderId, Lines = new[] { OmLine(B.Ma1.MaterialId, 10m, 0m), OmLine(B.Ma2.MaterialId, -1m, 0m) } }), 400);
        var file = await OmFileAsync(D.Od1.OrderId, (B.Ma1.MaterialCode, 10m, 0m), (B.Ma2.MaterialCode, -5m, 0m));
        Rejected(await UploadXlsxAsync(A + $"order-materials/import?orderId={D.Od1.OrderId}", token, file), 400);

        await AssertNoNewAuditAsync(last);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_171_Update_Start_Stop_Chain_Records_History_And_RowVer()
    {
        await using (var db = NewDb())
        {
            db.OrderMaterials.Add(new OrderMaterial { OrderId = D.Od1.OrderId, MaterialId = B.Ma1.MaterialId, RequiredQty = 10m, CustomerQty = 0m });
            (await db.CustomerOrders.SingleAsync(o => o.OrderId == D.Od1.OrderId)).MaterialStatus = "PARTIAL";
            await db.SaveChangesAsync();
        }

        var last = await LastAuditIdAsync();
        var versions = new List<byte[]> { await OrdRowVerAsync(D.Od1.OrderId) };

        (await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", await UpdateBodyAsync(D.Od1, KeepOd1()))).Code.Should().Be(200);
        versions.Add(await OrdRowVerAsync(D.Od1.OrderId));
        (await Adm(Post, A + $"customer-orders/{D.Od1.OrderId}/start-production", new { RowVer = versions[^1] })).Code.Should().Be(200);
        versions.Add(await OrdRowVerAsync(D.Od1.OrderId));
        (await Adm(Post, A + $"customer-orders/{D.Od1.OrderId}/stop", new { Reason = "Hết hàng", RowVer = versions[^1] })).Code.Should().Be(200);
        versions.Add(await OrdRowVerAsync(D.Od1.OrderId));

        var detail = await Adm(Get, A + $"customer-orders/{D.Od1.OrderId}");

        detail.Code.Should().Be(200, detail.ToString());
        versions.Select(Convert.ToHexString).Distinct().Should().HaveCount(4, "RowVer đổi sau mỗi bước");
        var history = B1Json.Prop(detail.Data, "history")!.Value.EnumerateArray().ToList();
        history.Should().HaveCount(3);
        history[^1].GetRawText().Should().Contain("Hết hàng");
        await AssertAuditAddedAsync(last, 3);
    }

    [Fact]
    public async Task IT_B4_172_Marker_Trigger_Failures_Are_Stable_Over_Ten_Rounds()
    {
        for (var i = 0; i < 10; i++)
        {
            var width = await Save(MarkerIn("S1", 1, 6.00m, 3.51m));
            var ratio = await Save(MarkerIn("S1", 1, 6.00m, 1.50m, ratios: [RatioIn("M", 1), RatioIn("XXL", 1)]));
            var pair = await Save(MarkerIn("S1", 1, 6.00m, 1.50m), MarkerIn("S2", 1, 6.00m, 4.00m));
            var first = await Save(MarkerIn("S1", 1, 6.00m, 1.50m));
            var again = await Save(MarkerIn("S1", 1, 6.00m, 1.50m));

            Rejected(width, 400);
            Rejected(ratio, 400, 409);
            Rejected(pair, 400, 409);
            foreach (var r in new[] { first, again })
            {
                r.ShouldNotBeServerError();
                r.Code.Should().BeOneOf(200, 201, 409);
                r.Body.Should().NotContainAny(Leaks);
            }

            await using var db = NewDb();
            (await db.ProductMarkers.AsNoTracking().CountAsync(m => m.WidthM > 3.50m && m.OrderId == D.Od1.OrderId)).Should().Be(0, $"vòng {i}");
            var markers = await MarkersAsync(D.Od1.OrderId, B.Pr1.ProductId);
            markers.GroupBy(m => (m.MarkerNo, m.MarkerCode)).Should().OnlyContain(g => g.Count() == 1, $"vòng {i}: không trùng UQ_PM");
            (await db.ProductMarkerRatios.AsNoTracking().CountAsync(x => !db.ProductMarkers.Any(m => m.ProductMarkerId == x.ProductMarkerId))).Should().Be(0);
        }
    }

    private Task<Api> Save(params object[] markers) => Mgr(Put, MarkersUrl(D.Od1), SaveMarkers(markers));
}

