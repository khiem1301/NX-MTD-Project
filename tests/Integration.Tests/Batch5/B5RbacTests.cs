using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

public sealed class B5RbacTests(ITestOutputHelper output) : B5TestBase(output)
{
    [Fact]
    public async Task IT_B5_001_Anonymous_Gets_401_On_All_Prefixes()
    {
        var before = await Snap5();
        foreach (var url in new[] { PO, WKP, PAY, SUP })
        {
            var (res, _) = await SendAsAsync(null, Get, url);
            using (res) await AssertErrorAsync(res, 401);
        }

        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_002", "expired")]
    [InlineData("IT_B5_002", "forged")]
    public async Task IT_B5_002_Expired_Or_Forged_Token_Gets_401(string caseId, string kind)
    {
        var token = kind == "expired" ? await TkExpired() : await TkForged();
        var rv = await RvAsync(P.Po2.PurchaseOrderId);
        var before = await Snap5();

        var r = await SendAsync(Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm", token, Conf(rv));

        r.Code.Should().Be(401, r.ToString());
        (await PaysAsync(P.Po2.PurchaseOrderId)).Should().BeEmpty();
        (await PoAsync(P.Po2.PurchaseOrderId)).PaymentStatus.Should().Be("UNPAID");
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_003_Admin_Cannot_Read_Accountant_Prefix()
    {
        await Deny("admin", [403], (Get, PAY), (Get, $"{PAY}/{P.Po2.PurchaseOrderId}"));
    }

    [Fact]
    public async Task IT_B5_004_Admin_Cannot_Write_Accountant_Prefix()
    {
        var rv = await RvAsync(P.Po2.PurchaseOrderId);
        var before = await Snap5();

        var a = await Call("admin", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm", Conf(rv, 9_600_000m));
        var b = await Call("admin", Post, $"{PAY}/{P.Po3.PurchaseOrderId}/return", Reason(await RvAsync(P.Po3.PurchaseOrderId), "x"));
        var c = await Call("admin", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp2.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)));

        new[] { a, b, c }.Should().OnlyContain(x => x.Code == 403, $"{a} | {b} | {c}");
        (await PaysAsync(P.Po2.PurchaseOrderId)).Should().BeEmpty();
        (await PayAsync(P.Pp2.PurchasePaymentId)).Amount.Should().Be(3_000_000m);
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_005_Admin_Cannot_Use_WarehouseKeeper_Prefix()
    {
        await Deny("admin", [403], (Get, WKP), (Get, $"{WKP}/{P.Po2.PurchaseOrderId}"));
    }

    [Fact]
    public async Task IT_B5_006_Admin_Can_Use_Manager_Prefix()
    {
        var list = await Call("admin", Get, PO);
        var create = await Create(Mk(), "admin");
        var place = await Call("admin", Post, $"{TRK}/{P.Po1.PurchaseOrderId}/place", PlaceBody(await RvAsync(P.Po1.PurchaseOrderId), Po1Lines()));

        list.Code.Should().Be(200, list.ToString());
        create.Code.Should().BeOneOf(200, 201);
        place.Code.Should().Be(200, place.ToString());
        (await PoAsync(PoIdOf(create))).SupplierId.Should().Be(B.Su1.SupplierId);
        (await PoAsync(P.Po1.PurchaseOrderId)).GoodsStatus.Should().Be("ORDERED");
    }

    [Fact]
    public async Task IT_B5_007_Manager_Cannot_Use_Other_Prefixes()
    {
        await Deny("manager", [403], (Get, PAY), (Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm"), (Get, WKP), (Get, SUP));
    }

    [Fact]
    public async Task IT_B5_008_Accountant_Cannot_Order_Or_Reprice()
    {
        await Deny("accountant", [403], (Get, PO), (Get, $"{TRK}/{P.Po1.PurchaseOrderId}"), (Post, $"{TRK}/{P.Po1.PurchaseOrderId}/reprice"), (Get, WKP));
    }

    [Fact]
    public async Task IT_B5_009_WarehouseKeeper_Reads_Same_Data_As_Manager()
    {
        var before = await Snap5();

        var list = await Call("warehouse", Get, WKP);
        var wk = await Call("warehouse", Get, $"{WKP}/{P.Po2.PurchaseOrderId}");
        var mgr = await Call("manager", Get, $"{PO}/{P.Po2.PurchaseOrderId}");

        list.Code.Should().Be(200, list.ToString());
        wk.Code.Should().Be(200, wk.ToString());
        mgr.Code.Should().Be(200, mgr.ToString());
        wk.Data.GetRawText().Should().Be(mgr.Data.GetRawText());
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_010_WarehouseKeeper_Is_Read_Only()
    {
        var before = await Snap5();

        var a = await Call("warehouse", Post, WKP, Mk());
        var b = await Call("warehouse", Post, PO, Mk());
        var c = await Call("warehouse", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm", Conf(await RvAsync(P.Po2.PurchaseOrderId)));

        a.Code.Should().BeOneOf(404, 405);
        b.Code.Should().Be(403, b.ToString());
        c.Code.Should().Be(403, c.ToString());
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_011", "teamleader")]
    [InlineData("IT_B5_011", "qaqc")]
    [InlineData("IT_B5_011", "worker")]
    public async Task IT_B5_011_Production_Roles_Are_Forbidden_Everywhere(string caseId, string who)
    {
        await Deny(who, [403], (Get, PO), (Get, WKP), (Get, PAY), (Get, M + "suppliers"));
    }

    [Fact]
    public async Task IT_B5_012_No_Update_Or_Delete_Routes()
    {
        var before = await Snap5();

        var put = await Call("manager", Put, $"{PO}/{P.Po1.PurchaseOrderId}", Mk());
        var del = await Call("manager", Del, $"{PO}/{P.Po1.PurchaseOrderId}");
        var delPay = await Call("accountant", Del, PayUrl(P.Po3.PurchaseOrderId, P.Pp2.PurchasePaymentId));

        put.Code.Should().BeOneOf(404, 405);
        del.Code.Should().BeOneOf(404, 405);
        delPay.Code.Should().BeOneOf(404, 405);
        (await PoAsync(P.Po1.PurchaseOrderId)).PoCode.Should().Be("PO-B5-01");
        (await PayAsync(P.Pp2.PurchasePaymentId)).Amount.Should().Be(3_000_000m);
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_013_Route_Casing_And_Kebab()
    {
        var before = await Snap5();

        var upper = await Call("admin", Get, "/api/v1/MANAGER/purchaseorders");
        var kebab = await Call("admin", Get, "/api/v1/manager/purchase-orders");

        upper.Code.Should().Be(200, upper.ToString());
        kebab.Code.Should().BeOneOf(200, 404);
        Note($"kebab route -> {kebab.Code}");
        await Unchanged(before);
    }
}
