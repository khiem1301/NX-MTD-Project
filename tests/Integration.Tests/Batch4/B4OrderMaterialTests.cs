using System.Globalization;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_129–166 — OrderMaterials: NVL của đơn, cột computed, lịch giao, import Excel [Đ6].</summary>
public sealed class B4OrderMaterialTests(ITestOutputHelper output) : B4TestBase(output)
{
    private const string Om = A + "order-materials";

    private static decimal Dec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private Task<Api> Create(int orderId, int materialId, decimal required, decimal customer, string? note = null) =>
        Adm(Post, Om, OmBody(orderId, materialId, required, customer, note));

    private async Task<int> CountMaterialsAsync(string code)
    {
        await using var db = NewDb();
        return await db.Materials.AsNoTracking().CountAsync(m => m.MaterialCode == code);
    }

    private static readonly string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // ---------------------------------------------------------------- 129–135 tạo / sửa / xóa

    [Fact]
    public async Task IT_B4_129_Create_Computes_PurchaseQty_And_Source()
    {
        var r = await Create(D.Od1.OrderId, B.Ma1.MaterialId, 150.5m, 50m, "Vải chính");

        Created(r);
        var om = (await OmAsync(D.Od1.OrderId, B.Ma1.MaterialId))!;
        om.PurchaseQty.Should().Be(100.500m);
        om.Source.Should().Be("BOTH");
        om.Note.Should().Be("Vải chính");
        Note($"MaterialStatus của OD1 sau khi thêm NVL = {(await OrderAsync(D.Od1.OrderId))!.MaterialStatus}");
    }

    [Theory]
    [InlineData("IT_B4_130", "0", "SUPPLIER", "100")]
    [InlineData("IT_B4_130", "100", "CUSTOMER", "0")]
    [InlineData("IT_B4_130", "150", "CUSTOMER", "0")]
    public async Task Source_And_PurchaseQty_Follow_Customer_Share(string caseId, string customer, string source, string purchase)
    {
        var r = await Mgr(Post, M + "order-materials", OmBody(D.Od1.OrderId, B.Ma1.MaterialId, 100m, Dec(customer)));

        Created(r);
        var om = (await OmAsync(D.Od1.OrderId, B.Ma1.MaterialId))!;
        om.Source.Should().Be(source);
        om.PurchaseQty.Should().Be(Dec(purchase));
    }

