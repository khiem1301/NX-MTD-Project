using FluentAssertions;
using Integration.Tests.Batch1;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_001–016 — ma trận phân quyền chéo Admin / Manager / WarehouseKeeper [Đ5].</summary>
public sealed class B4RbacTests(ITestOutputHelper output) : B4TestBase(output)
{
    [Fact]
    public async Task IT_B4_001_Anonymous_Gets_401_On_All_Prefixes()
    {
        var before = await Snap();
        foreach (var url in new[] { A + "customer-orders", M + "order-line-teams", WK + $"order-materials?orderId={D.Od2.OrderId}", C + "order-lookups/orders" })
        {
            var (res, _) = await SendAsAsync(null, Get, url);
            using (res) await AssertErrorAsync(res, 401);
        }

        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_002", "expired")]
    [InlineData("IT_B4_002", "forged")]
    public async Task IT_B4_002_Expired_Or_Forged_Token_Gets_401(string caseId, string kind)
    {
        var token = kind == "expired" ? await TkExpired() : await TkForged();
        var r1 = D.Od1.RowVer;
        var before = await Snap();

        var r = await SendAsync(Post, A + $"customer-orders/{D.Od1.OrderId}/start-production", token, new { RowVer = r1 });

        r.Code.Should().Be(401, r.ToString());
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.Status.Should().Be("NEW");
        od.RowVer.Should().Equal(r1);
        await Same(before);
    }

    [Fact]
    public Task IT_B4_003_Admin_Cannot_Call_WarehouseKeeper_Prefix() =>
        DenyAsync("admin", [403], (Get, WK + $"order-materials?orderId={D.Od2.OrderId}"));

    [Fact]
    public async Task IT_B4_004_Admin_Cannot_Write_Through_WarehouseKeeper_Prefix()
    {
        var before = await Snap();
        foreach (var (m, url) in new[] { (Post, WK + "order-materials"), (Del, WK + $"order-materials/{D.Om2.OrderMaterialId}") })
        {
            var (res, _) = await SendAsAsync("admin", m, url, m == Get ? null : new { });
            using (res) ((int)res.StatusCode).Should().BeOneOf(403, 404, 405);
        }

        (await OmsAsync(D.Od2.OrderId)).Select(o => o.OrderMaterialId).Should().Contain(D.Om2.OrderMaterialId);
        await Same(before);
    }

    [Fact]
    public Task IT_B4_005_Manager_Has_No_PreviewData_Route() =>
        DenyAsync("manager", [404, 405], (Post, M + $"order-materials/import/preview-data?orderId={D.Od1.OrderId}"));

    [Fact]
    public Task IT_B4_006_Manager_Cannot_Import_Through_Admin_Prefix() => DenyAsync("manager", [403],
        (Post, A + $"order-materials/import/preview-data?orderId={D.Od1.OrderId}"),
        (Post, A + $"order-materials/import-data?orderId={D.Od1.OrderId}"),
        (Post, A + $"order-materials/import?orderId={D.Od1.OrderId}"));

    [Fact]
    public async Task IT_B4_007_Manager_Can_ImportData_On_Manager_Prefix()
    {
        var r = await Mgr(Post, M + $"order-materials/import-data?orderId={D.Od1.OrderId}", new[] { OmRow(1, B.Ma1.MaterialCode, 10m, 0m) });

        r.Code.Should().Be(200, r.ToString());
        var oms = await OmsAsync(D.Od1.OrderId);
        oms.Should().ContainSingle(o => o.MaterialId == B.Ma1.MaterialId && o.RequiredQty == 10m);
    }

    [Fact]
    public Task IT_B4_008_Manager_Cannot_Use_Admin_CustomerOrders() => DenyAsync("manager", [403],
        (Get, A + "customer-orders"), (Post, A + "customer-orders"), (Post, A + $"customer-orders/{D.Od1.OrderId}/cancel"));

    [Fact]
    public async Task IT_B4_009_Admin_Can_Call_Manager_Prefix()
    {
        var list = await Adm(Get, M + "customer-orders");
        var team = await Adm(Put, M + "order-line-teams", new { OrderLineId = D.Ol1.OrderLineId, TeamIds = new[] { S0.TSew01.TeamId } });
        var marker = await Adm(Put, MarkersUrl(D.Od1), SaveMarkers(MarkerIn("S1", 1, 6.00m, 1.50m)));

        list.Code.Should().Be(200, list.ToString());
        team.Code.Should().Be(200, team.ToString());
        marker.Code.Should().Be(200, marker.ToString());
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        (await MarkersAsync(D.Od1.OrderId, B.Pr1.ProductId)).Should().ContainSingle(m => m.MarkerCode == "S1");
    }

    [Fact]
    public async Task IT_B4_010_Warehouse_Keeper_Reads_Order_Materials()
    {
        var before = await Snap();

        var r = await Call("warehouse", Get, WK + $"order-materials?orderId={D.Od2.OrderId}");

        r.Code.Should().Be(200, r.ToString());
        IdSet(r, "orderMaterialId").Should().BeEquivalentTo([D.Om1.OrderMaterialId, D.Om2.OrderMaterialId]);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_011_Warehouse_Keeper_Only_Has_Single_Get_Route()
    {
        var before = await Snap();
        foreach (var (m, url) in new[] { (Post, WK + "order-materials"), (Get, WK + "order-materials/paged") })
        {
            var (res, _) = await SendAsAsync("warehouse", m, url, m == Get ? null : new { });
            using (res) ((int)res.StatusCode).Should().BeOneOf(404, 405);
        }

        foreach (var url in new[] { M + $"order-materials?orderId={D.Od2.OrderId}", A + $"order-materials?orderId={D.Od2.OrderId}" })
        {
            var (res, _) = await SendAsAsync("warehouse", Get, url);
            using (res) await AssertErrorAsync(res, 403);
        }

        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_012", "accountant")]
    [InlineData("IT_B4_012", "teamleader")]
    [InlineData("IT_B4_012", "qaqc")]
    [InlineData("IT_B4_012", "electrician")]
    public async Task IT_B4_012_Other_Roles_Forbidden_On_Manager_And_WarehouseKeeper(string caseId, string who)
    {
        var before = await Snap();
        foreach (var url in new[] { M + "customer-orders", M + "order-line-teams", M + $"order-markers/{D.Od2.OrderId}", WK + $"order-materials?orderId={D.Od2.OrderId}" })
        {
            var r = await CallAs(who, Get, url);
            await AssertErrorAsync(Wrap(r), 403);
        }

        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_013", "accountant")]
    [InlineData("IT_B4_013", "warehouse")]
    [InlineData("IT_B4_013", "teamleader")]
    [InlineData("IT_B4_013", "teamdeputy")]
    [InlineData("IT_B4_013", "qaqc")]
    [InlineData("IT_B4_013", "electrician")]
    public async Task IT_B4_013_StaffExceptWorker_Can_Use_OrderLookups(string caseId, string who)
    {
        var before = await Snap();
        foreach (var url in new[] { C + "order-lookups/orders", C + "order-lookups/products" })
        {
            var r = await CallAs(who, Get, url);
            r.Code.Should().Be(200, $"{who} {url}: {r}");
        }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_014_Worker_Forbidden_On_OrderLookups()
    {
        var before = await Snap();
        foreach (var url in new[]
                 {
                     C + "order-lookups/orders", C + $"order-lookups/orders/{D.Od2.OrderId}/lines", C + "order-lookups/products", C + "order-lookups/product-colors",
                 })
        {
            var r = await Call("worker", Get, url);
            r.Code.Should().Be(403, $"{url}: {r}");
        }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_015_No_Delete_Route_For_CustomerOrders()
    {
        var before = await Snap();

        var r = await Adm(Del, A + $"customer-orders/{D.Od1.OrderId}");

        r.Code.Should().BeOneOf(404, 405);
        (await OrderAsync(D.Od1.OrderId)).Should().NotBeNull();
        (await LinesAsync(D.Od1.OrderId)).Select(l => l.OrderLineId).Should().Contain([D.Ol1.OrderLineId, D.Ol2.OrderLineId]);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_016_Route_Is_Case_Insensitive()
    {
        var before = await Snap();

        var r = await Adm(Get, "/api/v1/ADMIN/CUSTOMER-ORDERS");

        r.Code.Should().Be(200, r.ToString());
        await Same(before);
    }

    private static HttpResponseMessage Wrap(Api r) => new((System.Net.HttpStatusCode)r.Code) { Content = new StringContent(r.Body) };
}

