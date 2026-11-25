using System.Globalization;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

public sealed class B5PurchaseOrderCreateTests(ITestOutputHelper output) : B5TestBase(output)
{
    private static decimal Dc(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private async Task<Api> RejectedPost(object body, params int[] codes)
    {
        var before = await Snap5();
        var r = await Create(body);
        Rejected(r, codes);
        await Unchanged(before);
        return r;
    }

    [Fact]
    public async Task IT_B5_023_Create_Reserve_Order_Persists_Header_Lines_And_Schedule()
    {
        var dates = Dates(2);
        var body = Mk([PL(B.Ma1.MaterialId, 120.5m, 49_000m), PL(B.Ma3.MaterialId, 1000m, 1_500m)], null, B.Su1.SupplierId, 2, dates, "Dự trữ quý 1");

        var r = await Create(body);

        r.Code.Should().Be(201, r.ToString());
        B1Json.Str(r.Data, "poCode").Should().NotBeNullOrEmpty();
        Arr(r.Data, "lines").Should().HaveCount(2);
        B1Json.Int(r.Data, "expectedDeliveryCount").Should().Be(2);
        var po = await PoAsync(PoIdOf(r));
        po.OrderId.Should().BeNull();
        po.SupplierId.Should().Be(B.Su1.SupplierId);
        po.GoodsStatus.Should().BeOneOf("PENDING_FINALISATION", "ORDERED");
        po.PaymentStatus.Should().Be("UNPAID");
        po.CreatedAt.Year.Should().BeGreaterThan(2000);
        var lines = await PolsAsync(po.PurchaseOrderId);
        lines.Should().HaveCount(2).And.OnlyContain(l => l.OrderMaterialId == null);
        lines.Select(l => l.Amount).Should().BeEquivalentTo(new decimal?[] { 5_904_500.00m, 1_500_000.00m });
        var dsd = await PoDsdAsync(po.PurchaseOrderId);
        dsd.Select(d => d.SeqNo).Should().Equal(1, 2);
        dsd.Select(d => d.DeliveryDate).Should().Equal(dates);
        dsd.Should().OnlyContain(d => d.OrderId == null && d.RequestId == null);
    }

    [Fact]
    public async Task IT_B5_024_Create_For_Order_Links_OrderMaterial()
    {
        var r = await Create(Mk([PL(B.Ma1.MaterialId, 80m, 50_000m), PL(B.Ma3.MaterialId, 300m, 2_000m)], D.Od2.OrderId));

        Created(r);
        var po = await PoAsync(PoIdOf(r));
        po.OrderId.Should().Be(D.Od2.OrderId);
        var lines = await PolsAsync(po.PurchaseOrderId);
        lines.Select(l => l.OrderMaterialId).Should().BeEquivalentTo(new int?[] { D.Om1.OrderMaterialId, D.Om2.OrderMaterialId });
    }

    [Fact]
    public async Task IT_B5_025_Duplicate_Material_In_Reserve_Order_Is_Rejected_Atomically()
    {
        var before = await Snap5();

        var r = await Create(Mk([PL(B.Ma1.MaterialId, 5m, 1_000m), PL(B.Ma1.MaterialId, 6m, 1_000m)]));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (r.Code is 200 or 201) (await PolsAsync(PoIdOf(r))).Should().HaveCount(1, "gộp thành 1 dòng");
        else { r.Code.Should().BeOneOf(400, 409); await Unchanged(before); }
    }

    [Fact]
    public async Task IT_B5_026_Duplicate_OrderMaterial_In_Order_Po_Is_Rejected_Atomically()
    {
        var before = await Snap5();

        var r = await Create(Mk([PL(B.Ma1.MaterialId, 5m, 1_000m), PL(B.Ma1.MaterialId, 6m, 1_000m)], D.Od2.OrderId));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (r.Code is 200 or 201) (await PolsAsync(PoIdOf(r))).Should().HaveCount(1);
        else { r.Code.Should().BeOneOf(400, 409); await Unchanged(before); }
    }

    [Fact]
    public async Task IT_B5_027_Same_Material_In_Different_Reserve_Pos_Is_Allowed()
    {
        var r = await Create(Mk([PL(B.Ma1.MaterialId, 10m, 1_000m)]));

        Created(r);
        await using var db = NewDb();
        var poIds = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.MaterialId == B.Ma1.MaterialId && l.OrderMaterialId == null).Select(l => l.PurchaseOrderId).Distinct().ToListAsync();
        poIds.Should().Contain([P.Po2.PurchaseOrderId, PoIdOf(r)]);
    }

