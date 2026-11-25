using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_064–079 — danh sách / chi tiết / GBR-02 / phân trang [Đ7] / vw_OrderLineProgress [Đ4].</summary>
public sealed class B4CustomerOrderQueryTests(ITestOutputHelper output) : B4TestBase(output)
{
    private static bool HasKey(JsonElement el, string name)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) || HasKey(p.Value, name)) return true;
                return false;
            case JsonValueKind.Array:
                return el.EnumerateArray().Any(i => HasKey(i, name));
            default:
                return false;
        }
    }

    private static void AssertPage(Api r, int size, bool ok)
    {
        if (ok)
        {
            r.Code.Should().Be(200, r.ToString());
            r.Items().Count.Should().BeLessThanOrEqualTo(size);
        }
        else Rejected(r, 400);
    }

    [Theory]
    [InlineData("IT_B4_064", 0, false)]
    [InlineData("IT_B4_064", 1, true)]
    [InlineData("IT_B4_064", 100, true)]
    [InlineData("IT_B4_064", 101, false)]
    public async Task IT_B4_064_PageSize_Boundaries_On_Admin_List(string caseId, int size, bool ok)
    {
        var before = await Snap();

        var r = await Adm(Get, A + $"customer-orders?PageSize={size}");

        AssertPage(r, size, ok);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_065_PageSize_Rule_Is_Consistent_Across_Endpoints()
    {
        var before = await Snap();
        var urls = new[]
        {
            M + "order-line-teams?", M + $"order-materials/paged?OrderId={D.Od2.OrderId}&", C + "order-lookups/orders?",
        };

        foreach (var url in urls)
            foreach (var (size, ok) in new[] { (0, false), (1, true), (100, true), (101, false) })
            {
                var r = await Mgr(Get, url + $"PageSize={size}");
                AssertPage(r, size, ok);
            }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_066_Page_Boundaries()
    {
        var before = await Snap();

        Rejected(await Adm(Get, A + "customer-orders?Page=0"), 400);
        Rejected(await Adm(Get, A + "customer-orders?Page=-1"), 400);
        var far = await Adm(Get, A + "customer-orders?Page=999");

        far.Code.Should().Be(200, far.ToString());
        far.Items().Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_067_Keyword_Sort_And_Injection_Are_Safe()
    {
        var before = await Snap();

        var k100 = await Adm(Get, A + $"customer-orders?Keyword={new string('a', 100)}");
        var k101 = await Adm(Get, A + $"customer-orders?Keyword={new string('a', 101)}");
        var sql = await Adm(Get, A + "customer-orders?Keyword=" + Uri.EscapeDataString("' OR 1=1--"));
        var asc = await Adm(Get, A + "customer-orders?Keyword=DH-000&PageSize=100&SortDir=ASC");
        var junk = await Adm(Get, A + "customer-orders?Keyword=DH-000&PageSize=100&SortDir=xyz");
        var sortBy = await Adm(Get, A + "customer-orders?SortBy=" + Uri.EscapeDataString("OrderCode;DROP TABLE CustomerOrder"));

        k100.Code.Should().Be(200, k100.ToString());
        Rejected(k101, 400);
        sql.Code.Should().Be(200, sql.ToString());
        sql.Items().Should().BeEmpty();
        asc.Code.Should().Be(200, asc.ToString());
        junk.Code.Should().Be(200, junk.ToString());
        var ascIds = asc.Items().Select(i => B1Json.Int(i, "orderId")!.Value).ToList();
        var descIds = junk.Items().Select(i => B1Json.Int(i, "orderId")!.Value).ToList();
        ascIds.Should().BeInAscendingOrder();
        descIds.Should().BeInDescendingOrder();
        sortBy.ShouldNotBeServerError();
        sortBy.Code.Should().BeOneOf(200, 400);
        await using (var db = NewDb()) (await db.CustomerOrders.CountAsync()).Should().BeGreaterThan(0, "bảng còn nguyên");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_068_Date_And_Customer_Filters_Are_Safe()
    {
        var before = await Snap();

        var reversed = await Adm(Get, A + "customer-orders?DueFrom=2026-12-31&DueTo=2026-01-01");
        var noCustomer = await Adm(Get, A + $"customer-orders?CustomerId={await MissingIdAsync()}");
        var min = await Adm(Get, A + "customer-orders?DueFrom=0001-01-01");

        reversed.ShouldNotBeServerError();
        reversed.Code.Should().BeOneOf(200, 400);
        if (reversed.Code == 200) reversed.Items().Should().BeEmpty();
        noCustomer.Code.Should().Be(200, noCustomer.ToString());
        noCustomer.Items().Should().BeEmpty();
        min.Code.Should().Be(200, min.ToString());
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_069_Admin_Detail_Has_Price_History_RowVer_And_Flags()
    {
        var before = await Snap();

        var r = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");

        r.Code.Should().Be(200, r.ToString());
        var d = r.Data;
        B1Json.Bool(d, "hasOrderMarkers").Should().BeTrue();
        B1Json.Bool(d, "hasOrderMaterials").Should().BeTrue();
        B1Json.Prop(d, "history").Should().NotBeNull();
        B1Json.Str(d, "rowVer").Should().NotBeNullOrEmpty();
        HasKey(d, "stopReason").Should().BeTrue();
        var apiLines = B1Json.Prop(d, "lines")!.Value.EnumerateArray().ToList();
        apiLines.Should().OnlyContain(l => HasKey(l, "cmtUnitPrice"));
        var db = await LineVariantsAsync(D.Od2.OrderId);
        apiLines.Select(l => (B1Json.Int(l, "orderLineId")!.Value, B1Json.Str(l, "color")!, B1Json.Str(l, "sizeCode")!, B1Json.Int(l, "orderedQty")!.Value))
            .Should().BeEquivalentTo(db.Select(x => (x.OrderLineId, x.Color, x.Size, x.Qty)));
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_070_Manager_View_Hides_Price_Total_History_RowVer()
    {
        var before = await Snap();

        var detail = await Mgr(Get, M + $"customer-orders/{D.Od2.OrderId}");
        var list = await Mgr(Get, M + "customer-orders?Keyword=DH-000&PageSize=100");

        detail.Code.Should().Be(200, detail.ToString());
        list.Code.Should().Be(200, list.ToString());
        foreach (var key in new[] { "cmtUnitPrice", "totalAmount", "history", "rowVer" })
        {
            HasKey(detail.Root, key).Should().BeFalse($"GBR-02: detail không được có '{key}'");
            HasKey(list.Root, key).Should().BeFalse($"GBR-02: list không được có '{key}'");
        }

        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_071", "accountant")]
    [InlineData("IT_B4_071", "teamleader")]
    public async Task IT_B4_071_OrderLookups_Never_Expose_Price(string caseId, string who)
    {
        var before = await Snap();

        var orders = await CallAs(who, Get, C + "order-lookups/orders?Keyword=DH-000&PageSize=100");
        var lines = await CallAs(who, Get, C + $"order-lookups/orders/{D.Od2.OrderId}/lines");
        var products = await CallAs(who, Get, C + "order-lookups/products");
        var colors = await CallAs(who, Get, C + $"order-lookups/product-colors?productId={B.Pr1.ProductId}");

        foreach (var r in new[] { orders, lines, products, colors })
        {
            r.Code.Should().Be(200, r.ToString());
            HasKey(r.Root, "cmtUnitPrice").Should().BeFalse("D26/GBR-02");
        }

        var ids = IdSet(orders, "orderId");
        ids.Should().Contain(D.Od2.OrderId);
        ids.Should().NotContain([D.Od1.OrderId, D.Od3.OrderId, D.Od4.OrderId, D.Od5.OrderId]);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_072_TeamLeader_Cannot_Read_Other_Teams_Lines()
    {
        await using (var db = NewDb())
        {
            db.OrderLineTeams.Add(new OrderLineTeam { OrderLineId = D.Ol2.OrderLineId, SewingTeamId = S0.TSew02.TeamId, AssignedAt = Clock.Now });
            await db.SaveChangesAsync();
        }

        var before = await Snap();

        var r = await Call("teamleader", Get, C + $"order-lookups/orders/{D.Od1.OrderId}/lines?sewingTeamId={S0.TSew02.TeamId}");

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(403, 200);
        if (r.Code == 200) IdSet(r, "orderLineId").Should().NotContain(D.Ol2.OrderLineId, "IDOR theo tổ: tổ trưởng T_SEW01 không được thấy dòng của T_SEW02");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_073_Unknown_Lookups_Do_Not_Return_500()
    {
        var before = await Snap();
        var missing = await MissingIdAsync();

        var lines = await Call("accountant", Get, C + $"order-lookups/orders/{missing}/lines");
        var colors = await Call("accountant", Get, C + $"order-lookups/product-colors?productId={missing}");
        var route = await Call("accountant", Get, C + "order-lookups/orders/abc/lines");

        lines.ShouldNotBeServerError();
        lines.Code.Should().BeOneOf(200, 404);
        if (lines.Code == 200) lines.Items().Should().BeEmpty();
        colors.Code.Should().Be(200, colors.ToString());
        colors.Items().Should().BeEmpty();
        route.Code.Should().Be(404, route.ToString());
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_074_IsDelayRisk_Is_Computed_And_Read_Only()
    {
        await using (var db = NewDb())
        {
            var od = await db.CustomerOrders.SingleAsync(o => o.OrderId == D.Od2.OrderId);
            od.DelayRiskSince = Today.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var r = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Bool(r.Data, "isDelayRisk").Should().BeTrue();
        (await OrderAsync(D.Od2.OrderId))!.IsDelayRisk.Should().BeTrue();
        await using var write = NewDb();
        var act = () => write.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.CustomerOrder SET IsDelayRisk = 0 WHERE OrderId = {D.Od2.OrderId}");
        await act.Should().ThrowAsync<Exception>("cột computed chỉ đọc");
    }

    // ---------------------------------------------------------------- 075–079 vw_OrderLineProgress

    private static int DeliveredOf(Api r, int lineId)
    {
        var line = B1Json.Prop(r.Data, "lines")!.Value.EnumerateArray().Single(l => B1Json.Int(l, "orderLineId") == lineId);
        return B1Json.Int(line, "deliveredQty")!.Value;
    }

    [Fact]
    public async Task IT_B4_075_DeliveredQty_Counts_Only_Delivered_Notes()
    {
        var admin = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");
        var mgr = await Mgr(Get, M + $"customer-orders/{D.Od2.OrderId}");

        DeliveredOf(admin, D.Ol3.OrderLineId).Should().Be(80);
        DeliveredOf(mgr, D.Ol3.OrderLineId).Should().Be(80);
        (await DeliveredQtyAsync(D.Ol3.OrderLineId)).Should().Be(80);
        (await DispatchSumAsync(D.Ol3.OrderLineId, "DELIVERED")).Should().Be(80);
    }

    [Fact]
    public async Task IT_B4_076_View_Reflects_New_Delivered_Note_Immediately()
    {
        await using (var db = NewDb())
        {
            var dn = new DispatchNote
            {
                DispatchCode = "DN-B4-03", OrderId = D.Od2.OrderId, DeliveryDate = Today, DeliveryAddress = "KCN Tân Tạo", Status = "DELIVERED", CreatedAt = Clock.Now,
                DispatchedBy = S0.Wh.EmployeeId, DispatchedAt = Clock.Now, CustomerReceivedDate = Today, DeliveryMethod = "CUSTOMER_PICKUP",
            };
            db.DispatchNotes.Add(dn);
            await db.SaveChangesAsync();
            db.DispatchNoteLines.Add(new DispatchNoteLine { DispatchNoteId = dn.DispatchNoteId, OrderLineId = D.Ol3.OrderLineId, IssueQty = 20 });
            await db.SaveChangesAsync();
        }

        var r = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");

        DeliveredOf(r, D.Ol3.OrderLineId).Should().Be(100);
        (await DeliveredQtyAsync(D.Ol3.OrderLineId)).Should().Be(100);
    }

    [Fact]
    public async Task IT_B4_077_Line_Without_Dispatch_Has_Zero_Delivered()
    {
        var r = await Adm(Get, A + $"customer-orders/{D.Od1.OrderId}");

        r.Code.Should().Be(200, r.ToString());
        DeliveredOf(r, D.Ol1.OrderLineId).Should().Be(0);
        (await DeliveredQtyAsync(D.Ol1.OrderLineId)).Should().Be(0);
    }

    [Fact]
    public async Task IT_B4_078_Over_Delivered_Line_Does_Not_Break_The_View()
    {
        await using (var db = NewDb())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DispatchNoteLine SET IssueQty = 250 WHERE DispatchNoteId = {D.DnDelivered.DispatchNoteId}");

        var r = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");

        r.Code.Should().Be(200, r.ToString());
        DeliveredOf(r, D.Ol3.OrderLineId).Should().Be(250);
        (await DeliveredQtyAsync(D.Ol3.OrderLineId)).Should().Be(250);
    }

    [Fact]
    public async Task IT_B4_079_Team_Allotment_And_Progress_Are_Consistent_Across_Endpoints()
    {
        var teams = await Mgr(Get, M + "order-line-teams?PageSize=100");
        var lines = await Call("teamleader", Get, C + $"order-lookups/orders/{D.Od2.OrderId}/lines?sewingTeamId={S0.TSew01.TeamId}");
        var detail = await Adm(Get, A + $"customer-orders/{D.Od2.OrderId}");

        teams.Code.Should().Be(200, teams.ToString());
        lines.Code.Should().Be(200, lines.ToString());
        var row = teams.Items().Single(i => B1Json.Int(i, "orderLineId") == D.Ol3.OrderLineId);
        B1Json.Prop(row, "teams")!.Value.EnumerateArray().Select(t => B1Json.Str(t, "teamCode")).Should().Equal(S0.TSew01.TeamCode);
        B1Json.Int(row, "orderedQty").Should().Be(200);
        B1Json.Int(lines.Items().Single(i => B1Json.Int(i, "orderLineId") == D.Ol3.OrderLineId), "orderedQty").Should().Be(200);
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        DeliveredOf(detail, D.Ol3.OrderLineId).Should().Be(await DeliveredQtyAsync(D.Ol3.OrderLineId));
    }
}

