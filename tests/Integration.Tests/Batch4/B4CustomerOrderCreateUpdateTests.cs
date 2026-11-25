using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_017–050 — A/customer-orders: mã gợi ý, tạo / sửa đơn, UQ_OrderLine [Đ1], RowVer.</summary>
public sealed class B4CustomerOrderCreateUpdateTests(ITestOutputHelper output) : B4TestBase(output)
{
    private string Pr1 => B.Pr1.ProductId.ToString();

    private async Task<Api> Create(Dictionary<string, object?> body) => await Adm(Post, A + "customer-orders", body);

    [Fact]
    public async Task IT_B4_017_NextCode_Is_Suggestion_Only()
    {
        var before = await Snap();

        var r = await Adm(Get, A + "customer-orders/next-code");

        r.Code.Should().Be(200, r.ToString());
        var code = r.Data.ValueKind == JsonValueKind.String ? r.Data.GetString() : B1Json.Str(r.Data, "code") ?? B1Json.Str(r.Data, "orderCode");
        code.Should().MatchRegex(@"^DH-\d+$");
        (await CountOrdersByCodeAsync(code!)).Should().Be(0, "mã gợi ý chưa được giữ chỗ");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_018_Create_Order_With_Two_Distinct_Variants()
    {
        var last = await LastAuditIdAsync();
        var code = Code();
        var body = OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 100), Ln(B.Pr1.ProductId, "Trắng", "M", 80)]);

        var r = await Create(body);

        Created(r);
        var od = (await OrderByCodeAsync(code))!;
        OrderIdOf(r).Should().Be(od.OrderId);
        r.Data.GetProperty("products").GetArrayLength().Should().Be(0);
        od.Status.Should().Be("NEW");
        od.MaterialStatus.Should().Be("AWAITING");
        od.IsDelayRisk.Should().NotBe(true);
        AssertCreatedNow(od.CreatedAt);
        (await LinesAsync(od.OrderId)).Select(l => l.ProductVariantId).Should().BeEquivalentTo([B.Pr1VarM.ProductVariantId, D.Pv3.ProductVariantId]);
        await AssertAuditAddedAsync(last);
    }

    [Fact]
    public async Task IT_B4_019_Duplicate_Variant_In_Same_Order_Is_Rejected_And_Rolled_Back()
    {
        var before = await Snap();
        var code = Code();

        var r = await Create(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 100), Ln(B.Pr1.ProductId, "Đen", "M", 50)]));

        Rejected(r, 409, 400);
        (await OrderByCodeAsync(code)).Should().BeNull("rollback: không có đơn");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_020_Case_Variant_Duplicate_Never_Produces_Duplicate_OrderLine()
    {
        var code = Code();

        var r = await Create(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 100), Ln(B.Pr1.ProductId, "đen", "m", 50)]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400, 409);
        Note($"'Đen M' + 'đen m' → {r.Code}");
        var od = await OrderByCodeAsync(code);
        if (od is not null)
            (await LinesAsync(od.OrderId)).GroupBy(l => l.ProductVariantId).Should().OnlyContain(g => g.Count() == 1);
    }

    [Fact]
    public async Task IT_B4_021_Update_Adding_Existing_Variant_As_New_Line_Is_Rejected()
    {
        var before = await Snap();
        var body = await UpdateBodyAsync(D.Od1, [.. KeepOd1(), Ln(B.Pr1.ProductId, "Đen", "M", 10)]);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body);

        Rejected(r, 409, 400);
        (await LinesAsync(D.Od1.OrderId)).Should().HaveCount(2);
        (await OrdRowVerAsync(D.Od1.OrderId)).Should().Equal(D.Od1.RowVer);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_022_Update_Changing_Line_To_Existing_Variant_Is_Rejected()
    {
        var before = await Snap();
        var body = await UpdateBodyAsync(D.Od1, [KeepOd1()[0], Ln(B.Pr1.ProductId, "Đen", "M", 50, 25_000m, D.Ol2.OrderLineId)]);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body);

        Rejected(r, 409, 400);
        (await LinesAsync(D.Od1.OrderId)).Single(l => l.OrderLineId == D.Ol2.OrderLineId).ProductVariantId.Should().Be(B.Pr1VarL.ProductVariantId);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_023_Same_Variant_Allowed_In_Different_Orders()
    {
        var code = Code();

        var r = await Create(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 40)]));

        Created(r);
        var od = (await OrderByCodeAsync(code))!;
        (await LinesAsync(od.OrderId)).Single().ProductVariantId.Should().Be(B.Pr1VarM.ProductVariantId);
        (await LinesAsync(D.Od1.OrderId)).Should().Contain(l => l.ProductVariantId == B.Pr1VarM.ProductVariantId);
    }

    [Fact]
    public async Task IT_B4_024_New_Size_Creates_Variant_Or_Is_Rejected()
    {
        var code = Code();

        var r = await Create(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "XL", 10)]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400);
        if (r.Code is 200 or 201)
        {
            await using var db = NewDb();
            var v = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.ProductId == B.Pr1.ProductId && x.SizeCode == "XL" && x.ProductColorId == B.Pr1Black.ProductColorId);
            v.Status.Should().Be("ACTIVE");
            (await LinesAsync((await OrderByCodeAsync(code))!.OrderId)).Single().ProductVariantId.Should().Be(v.ProductVariantId);
        }
        else (await OrderByCodeAsync(code)).Should().BeNull();
    }

    [Fact]
    public async Task IT_B4_025_Staged_Product_Is_Created_In_Same_Transaction()
    {
        var code = Code();
        var pcode = "SPB4" + Guid.NewGuid().ToString("N")[..6];
        var body = OrderBody(code, [Ln(-1, "Đỏ", "S", 10, 30_000m)]);
        body["StagedProducts"] = new[] { new { TempId = -1, ProductCode = pcode, SampleImageUrl = (string?)null, Description = (string?)null, ExtraImages = (string[]?)null } };

        var r = await Create(body);

        Created(r);
        var map = r.Data.GetProperty("products").EnumerateArray().Single();
        B1Json.Int(map, "tempId").Should().Be(-1);
        B1Json.Str(map, "productCode").Should().Be(pcode);
        var pid = B1Json.Int(map, "productId")!.Value;
        pid.Should().BeGreaterThan(0);
        await using var db = NewDb();
        (await db.Products.AsNoTracking().SingleAsync(p => p.ProductCode == pcode)).ProductId.Should().Be(pid);
        var color = await db.ProductColors.AsNoTracking().SingleAsync(c => c.ProductId == pid && c.Color == "Đỏ");
        var variant = await db.ProductVariants.AsNoTracking().SingleAsync(v => v.ProductId == pid && v.ProductColorId == color.ProductColorId && v.SizeCode == "S");
        var line = (await LinesAsync((await OrderByCodeAsync(code))!.OrderId)).Single();
        line.ProductId.Should().Be(pid);
        line.ProductVariantId.Should().Be(variant.ProductVariantId);
    }

    [Fact]
    public async Task IT_B4_026_Staged_Product_With_Unmatched_TempId_Rolls_Back()
    {
        var before = await Snap();
        var code = Code();
        var pcode = "SPB4" + Guid.NewGuid().ToString("N")[..6];
        var body = OrderBody(code, [Ln(-2, "Đỏ", "S", 10)]);
        body["StagedProducts"] = new[] { new { TempId = -1, ProductCode = pcode, SampleImageUrl = (string?)null, Description = (string?)null, ExtraImages = (string[]?)null } };

        var r = await Create(body);

        Rejected(r, 400);
        (await ProductByCode(pcode)).Should().BeNull();
        (await OrderByCodeAsync(code)).Should().BeNull();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_027_Staged_Product_With_Existing_Code_Rolls_Back()
    {
        var before = await Snap();
        var code = Code();
        var body = OrderBody(code, [Ln(-1, "Đỏ", "S", 10)]);
        body["StagedProducts"] = new[] { new { TempId = -1, ProductCode = B.Pr1.ProductCode, SampleImageUrl = (string?)null, Description = (string?)null, ExtraImages = (string[]?)null } };

        var r = await Create(body);

        Rejected(r, 409, 400);
        (await OrderByCodeAsync(code)).Should().BeNull();
        await using (var db = NewDb()) (await db.Products.CountAsync(p => p.ProductCode == B.Pr1.ProductCode)).Should().Be(1);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_028_Staged_Product_Is_Not_Saved_When_A_Line_Is_Invalid()
    {
        var before = await Snap();
        var code = Code();
        var pcode = "SPB4" + Guid.NewGuid().ToString("N")[..6];
        var body = OrderBody(code, [Ln(-1, "Đỏ", "S", 0)]);
        body["StagedProducts"] = new[] { new { TempId = -1, ProductCode = pcode, SampleImageUrl = (string?)null, Description = (string?)null, ExtraImages = (string[]?)null } };

        var r = await Create(body);

        Rejected(r, 400);
        (await ProductByCode(pcode)).Should().BeNull("rollback toàn bộ");
        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_029", false)]
    [InlineData("IT_B4_029", true)]
    public async Task IT_B4_029_Duplicate_OrderCode_Is_Rejected(string caseId, bool lower)
    {
        var before = await Snap();
        var dup = lower ? D.Od1.OrderCode.ToLowerInvariant() : D.Od1.OrderCode;

        var r = await Create(OrderBody(dup));

        if (!lower) Rejected(r, 409);
        else { r.ShouldNotBeServerError(); Note($"'{dup}' → {r.Code} (collation)"); r.Code.Should().BeOneOf(409, 400, 200, 201); }
        if (!lower) await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_032", "OrderCode", 30, true)]
    [InlineData("IT_B4_032", "OrderCode", 31, false)]
    [InlineData("IT_B4_032", "DeliveryAddress", 500, true)]
    [InlineData("IT_B4_032", "DeliveryAddress", 501, false)]
    [InlineData("IT_B4_032", "Note", 1000, true)]
    [InlineData("IT_B4_032", "Note", 1001, false)]
    public async Task IT_B4_032_Text_Length_Boundaries(string caseId, string field, int length, bool ok)
    {
        var code = field == "OrderCode" ? "DH" + new string('Z', length - 2) : Code();
        var body = OrderBody(code);
        var text = field == "OrderCode" ? code : Viet(length);
        body[field] = text;

        var r = await Create(body);

        if (ok)
        {
            Created(r);
            var od = (await OrderByCodeAsync(code))!;
            (field switch { "OrderCode" => od.OrderCode, "DeliveryAddress" => od.DeliveryAddress, _ => od.Note }).Should().Be(text);
        }
        else
        {
            Rejected(r, 400);
            (await OrderByCodeAsync(code)).Should().BeNull();
        }
    }

    [Fact]
    public async Task IT_B4_033_Missing_Required_Fields_Return_400()
    {
        var before = await Snap();
        var cases = new List<Dictionary<string, object?>>();
        var a = OrderBody(Code()); a.Remove("DeliveryAddress"); cases.Add(a);
        var b = OrderBody(Code()); b["DeliveryAddress"] = ""; cases.Add(b);
        var c = OrderBody(Code()); c.Remove("OrderCode"); cases.Add(c);
        var d = OrderBody(Code(), []); cases.Add(d);

        foreach (var body in cases) Rejected(await Create(body), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_034_Unknown_Or_Discontinued_Customer_Is_Rejected()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await Create(OrderBody(Code(), customerId: missing)), 400, 404);
        Rejected(await Create(OrderBody(Code(), customerId: B.Cu3.CustomerId)), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_035_Invalid_Dates_Return_400()
    {
        var before = await Snap();
        var a = OrderBody(Code()); a["ReceivedDate"] = DateOnly.MinValue;
        var b = OrderBody(Code()); b.Remove("DueDate");
        var c = OrderBody(Code()); c["DueDate"] = Today.AddDays(-5);

        foreach (var body in new[] { a, b, c }) Rejected(await Create(body), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_036_DueDate_Equal_Or_Before_ReceivedDate_Is_Handled()
    {
        var same = OrderBody(Code()); same["DueDate"] = same["ReceivedDate"];
        var past = OrderBody(Code()); past["ReceivedDate"] = Today.AddDays(-10); past["DueDate"] = Today.AddDays(-1);

        var r1 = await Create(same);
        var r2 = await Create(past);

        r1.ShouldNotBeServerError(); r2.ShouldNotBeServerError();
        r1.Code.Should().BeOneOf(200, 201, 400);
        r2.Code.Should().BeOneOf(200, 201, 400);
        Note($"DueDate = ReceivedDate → {r1.Code}; DueDate quá khứ → {r2.Code}");
        (await OrderByCodeAsync((string)same["OrderCode"]!) is not null).Should().Be(r1.Code is 200 or 201);
        (await OrderByCodeAsync((string)past["OrderCode"]!) is not null).Should().Be(r2.Code is 200 or 201);
    }

    [Theory]
    [InlineData("IT_B4_037", "0", false)]
    [InlineData("IT_B4_037", "-1", false)]
    [InlineData("IT_B4_037", "1", true)]
    [InlineData("IT_B4_037", "2147483647", true)]
    [InlineData("IT_B4_037", "2147483648", false)]
    public async Task IT_B4_037_OrderedQty_Boundaries(string caseId, string qty, bool ok)
    {
        var code = Code();
        var raw = JsonSerializer.Serialize(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 987654321)])).Replace("987654321", qty);

        var (res, json) = await SendAsAsync("admin", Post, A + "customer-orders", raw);
        using (res)
        {
            var api = new Api { Status = res.StatusCode, Body = await res.Content.ReadAsStringAsync(), Root = json ?? default, IsJson = json is not null };
            if (ok) Created(api); else Rejected(api, 400);
        }

        var od = await OrderByCodeAsync(code);
        if (ok) (await LinesAsync(od!.OrderId)).Single().OrderedQty.Should().Be(int.Parse(qty));
        else od.Should().BeNull();
    }

    [Theory]
    [InlineData("IT_B4_038", "-1", "400")]
    [InlineData("IT_B4_038", "0", "400|201")]
    [InlineData("IT_B4_038", "0.01", "201")]
    [InlineData("IT_B4_038", "9999999999999999.99", "201")]
    [InlineData("IT_B4_038", "10000000000000000", "400")]
    [InlineData("IT_B4_038", "100.555", "400|201")]
    public async Task IT_B4_038_CmtUnitPrice_Boundaries(string caseId, string price, string expected)
    {
        var code = Code();
        var raw = JsonSerializer.Serialize(OrderBody(code, [Ln(B.Pr1.ProductId, "Đen", "M", 5, 123456789m)])).Replace("123456789", price);

        var (res, json) = await SendAsAsync("admin", Post, A + "customer-orders", raw);
        using (res)
        {
            var api = new Api { Status = res.StatusCode, Body = await res.Content.ReadAsStringAsync(), Root = json ?? default, IsJson = json is not null };
            api.ShouldNotBeServerError();
            var allowed = expected.Split('|').Select(int.Parse).SelectMany(c => c == 201 ? new[] { 200, 201 } : [c]).ToArray();
            api.Code.Should().BeOneOf(allowed, api.ToString());
            api.Body.Should().NotContainAny(Leaks);
            var od = await OrderByCodeAsync(code);
            (od is not null).Should().Be(api.Code is 200 or 201);
            if (od is not null) (await LinesAsync(od.OrderId)).Single().CmtUnitPrice.Should().BeInRange(0m, 9_999_999_999_999_999.99m);
        }
    }

    [Fact]
    public async Task IT_B4_039_Color_From_Other_Product_Does_Not_Cause_FK_Error()
    {
        var code = Code();

        var r = await Create(OrderBody(code, [Ln(B.Pr1.ProductId, "Xanh", "M", 5)]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400);
        r.Body.Should().NotContainAny(Leaks);
        await using var db = NewDb();
        (await db.OrderLines.AsNoTracking().CountAsync(l => l.ProductVariantId == D.Pv9.ProductVariantId && l.ProductId == B.Pr1.ProductId)).Should().Be(0);
    }

    [Fact]
    public async Task IT_B4_040_Unknown_Or_Discontinued_Product_Is_Rejected()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await Create(OrderBody(Code(), [Ln(missing, "Đen", "M", 5)])), 404, 400);
        Rejected(await Create(OrderBody(Code(), [Ln(B.Pr3.ProductId, "Đen", "M", 5)])), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_041_Mass_Assignment_Fields_Are_Ignored()
    {
        var code = Code();
        var body = OrderBody(code);
        body["Status"] = "IN_PRODUCTION"; body["MaterialStatus"] = "RECEIVED"; body["FinalAmount"] = 1; body["IsDelayRisk"] = true;
        body["CreatedAt"] = DateTime.MinValue;

        var r = await Create(body);

        Created(r);
        var od = (await OrderByCodeAsync(code))!;
        od.Status.Should().Be("NEW");
        od.MaterialStatus.Should().Be("AWAITING");
        od.FinalAmount.Should().BeNull();
        od.DelayRiskSince.Should().BeNull();
        AssertCreatedNow(od.CreatedAt);
    }

    [Fact]
    public async Task IT_B4_042_Update_Order_Changes_Rows_RowVer_And_Audit()
    {
        var last = await LastAuditIdAsync();
        var r1 = D.Od1.RowVer;
        var body = await UpdateBodyAsync(D.Od1, [Ln(B.Pr1.ProductId, "Đen", "M", 120, 25_000m, D.Ol1.OrderLineId), KeepOd1()[1], Ln(B.Pr1.ProductId, "Trắng", "M", 30)]);
        body["DueDate"] = Today.AddDays(40);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body);

        r.Code.Should().Be(200, r.ToString());
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.RowVer.Should().NotEqual(r1);
        od.UpdatedAt.Should().NotBeNull();
        od.DueDate.Should().Be(Today.AddDays(40));
        var lines = await LinesAsync(D.Od1.OrderId);
        lines.Single(l => l.OrderLineId == D.Ol1.OrderLineId).OrderedQty.Should().Be(120);
        lines.Should().HaveCount(3).And.Contain(l => l.ProductVariantId == D.Pv3.ProductVariantId);
        await AssertAuditAddedAsync(last);
    }

    [Fact]
    public async Task IT_B4_043_Stale_RowVer_Returns_409()
    {
        var r1 = D.Od1.RowVer;
        var first = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", await UpdateBodyAsync(D.Od1, KeepOd1()));
        first.Code.Should().Be(200, first.ToString());
        var before = await Snap();
        var stale = await UpdateBodyAsync(D.Od1, [.. KeepOd1(), Ln(B.Pr1.ProductId, "Trắng", "M", 30)], r1);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", stale);

        Rejected(r, 409);
        (await LinesAsync(D.Od1.OrderId)).Should().HaveCount(2);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_044_Missing_Or_Malformed_RowVer_Is_Rejected()
    {
        var before = await Snap();
        var none = await UpdateBodyAsync(D.Od1, KeepOd1()); none.Remove("RowVer");
        var bad = await UpdateBodyAsync(D.Od1, KeepOd1()); bad["RowVer"] = "không-phải-base64";
        var empty = await UpdateBodyAsync(D.Od1, KeepOd1()); empty["RowVer"] = "";

        foreach (var body in new[] { none, bad, empty })
            Rejected(await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body), 400, 409);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_046_Removing_Line_Of_New_Order_Cascades_Team_Rows()
    {
        await using (var db = NewDb())
        {
            db.OrderLineTeams.Add(new NX_MTD.Domain.Entities.OrderLineTeam { OrderLineId = D.Ol2.OrderLineId, SewingTeamId = S0.TSew02.TeamId, AssignedAt = Clock.Now });
            await db.SaveChangesAsync();
        }

        var body = await UpdateBodyAsync(D.Od1, [KeepOd1()[0]]);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body);

        r.Code.Should().Be(200, r.ToString());
        var lines = await LinesAsync(D.Od1.OrderId);
        lines.Should().ContainSingle(l => l.OrderLineId == D.Ol1.OrderLineId);
        (await TeamIdsAsync(D.Ol2.OrderLineId)).Should().BeEmpty("ON DELETE CASCADE");
    }

    [Fact]
    public async Task IT_B4_047_Cannot_Remove_Line_Of_Order_In_Production()
    {
        var before = await Snap();
        var body = await UpdateBodyAsync(D.Od2, [Ln(B.Pr1.ProductId, "Đen", "M", 5)]);

        var r = await Adm(Put, A + $"customer-orders/{D.Od2.OrderId}", body);

        Rejected(r, 400, 409);
        (await LinesAsync(D.Od2.OrderId)).Should().ContainSingle(l => l.OrderLineId == D.Ol3.OrderLineId);
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_048_Update_With_Line_Of_Another_Order_Is_Rejected()
    {
        var before = await Snap();
        var body = await UpdateBodyAsync(D.Od1, [.. KeepOd1(), Ln(B.Pr2.ProductId, "Xanh", "M", 99, 25_000m, D.Ol9.OrderLineId)]);

        var r = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", body);

        Rejected(r, 400, 404);
        var ol9 = (await LinesAsync(D.Od4.OrderId)).Single();
        ol9.OrderLineId.Should().Be(D.Ol9.OrderLineId);
        ol9.OrderedQty.Should().Be(D.Ol9.OrderedQty);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_049_Closed_Orders_Cannot_Be_Updated()
    {
        var before = await Snap();
        foreach (var od in new[] { D.Od3, D.Od5 })
        {
            var cur = (await LinesAsync(od.OrderId)).Select(l => Ln(B.Pr1.ProductId, "Đen", "M", l.OrderedQty, l.CmtUnitPrice, l.OrderLineId)).ToArray();
            if (cur.Length == 0) cur = [Ln(B.Pr1.ProductId, "Đen", "M", 5)];
            Rejected(await Adm(Put, A + $"customer-orders/{od.OrderId}", await UpdateBodyAsync(od, cur)), 400, 409);
        }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_050_Unknown_Id_And_Non_Integer_Route_Return_404()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await Adm(Put, A + $"customer-orders/{missing}", await UpdateBodyAsync(D.Od1, KeepOd1())), 404);
        Rejected(await Adm(Get, A + "customer-orders/abc"), 404);

        await Same(before);
    }

    private async Task<NX_MTD.Domain.Entities.Product?> ProductByCode(string code)
    {
        await using var db = NewDb();
        return await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductCode == code);
    }
}