    [Fact]
    public async Task IT_B5_028_New_Material_Is_Added_To_Catalog_In_Same_Transaction()
    {
        var name = "Chun bản 2cm " + Rnd(6);

        var r = await Create(Mk([NewPL(name, "cuộn", null, 50m, 12_000m)]));

        Created(r);
        B1Json.Str(Arr(r.Data, "lines")[0], "materialCode").Should().StartWith("PL-");
        var m = (await MaterialByNameAsync(name))!;
        m.MaterialType.Should().Be("TRIM");
        m.Color.Should().Be("");
        m.Status.Should().Be("ACTIVE");
        (await PolsAsync(PoIdOf(r)))[0].MaterialId.Should().Be(m.MaterialId);
    }

    [Fact]
    public async Task IT_B5_029_Existing_Name_And_Color_Reuses_Material()
    {
        var before = await Snap5();

        var r = await Create(Mk([NewPL(B.Ma3.MaterialName, "cái", "", 10m, 1_000m)]));

        Created(r);
        (await PolsAsync(PoIdOf(r)))[0].MaterialId.Should().Be(B.Ma3.MaterialId);
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().CountAsync(m => m.MaterialName == B.Ma3.MaterialName && m.MaterialType == "TRIM" && m.Color == "")).Should().Be(1);
        (await Snap5()).Counts.Values["Materials"].Should().Be(before.Counts.Values["Materials"]);
    }

    [Theory]
    [InlineData("IT_B5_030", "both")]
    [InlineData("IT_B5_030", "none")]
    public async Task IT_B5_030_Line_Needs_Exactly_One_Of_Existing_Or_New_Material(string caseId, string kind)
    {
        var line = kind == "both" ? NewPL("X " + Rnd(5), "cái", null, 1m, 1m, B.Ma1.MaterialId) : NewPL(null, null, null, 1m, 1m);

        await RejectedPost(Mk([line]), 400);
    }

    [Fact]
    public async Task IT_B5_031_Invalid_Second_Line_Rolls_Back_New_Material()
    {
        var name = "Ren trang trí " + Rnd(5);
        var before = await Snap5();

        var r = await Create(Mk([NewPL(name, "m", null, 5m, 1_000m), PL(B.Ma1.MaterialId, -1m, 1_000m)]));

        Rejected(r, 400);
        (await MaterialByNameAsync(name)).Should().BeNull();
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_032", "name", 200, 201)]
    [InlineData("IT_B5_032", "name", 201, 400)]
    [InlineData("IT_B5_032", "unit", 20, 201)]
    [InlineData("IT_B5_032", "unit", 21, 400)]
    [InlineData("IT_B5_032", "color", 50, 201)]
    [InlineData("IT_B5_032", "color", 51, 400)]
    public async Task IT_B5_032_New_Material_Field_Length_Boundaries(string caseId, string field, int len, int expected)
    {
        var name = field == "name" ? Fill(len) : "NVL " + Rnd(8);
        var unit = field == "unit" ? Fill(len) : "cái";
        var color = field == "color" ? Fill(len) : null;
        var before = await Snap5();

        var r = await Create(Mk([NewPL(name, unit, color, 1m, 1_000m)]));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 201) (await MaterialByNameAsync(name)).Should().NotBeNull();
        else await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_033", "discontinued", 400)]
    [InlineData("IT_B5_033", "missing", 400)]
    [InlineData("IT_B5_033", "zero", 400)]
    public async Task IT_B5_033_Invalid_Supplier_Is_Rejected(string caseId, string kind, int expected)
    {
        var id = kind switch { "discontinued" => B.Su3.SupplierId, "missing" => await MissingSupplierIdAsync(), _ => 0 };
        var before = await Snap5();

        var r = await Create(Mk(supplierId: id));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(kind == "missing" ? new[] { 400, 404 } : new[] { expected }, r.ToString());
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_034", "missing")]
    [InlineData("IT_B5_034", "cancelled")]
    public async Task IT_B5_034_Invalid_Order_Is_Rejected(string caseId, string kind)
    {
        var id = kind == "missing" ? await MissingPoIdAsync() : D.Od3.OrderId;

        await RejectedPost(Mk(orderId: id), 400, 404);
    }

    [Theory]
    [InlineData("IT_B5_035", "missing")]
    [InlineData("IT_B5_035", "discontinued")]
    public async Task IT_B5_035_Invalid_Material_Is_Rejected(string caseId, string kind)
    {
        var id = kind == "missing" ? await MissingMaterialIdAsync() : B.Ma4.MaterialId;

        await RejectedPost(Mk([PL(id, 1m, 1_000m)]), 400, 404);
    }

    [Theory]
    [InlineData("IT_B5_036", "0", 0)]
    [InlineData("IT_B5_036", "-0.001", 0)]
    [InlineData("IT_B5_036", "0.001", 1)]
    [InlineData("IT_B5_036", "999999999999999.999", 1)]
    [InlineData("IT_B5_036", "1000000000000000", 0)]
    [InlineData("IT_B5_036", "1.0005", 2)]
    public async Task IT_B5_036_OrderedQty_Boundaries(string caseId, string qty, int mode)
    {
        var before = await Snap5();

        var r = await Create(Mk([PL(B.Ma1.MaterialId, Dc(qty), 1m)]));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (mode == 1) Created(r);
        else if (mode == 0) { r.Code.Should().Be(400, r.ToString()); await Unchanged(before); }
        else r.Code.Should().BeOneOf(200, 201, 400);
        await using var db = NewDb();
        (await db.PurchaseOrderLines.AsNoTracking().AnyAsync(l => l.OrderedQty <= 0)).Should().BeFalse();
    }

    [Theory]
    [InlineData("IT_B5_037", "-1", 0)]
    [InlineData("IT_B5_037", "0", 2)]
    [InlineData("IT_B5_037", "0.01", 1)]
    [InlineData("IT_B5_037", "9999999999999999.99", 1)]
    [InlineData("IT_B5_037", "10000000000000000", 0)]
    [InlineData("IT_B5_037", "100.555", 2)]
    public async Task IT_B5_037_UnitPrice_Boundaries(string caseId, string price, int mode)
    {
        var before = await Snap5();

        var r = await Create(Mk([PL(B.Ma1.MaterialId, 1m, Dc(price))]));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (mode == 1) Created(r);
        else if (mode == 0) { r.Code.Should().Be(400, r.ToString()); await Unchanged(before); }
        else r.Code.Should().BeOneOf(200, 201, 400);
        await using var db = NewDb();
        (await db.PurchaseOrderLines.AsNoTracking().AnyAsync(l => l.UnitPrice < 0)).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B5_038_Amount_Overflow_Never_Causes_500()
    {
        var r = await Create(Mk([PL(B.Ma1.MaterialId, Dc("999999999999999.999"), Dc("9999999999999999.99"))]));

        r.ShouldNotBeServerError();
        if (r.Code is 200 or 201)
        {
            (await Call("manager", Get, $"{PO}/{PoIdOf(r)}")).ShouldNotBeServerError();
            (await Call("manager", Get, PO)).ShouldNotBeServerError();
            await using var db = NewDb();
            var act = async () => await db.PurchaseOrderLines.AsNoTracking().Select(l => l.Amount).ToListAsync();
            await act.Should().NotThrowAsync();
        }
        else r.Code.Should().Be(400);
    }

    [Theory]
    [InlineData("IT_B5_039", "empty")]
    [InlineData("IT_B5_039", "null")]
    public async Task IT_B5_039_Empty_Lines_Are_Rejected(string caseId, string kind)
    {
        var body = Mk();
        body["Lines"] = kind == "empty" ? Array.Empty<object>() : null;

        await RejectedPost(body, 400);
    }

    [Theory]
    [InlineData("IT_B5_040", "zero")]
    [InlineData("IT_B5_040", "missing")]
    [InlineData("IT_B5_040", "feb30")]
    public async Task IT_B5_040_Invalid_OrderDate_Is_Rejected(string caseId, string kind)
    {
        var body = Mk();
        if (kind == "missing") body.Remove("OrderDate"); else body["OrderDate"] = kind == "zero" ? "0001-01-01" : "2026-02-30";

        await RejectedPost(body, 400);
        await using var db = NewDb();
        (await db.PurchaseOrders.AsNoTracking().AnyAsync(p => p.OrderDate == new DateOnly(1, 1, 1))).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B5_041_Count_Must_Match_Delivery_Dates()
    {
        await RejectedPost(Mk(count: 3, dates: Dates(2)), 400);
    }

    [Theory]
    [InlineData("IT_B5_042", "0", 2)]
    [InlineData("IT_B5_042", "-1", 0)]
    [InlineData("IT_B5_042", "32767", 2)]
    [InlineData("IT_B5_042", "32768", 0)]
    [InlineData("IT_B5_042", "nulldates", 2)]
    public async Task IT_B5_042_ExpectedDeliveryCount_Boundaries(string caseId, string kind, int mode)
    {
        var body = Mk(count: 1);
        if (kind == "nulldates") body["ExpectedDeliveryDates"] = null;
        else { body["ExpectedDeliveryCount"] = int.Parse(kind); body["ExpectedDeliveryDates"] = kind == "0" ? Array.Empty<DateOnly>() : Dates(1); }
        var before = await Snap5();

        var r = await Create(body);

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (mode == 0) { r.Code.Should().Be(400, r.ToString()); await Unchanged(before); }
        else r.Code.Should().BeOneOf(200, 201, 400);
        await using var db = NewDb();
        (await db.DeliveryScheduleDates.AsNoTracking().AnyAsync(d => d.SeqNo < 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("IT_B5_043", "zero")]
    [InlineData("IT_B5_043", "dup")]
    [InlineData("IT_B5_043", "beforeOrderDate")]
    public async Task IT_B5_043_Invalid_Delivery_Dates_Are_Rejected(string caseId, string kind)
    {
        var dates = kind switch
        {
            "zero" => new[] { new DateOnly(1, 1, 1), T.AddDays(7) },
            "dup" => new[] { T.AddDays(7), T.AddDays(7) },
            _ => new[] { T.AddDays(-3), T.AddDays(7) },
        };

        await RejectedPost(Mk(count: 2, dates: dates), 400);
        await using var db = NewDb();
        (await db.DeliveryScheduleDates.AsNoTracking().AnyAsync(d => d.DeliveryDate == new DateOnly(1, 1, 1))).Should().BeFalse();
    }

    [Theory]
    [InlineData("IT_B5_044", 1000, 201)]
    [InlineData("IT_B5_044", 1001, 400)]
    public async Task IT_B5_044_Note_Length(string caseId, int len, int expected)
    {
        var note = new string('ế', len);
        var before = await Snap5();

        var r = await Create(Mk(note: note));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 201) (await PoAsync(PoIdOf(r))).Note.Should().Be(note);
        else await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_045_Mass_Assignment_Fields_Are_Ignored()
    {
        var body = Mk();
        body["PoCode"] = "HACK-01"; body["GoodsStatus"] = "RECEIVED"; body["PaymentStatus"] = "PAID"; body["PaidAmount"] = 1;
        body["SourceRequestId"] = 1; body["CreatedAt"] = "0001-01-01";

        var r = await Create(body);

        Created(r);
        var po = await PoAsync(PoIdOf(r));
        po.PoCode.Should().NotBe("HACK-01");
        po.PaymentStatus.Should().Be("UNPAID");
        po.PaidAmount.Should().BeNull();
        po.SourceRequestId.Should().BeNull();
        po.CreatedAt.Year.Should().BeGreaterThan(2000);
    }

    [Fact]
    public async Task IT_B5_048_Trigger_Violation_Is_Reported_Without_Leaking_Internals()
    {
        // MA2 không thuộc NVL của OD2 → dòng gắn đơn nhưng không khớp OrderMaterial
        var before = await Snap5();

        var r = await Create(Mk([PL(B.Ma2.MaterialId, 5m, 1_000m)], D.Od2.OrderId));

        Rejected(r, 400, 409);
        await Unchanged(before);
    }
}