    [Fact]
    public async Task IT_B4_131_Duplicate_Order_Material_Returns_409()
    {
        var before = await Snap();

        var r = await Create(D.Od2.OrderId, B.Ma1.MaterialId, 10m, 0m);

        Rejected(r, 409);
        (await OmsAsync(D.Od2.OrderId)).Count(o => o.MaterialId == B.Ma1.MaterialId).Should().Be(1);
        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_133", "0", "400")]
    [InlineData("IT_B4_133", "-1", "400")]
    [InlineData("IT_B4_133", "0.001", "201")]
    [InlineData("IT_B4_133", "999999999999999.999", "201")]
    [InlineData("IT_B4_133", "1000000000000000.000", "400")]
    [InlineData("IT_B4_133", "1.0005", "400|201")]
    public async Task RequiredQty_Boundaries(string caseId, string required, string expected)
    {
        var r = await Create(D.Od1.OrderId, B.Ma1.MaterialId, Dec(required), 0m);

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        var allowed = expected.Split('|').Select(int.Parse).SelectMany(c => c == 201 ? new[] { 200, 201 } : [c]).ToArray();
        r.Code.Should().BeOneOf(allowed, r.ToString());
        (await OmAsync(D.Od1.OrderId, B.Ma1.MaterialId) is not null).Should().Be(r.Code is 200 or 201);
    }

    [Theory]
    [InlineData("IT_B4_134", "customer-1", "400")]
    [InlineData("IT_B4_134", "note500", "201")]
    [InlineData("IT_B4_134", "note501", "400")]
    public async Task CustomerQty_And_Note_Boundaries(string caseId, string kind, string expected)
    {
        var r = kind switch
        {
            "customer-1" => await Create(D.Od1.OrderId, B.Ma1.MaterialId, 10m, -1m),
            "note500" => await Create(D.Od1.OrderId, B.Ma1.MaterialId, 10m, 0m, Viet(500)),
            _ => await Create(D.Od1.OrderId, B.Ma1.MaterialId, 10m, 0m, Viet(501)),
        };

        if (expected == "201") Created(r); else Rejected(r, 400);
        (await OmAsync(D.Od1.OrderId, B.Ma1.MaterialId) is not null).Should().Be(expected == "201");
        if (kind == "note500") (await OmAsync(D.Od1.OrderId, B.Ma1.MaterialId))!.Note.Should().Be(Viet(500));
    }

    [Fact]
    public async Task IT_B4_135_Unknown_Or_Closed_References_Are_Rejected()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await Create(D.Od1.OrderId, missing, 10m, 0m), 404, 400);
        Rejected(await Create(D.Od1.OrderId, B.Ma4.MaterialId, 10m, 0m), 400);
        Rejected(await Create(missing, B.Ma1.MaterialId, 10m, 0m), 404, 400);
        Rejected(await Create(D.Od3.OrderId, B.Ma1.MaterialId, 10m, 0m), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_136_Batch_Create_Three_Materials()
    {
        var body = new { OrderId = D.Od1.OrderId, Lines = new[] { OmLine(B.Ma1.MaterialId, 100m, 0m), OmLine(B.Ma2.MaterialId, 50m, 50m), OmLine(B.Ma3.MaterialId, 80m, 20m) } };

        var r = await Adm(Post, Om + "/batch", body);

        Created(r);
        var oms = await OmsAsync(D.Od1.OrderId);
        oms.Should().HaveCount(3);
        oms.Single(o => o.MaterialId == B.Ma1.MaterialId).Should().Match<OrderMaterial>(o => o.Source == "SUPPLIER" && o.PurchaseQty == 100m);
        oms.Single(o => o.MaterialId == B.Ma2.MaterialId).Should().Match<OrderMaterial>(o => o.Source == "CUSTOMER" && o.PurchaseQty == 0m);
        oms.Single(o => o.MaterialId == B.Ma3.MaterialId).Should().Match<OrderMaterial>(o => o.Source == "BOTH" && o.PurchaseQty == 60m);
    }

    [Fact]
    public async Task IT_B4_137_Batch_With_Invalid_Line_Is_All_Or_Nothing()
    {
        var before = await Snap();
        var body = new { OrderId = D.Od1.OrderId, Lines = new[] { OmLine(B.Ma1.MaterialId, 100m, 0m), OmLine(B.Ma2.MaterialId, -5m, 0m), OmLine(B.Ma3.MaterialId, 80m, 20m) } };

        Rejected(await Adm(Post, Om + "/batch", body), 400);

        (await OmsAsync(D.Od1.OrderId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_138_Batch_With_Duplicate_Material_Is_Rejected()
    {
        var before = await Snap();
        var body = new { OrderId = D.Od1.OrderId, Lines = new[] { OmLine(B.Ma1.MaterialId, 100m, 0m), OmLine(B.Ma1.MaterialId, 10m, 0m) } };

        Rejected(await Adm(Post, Om + "/batch", body), 400, 409);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_139_Batch_Containing_Existing_Material_Adds_Nothing()
    {
        var before = await Snap();
        var body = new { OrderId = D.Od2.OrderId, Lines = new[] { OmLine(B.Ma1.MaterialId, 10m, 0m), OmLine(B.Ma2.MaterialId, 10m, 0m) } };

        Rejected(await Adm(Post, Om + "/batch", body), 409, 400);

        (await OmAsync(D.Od2.OrderId, B.Ma2.MaterialId)).Should().BeNull();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_140_Batch_With_Empty_Or_Null_Lines_Returns_400()
    {
        var before = await Snap();

        Rejected(await Adm(Post, Om + "/batch", new { OrderId = D.Od1.OrderId, Lines = Array.Empty<object>() }), 400);
        Rejected(await Adm(Post, Om + "/batch", new { OrderId = D.Od1.OrderId, Lines = (object[]?)null }), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_141_Update_Recomputes_Purchase_And_Source()
    {
        var r = await Adm(Put, Om + $"/{D.Om2.OrderMaterialId}", new { MaterialId = B.Ma3.MaterialId, RequiredQty = 120m, CustomerQty = 100m, Note = (string?)null });

        r.Code.Should().Be(200, r.ToString());
        var om = (await OmsAsync(D.Od2.OrderId)).Single(o => o.OrderMaterialId == D.Om2.OrderMaterialId);
        (om.RequiredQty, om.PurchaseQty, om.Source).Should().Be((120.000m, 20.000m, "BOTH"));
    }

    [Fact]
    public async Task IT_B4_142_Update_To_Material_Already_In_Order_Returns_409()
    {
        var before = await Snap();

        var r = await Adm(Put, Om + $"/{D.Om2.OrderMaterialId}", new { MaterialId = B.Ma1.MaterialId, RequiredQty = 100m, CustomerQty = 100m, Note = (string?)null });

        Rejected(r, 409);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_143_Update_Of_Received_Order_Material_Is_Guarded()
    {
        var before = await Snap();

        var swap = await Adm(Put, Om + $"/{D.Om1.OrderMaterialId}", new { MaterialId = B.Ma2.MaterialId, RequiredQty = 150m, CustomerQty = 0m, Note = (string?)null });
        var shrink = await Adm(Put, Om + $"/{D.Om1.OrderMaterialId}", new { MaterialId = B.Ma1.MaterialId, RequiredQty = 30m, CustomerQty = 0m, Note = (string?)null });

        Rejected(swap, 400, 409);
        Rejected(shrink, 400, 409);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_144_Delete_Order_Material_With_Receipt_Returns_409()
    {
        var before = await Snap();

        var r = await Adm(Del, Om + $"/{D.Om1.OrderMaterialId}");

        Rejected(r, 409);
        (await OmsAsync(D.Od2.OrderId)).Select(o => o.OrderMaterialId).Should().Contain(D.Om1.OrderMaterialId);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_145_Delete_Unused_Order_Material_Succeeds()
    {
        int id;
        await using (var db = NewDb())
        {
            var om = new OrderMaterial { OrderId = D.Od1.OrderId, MaterialId = B.Ma2.MaterialId, RequiredQty = 10m, CustomerQty = 0m };
            db.OrderMaterials.Add(om);
            await db.SaveChangesAsync();
            id = om.OrderMaterialId;
        }

        var r = await Adm(Del, Om + $"/{id}");

        r.Code.Should().BeOneOf(200, 204);
        (await OmsAsync(D.Od1.OrderId)).Should().BeEmpty();
        Note($"MaterialStatus của OD1 sau khi xóa NVL = {(await OrderAsync(D.Od1.OrderId))!.MaterialStatus}");
    }

    [Fact]
    public async Task IT_B4_146_Unknown_Order_Material_Returns_404()
    {
        var before = await Snap();
        var missing = await MissingOrderMaterialIdAsync();

        Rejected(await Adm(Del, Om + $"/{missing}"), 404);
        Rejected(await Adm(Put, Om + $"/{missing}", new { MaterialId = B.Ma1.MaterialId, RequiredQty = 1m, CustomerQty = 0m, Note = (string?)null }), 404);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_147_Paged_Filter_And_Received_Short_Quantities()
    {
        var before = await Snap();

        var supply = await Mgr(Get, M + $"order-materials/paged?OrderId={D.Od2.OrderId}&Supply=Customer");
        var junk = await Mgr(Get, M + $"order-materials/paged?OrderId={D.Od2.OrderId}&Supply=Xyz");
        var spec = await Mgr(Get, M + $"order-materials/product-materials?orderId={D.Od2.OrderId}");
        var all = await Mgr(Get, M + $"order-materials?orderId={D.Od2.OrderId}");

        supply.Code.Should().Be(200, supply.ToString());
        IdSet(supply, "orderMaterialId").Should().BeSubsetOf([D.Om1.OrderMaterialId, D.Om2.OrderMaterialId]);
        junk.ShouldNotBeServerError();
        junk.Code.Should().BeOneOf(200, 400);
        if (junk.Code == 200) junk.Items().Should().BeEmpty();
        spec.Code.Should().Be(200, spec.ToString());
        all.Code.Should().Be(200, all.ToString());
        foreach (var i in all.Items())
        {
            var required = B1Json.Dec(i, "requiredQty")!.Value;
            var received = B1Json.Dec(i, "receivedQty")!.Value;
            received.Should().Be(B1Json.Dec(i, "customerReceivedQty")!.Value + B1Json.Dec(i, "purchasedReceivedQty")!.Value);
            B1Json.Dec(i, "shortQty").Should().Be(Math.Max(required - received, 0m));
        }

        var om1 = all.Items().Single(i => B1Json.Int(i, "orderMaterialId") == D.Om1.OrderMaterialId);
        B1Json.Dec(om1, "receivedQty").Should().Be(50m, "khớp MaterialReceiptLine đã xác nhận");
        await Same(before);
    }

    // ---------------------------------------------------------------- 148–154 lịch khách giao

    private Task<Api> SaveDelivery(int orderId, int count, object[] dates) =>
        Adm(Put, Om + $"/customer-delivery/{orderId}", new { DeliveryCount = count, DeliveryDates = dates });

    [Fact]
    public async Task IT_B4_148_Save_Customer_Delivery_Schedule()
    {
        var d1 = Today.AddDays(10); var d2 = Today.AddDays(20);

        var r = await SaveDelivery(D.Od1.OrderId, 2, [d1, d2]);

        r.Code.Should().Be(200, r.ToString());
        var get = await Adm(Get, Om + $"/customer-delivery/{D.Od1.OrderId}");
        get.Code.Should().Be(200, get.ToString());
        B1Json.Prop(get.Data, "deliveryDates")!.Value.GetArrayLength().Should().Be(2);
        var rows = await DsdAsync(D.Od1.OrderId);
        rows.Select(x => (x.SeqNo, x.DeliveryDate)).Should().Equal(((short)1, d1), ((short)2, d2));
        rows.Should().OnlyContain(x => x.PurchaseOrderId == null && x.RequestId == null);
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.CustomerDeliveryCount.Should().Be(2);
        Note($"CustomerDeliveryDate={od.CustomerDeliveryDate}, DateTo={od.CustomerDeliveryDateTo}");
    }

    [Fact]
    public async Task IT_B4_149_Saving_Again_Replaces_Old_Dates()
    {
        (await SaveDelivery(D.Od1.OrderId, 2, [Today.AddDays(10), Today.AddDays(20)])).Code.Should().Be(200);
        var d = Today.AddDays(15);

        var r = await SaveDelivery(D.Od1.OrderId, 1, [d]);

        r.Code.Should().Be(200, r.ToString());
        (await DsdAsync(D.Od1.OrderId)).Select(x => (x.SeqNo, x.DeliveryDate)).Should().Equal(((short)1, d));
    }

    [Theory]
    [InlineData("IT_B4_150", 3, 2, "400")]
    [InlineData("IT_B4_150", 0, 0, "200|400")]
    [InlineData("IT_B4_150", -1, 0, "400")]
    public async Task Delivery_Count_Must_Match_Dates(string caseId, int count, int dates, string expected)
    {
        var list = Enumerable.Range(1, dates).Select(i => (object)Today.AddDays(10 + i)).ToArray();

        var r = await SaveDelivery(D.Od1.OrderId, count, list);

        r.ShouldNotBeServerError();
        var allowed = expected.Split('|').Select(int.Parse).ToArray();
        r.Code.Should().BeOneOf(allowed, r.ToString());
        (await DsdAsync(D.Od1.OrderId)).Should().OnlyContain(x => x.SeqNo >= 1);
        if (r.Code == 400) (await DsdAsync(D.Od1.OrderId)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("IT_B4_151", "min")]
    [InlineData("IT_B4_151", "duplicate")]
    [InlineData("IT_B4_151", "beforeReceived")]
    public async Task Delivery_Dates_Are_Validated(string caseId, string kind)
    {
        object[] dates = kind switch
        {
            "min" => [DateOnly.MinValue],
            "duplicate" => [Today.AddDays(10), Today.AddDays(10)],
            _ => [(await OrderAsync(D.Od1.OrderId))!.ReceivedDate.AddDays(-5)],
        };

        var r = await SaveDelivery(D.Od1.OrderId, dates.Length, dates);

        Rejected(r, 400);
        (await DsdAsync(D.Od1.OrderId)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("IT_B4_152", 32768, 1, "400")]
    [InlineData("IT_B4_152", 32767, 32767, "200|400")]
    public async Task Delivery_Count_Short_Limits(string caseId, int count, int dates, string expected)
    {
        var list = Enumerable.Range(0, dates).Select(i => (object)Today.AddDays(1 + i)).ToArray();

        var r = await SaveDelivery(D.Od1.OrderId, count, list);

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(expected.Split('|').Select(int.Parse).ToArray(), r.ToString());
        (await DsdAsync(D.Od1.OrderId)).Count.Should().Be(r.Code == 200 ? dates : 0);
    }

    [Fact]
    public async Task IT_B4_154_Delivery_Schedule_Of_Unknown_Order_Returns_404()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await SaveDelivery(missing, 1, [Today.AddDays(10)]), 404);

        await using var db = NewDb();
        (await db.DeliveryScheduleDates.AsNoTracking().CountAsync(d => d.OrderId == missing)).Should().Be(0);
        await Same(before);
    }

    // ---------------------------------------------------------------- 155–166 import Excel [Đ6]

    private static readonly string[] Actions = ["New", "Update", "Unchanged"];

    [Fact]
    public async Task IT_B4_155_Import_Preview_Does_Not_Write()
    {
        var file = await OmFileAsync(D.Od1.OrderId, (B.Ma1.MaterialCode, 100m, 0m), (B.Ma2.MaterialCode, 50m, 50m), (B.Ma3.MaterialCode, 80m, 20m));
        var before = await Snap();

        var r = await UploadXlsxAsync(Om + $"/import/preview?orderId={D.Od1.OrderId}", await TkAdm(), file, "om_ok.xlsx");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Prop(r.Data, "rows")!.Value.EnumerateArray().Select(x => B1Json.Str(x, "action")).Should().NotBeEmpty().And.OnlyContain(a => Actions.Contains(a));
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_156_Missing_Or_Broken_File_Returns_400()
    {
        var token = await TkAdm();
        var before = await Snap();

        foreach (var path in new[] { "/import/preview", "/import" })
        {
            var url = Om + path + $"?orderId={D.Od1.OrderId}";
            Rejected(await UploadXlsxAsync(url, token, null), 400);
            Rejected(await UploadAsync(url, token, ZeroXlsx(), "zero.xlsx", XlsxType), 400);
            Rejected(await UploadAsync(url, token, CorruptXlsx(), "corrupt.xlsx", XlsxType), 400);
            Rejected(await UploadAsync(url, token, PdfBytes(), "not_excel.pdf", "application/pdf"), 400);
        }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_157_Import_With_Invalid_Row_Rolls_Back_Everything()
    {
        var file = await OmFileAsync(D.Od1.OrderId, (B.Ma1.MaterialCode, 100m, 0m), (B.Ma2.MaterialCode, -5m, 0m), (B.Ma3.MaterialCode, 80m, 20m));
        var before = await Snap();

        var r = await UploadXlsxAsync(Om + $"/import?orderId={D.Od1.OrderId}", await TkAdm(), file, "om_err.xlsx");

        Rejected(r, 400);
        (await OmsAsync(D.Od1.OrderId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_158_Import_Creates_Rows_With_Computed_Columns()
    {
        var file = await OmFileAsync(D.Od1.OrderId, (B.Ma1.MaterialCode, 100m, 0m), (B.Ma2.MaterialCode, 50m, 50m), (B.Ma3.MaterialCode, 80m, 20m));

        var r = await UploadXlsxAsync(Om + $"/import?orderId={D.Od1.OrderId}", await TkAdm(), file, "om_ok.xlsx");

        r.Code.Should().Be(200, r.ToString());
        var oms = await OmsAsync(D.Od1.OrderId);
        oms.Select(o => (o.MaterialId, o.RequiredQty, o.CustomerQty, o.Source, o.PurchaseQty)).Should().BeEquivalentTo(new[]
        {
            (B.Ma1.MaterialId, 100m, 0m, "SUPPLIER", (decimal?)100m), (B.Ma2.MaterialId, 50m, 50m, "CUSTOMER", (decimal?)0m),
            (B.Ma3.MaterialId, 80m, 20m, "BOTH", (decimal?)60m),
        });
    }

    private static object[] NewMaterialRows(params object[] extra) => [OmRow(1, "V-9999", 10m, 0m, "Vải mới", "m"), .. extra];

    [Fact]
    public async Task IT_B4_159_PreviewData_Of_Unknown_Material_Does_Not_Create_It()
    {
        var before = await Snap();

        var r = await Adm(Post, Om + $"/import/preview-data?orderId={D.Od1.OrderId}", NewMaterialRows());

        r.Code.Should().Be(200, r.ToString());
        r.Body.Should().Contain("V-9999");
        (await CountMaterialsAsync("V-9999")).Should().Be(0);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_160_ImportData_Of_Unknown_Material_Adds_It_In_Same_Transaction()
    {
        var r = await Adm(Post, Om + $"/import-data?orderId={D.Od1.OrderId}", NewMaterialRows());

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        Note($"import NVL chưa có danh mục → {r.Code}");
        if (r.Code == 200)
        {
            (await CountMaterialsAsync("V-9999")).Should().Be(1);
            await using var db = NewDb();
            var mid = (await db.Materials.AsNoTracking().SingleAsync(m => m.MaterialCode == "V-9999")).MaterialId;
            (await OmAsync(D.Od1.OrderId, mid)).Should().NotBeNull();
        }
        else (await CountMaterialsAsync("V-9999")).Should().Be(0);
    }

    [Fact]
    public async Task IT_B4_161_Failed_Import_Does_Not_Leave_New_Material()
    {
        var before = await Snap();

        var r = await Adm(Post, Om + $"/import-data?orderId={D.Od1.OrderId}", NewMaterialRows(OmRow(2, B.Ma1.MaterialCode, null, 0m)));

        Rejected(r, 400);
        (await CountMaterialsAsync("V-9999")).Should().Be(0);
        (await OmsAsync(D.Od1.OrderId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_162_Replace_Keeps_Blocked_Row_And_Removes_Others()
    {
        var rows = new[] { OmRow(1, B.Ma2.MaterialCode, 10m, 0m) };
        var preview = await Adm(Post, Om + $"/import/preview-data?orderId={D.Od2.OrderId}&replace=true", rows);
        preview.Code.Should().Be(200, preview.ToString());
        B1Json.Prop(preview.Data, "blocked")!.Value.EnumerateArray().Select(x => x.GetString()).Should().Contain(B.Ma1.MaterialCode);

        var r = await Adm(Post, Om + $"/import-data?orderId={D.Od2.OrderId}&replace=true", rows);

        r.Code.Should().Be(200, r.ToString());
        var oms = await OmsAsync(D.Od2.OrderId);
        oms.Select(o => o.MaterialId).Should().BeEquivalentTo([B.Ma1.MaterialId, B.Ma2.MaterialId]);
        oms.Should().NotContain(o => o.OrderMaterialId == D.Om2.OrderMaterialId);
    }

    [Fact]
    public async Task IT_B4_163_Invalid_Import_Rows_Return_400()
    {
        var before = await Snap();
        var url = Om + $"/import-data?orderId={D.Od1.OrderId}";

        Rejected(await Adm(Post, url, new[] { OmRow(1, B.Ma1.MaterialCode, 10m), OmRow(2, B.Ma1.MaterialCode, 20m) }), 400);
        Rejected(await Adm(Post, url, new[] { OmRow(1, B.Ma1.MaterialCode, null) }), 400);
        Rejected(await Adm(Post, url, new[] { OmRow(1, "", 10m) }), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_164_Empty_Or_Null_Body_Does_Not_Wipe_Order_Materials()
    {
        var before = await Snap();
        var url = Om + $"/import-data?orderId={D.Od2.OrderId}";

        var empty = await Adm(Post, url, Array.Empty<object>());
        var emptyReplace = await Adm(Post, url + "&replace=true", Array.Empty<object>());
        var none = await Adm(Post, url, "null");

        foreach (var r in new[] { empty, emptyReplace }) { r.ShouldNotBeServerError(); r.Code.Should().BeOneOf(200, 400); }
        Rejected(none, 400);
        (await OmsAsync(D.Od2.OrderId)).Select(o => o.OrderMaterialId).Should().BeEquivalentTo([D.Om1.OrderMaterialId, D.Om2.OrderMaterialId]);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_166_Import_Requires_A_Known_OrderId()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();
        var rows = new[] { OmRow(1, B.Ma1.MaterialCode, 10m) };
        var file = await OmFileAsync(D.Od1.OrderId, (B.Ma1.MaterialCode, 10m, 0m));

        Rejected(await Adm(Post, Om + "/import-data", rows), 400);
        Rejected(await Adm(Post, Om + $"/import-data?orderId={missing}", rows), 404);
        Rejected(await UploadXlsxAsync(Om + "/import", await TkAdm(), file), 400);
        Rejected(await UploadXlsxAsync(Om + $"/import?orderId={missing}", await TkAdm(), file), 404);

        await Same(before);
    }
}

