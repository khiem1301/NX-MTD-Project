using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_051–063 — A/customer-orders: start-production / cancel / stop, RowVer, Status HOA [Đ3].</summary>
public sealed class B4CustomerOrderStateTests(ITestOutputHelper output) : B4TestBase(output)
{
    private string Act(CustomerOrder od, string action) => A + $"customer-orders/{od.OrderId}/{action}";

    /// <summary>Đưa OD1 về trạng thái "đã có NVL": có OrderMaterial và MaterialStatus = PARTIAL.</summary>
    private async Task MakeOd1ReadyAsync()
    {
        await using var db = NewDb();
        db.OrderMaterials.Add(new OrderMaterial { OrderId = D.Od1.OrderId, MaterialId = B.Ma1.MaterialId, RequiredQty = 10m, CustomerQty = 0m });
        var od = await db.CustomerOrders.SingleAsync(o => o.OrderId == D.Od1.OrderId);
        od.MaterialStatus = OrderMaterialStatuses.Partial;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IT_B4_051_StartProduction_Blocked_While_Material_Awaiting()
    {
        var before = await Snap();

        var r = await Adm(Post, Act(D.Od1, "start-production"), new { RowVer = D.Od1.RowVer });

        Rejected(r, 400, 409);
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.Status.Should().Be("NEW");
        od.RowVer.Should().Equal(D.Od1.RowVer);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_052_StartProduction_Stores_Upper_Case_Status()
    {
        await MakeOd1ReadyAsync();
        var rv = await OrdRowVerAsync(D.Od1.OrderId);
        var last = await LastAuditIdAsync();

        var r = await Adm(Post, Act(D.Od1, "start-production"), new { RowVer = rv });

        r.Code.Should().Be(200, r.ToString());
        Note($"Response data: {r.Data}");
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.Status.Should().Be(OrderStatuses.InProduction).And.NotBe("InProduction");
        od.RowVer.Should().NotEqual(rv);
        await AssertAuditAddedAsync(last);
    }

    [Fact]
    public async Task IT_B4_053_StartProduction_Twice_Is_Rejected()
    {
        var before = await Snap();

        var r = await Adm(Post, Act(D.Od2, "start-production"), new { RowVer = await OrdRowVerAsync(D.Od2.OrderId) });

        Rejected(r, 400, 409);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_054_Cancel_Stores_Upper_Case_Status_And_Reason()
    {
        var r1 = D.Od1.RowVer;

        var r = await Adm(Post, Act(D.Od1, "cancel"), new { Reason = "Khách hủy", RowVer = r1 });

        r.Code.Should().Be(200, r.ToString());
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.Status.Should().Be(OrderStatuses.Cancelled);
        od.RowVer.Should().NotEqual(r1);
        Note($"StopReason sau cancel = '{od.StopReason}'");
    }

    [Fact]
    public async Task IT_B4_055_Cancel_With_Null_Reason_Is_Allowed()
    {
        var r = await Adm(Post, Act(D.Od1, "cancel"), new { Reason = (string?)null, RowVer = D.Od1.RowVer });

        r.Code.Should().Be(200, r.ToString());
        var od = (await OrderAsync(D.Od1.OrderId))!;
        od.Status.Should().Be(OrderStatuses.Cancelled);
        od.StopReason.Should().BeNull();
    }

    [Fact]
    public async Task IT_B4_056_Cancel_In_Production_Or_Already_Cancelled()
    {
        var inProd = await Adm(Post, Act(D.Od2, "cancel"), new { Reason = "Hủy", RowVer = D.Od2.RowVer });
        inProd.ShouldNotBeServerError();
        inProd.Code.Should().BeOneOf(200, 400, 409);
        Note($"cancel đơn đang sản xuất → {inProd.Code}");
        var before = await Snap();

        var r = await Adm(Post, Act(D.Od3, "cancel"), new { Reason = "Hủy", RowVer = await OrdRowVerAsync(D.Od3.OrderId) });

        Rejected(r, 400, 409);
        (await OrderAsync(D.Od3.OrderId))!.Status.Should().Be(OrderStatuses.Cancelled);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_057_Stop_Stores_Upper_Case_Status_And_Keeps_Team_And_Marker()
    {
        var r = await Adm(Post, Act(D.Od2, "stop"), new { Reason = "Thiếu vải", RowVer = D.Od2.RowVer });

        r.Code.Should().Be(200, r.ToString());
        var od = (await OrderAsync(D.Od2.OrderId))!;
        od.Status.Should().Be(OrderStatuses.Stopped);
        od.StopReason.Should().Be("Thiếu vải");
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        (await MarkersAsync(D.Od2.OrderId, B.Pr1.ProductId)).Should().ContainSingle(m => m.ProductMarkerId == D.Mk1.ProductMarkerId);
    }

    [Fact]
    public async Task IT_B4_058_Stop_Requires_Reason()
    {
        var before = await Snap();
        var rv = D.Od2.RowVer;
        var bodies = new object[] { new { Reason = "", RowVer = rv }, new { Reason = (string?)null, RowVer = rv }, new { RowVer = rv } };

        foreach (var body in bodies) Rejected(await Adm(Post, Act(D.Od2, "stop"), body), 400);

        (await OrderAsync(D.Od2.OrderId))!.Status.Should().Be(OrderStatuses.InProduction);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_059_Reason_Length_Boundaries()
    {
        var tooLongStop = await Adm(Post, Act(D.Od2, "stop"), new { Reason = Viet(501), RowVer = D.Od2.RowVer });
        var tooLongCancel = await Adm(Post, Act(D.Od1, "cancel"), new { Reason = Viet(501), RowVer = D.Od1.RowVer });
        Rejected(tooLongStop, 400);
        Rejected(tooLongCancel, 400);
        (await OrderAsync(D.Od2.OrderId))!.Status.Should().Be(OrderStatuses.InProduction);
        (await OrderAsync(D.Od1.OrderId))!.Status.Should().Be(OrderStatuses.New);
        var reason = Viet(500);

        var ok = await Adm(Post, Act(D.Od2, "stop"), new { Reason = reason, RowVer = D.Od2.RowVer });

        ok.Code.Should().Be(200, ok.ToString());
        (await OrderAsync(D.Od2.OrderId))!.StopReason.Should().Be(reason);
    }

    [Fact]
    public async Task IT_B4_060_Stop_Order_Not_In_Production_Is_Rejected()
    {
        var before = await Snap();

        var r = await Adm(Post, Act(D.Od1, "stop"), new { Reason = "Thử dừng", RowVer = D.Od1.RowVer });

        Rejected(r, 400, 409);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_062_Stale_RowVer_Returns_409_And_Unknown_Id_Returns_404()
    {
        var r1 = D.Od1.RowVer;
        var bump = await Adm(Put, A + $"customer-orders/{D.Od1.OrderId}", await UpdateBodyAsync(D.Od1, KeepOd1()));
        bump.Code.Should().Be(200, bump.ToString());
        var before = await Snap();
        var missing = await MissingIdAsync();

        Rejected(await Adm(Post, Act(D.Od1, "cancel"), new { Reason = "x", RowVer = r1 }), 409);
        Rejected(await Adm(Post, Act(D.Od1, "stop"), new { Reason = "x", RowVer = r1 }), 409);
        Rejected(await Adm(Post, Act(D.Od1, "start-production"), new { RowVer = r1 }), 409);
        Rejected(await Adm(Post, A + $"customer-orders/{missing}/cancel", new { Reason = "x", RowVer = r1 }), 404);
        Rejected(await Adm(Post, A + $"customer-orders/{missing}/stop", new { Reason = "x", RowVer = r1 }), 404);
        Rejected(await Adm(Post, A + $"customer-orders/{missing}/start-production", new { RowVer = r1 }), 404);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_063_Status_Filter_Case_Handling()
    {
        var q = "&Keyword=DH-000&PageSize=100";

        var pascal = await Adm(Get, A + "customer-orders?Status=New" + q);
        var upper = await Adm(Get, A + "customer-orders?Status=NEW" + q);
        var junk = await Adm(Get, A + "customer-orders?Status=Xyz" + q);

        pascal.Code.Should().Be(200, pascal.ToString());
        var ids = IdSet(pascal, "orderId");
        ids.Should().Contain([D.Od1.OrderId, D.Od4.OrderId]);
        ids.Should().NotContain([D.Od2.OrderId, D.Od3.OrderId, D.Od5.OrderId]);
        await using (var db = NewDb())
        {
            var newIds = await db.CustomerOrders.AsNoTracking().Where(o => o.Status == "NEW").Select(o => o.OrderId).ToListAsync();
            ids.Should().BeSubsetOf(newIds);
        }

        upper.ShouldNotBeServerError();
        upper.Code.Should().BeOneOf(200, 400);
        Note($"Status=NEW → {upper.Code}; ids = {string.Join(",", upper.Code == 200 ? IdSet(upper, "orderId") : [])}");
        if (upper.Code == 200) IdSet(upper, "orderId").Should().BeEquivalentTo(ids);
        junk.ShouldNotBeServerError();
        junk.Code.Should().BeOneOf(200, 400);
        if (junk.Code == 200) junk.Items().Should().BeEmpty();
    }
}

