using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_059–070 — lệnh cắt bù, kiểm vải, endpoint đọc của thủ kho / tổ trưởng, lọc và phân trang.</summary>
public sealed class B7WorkOrderReadTests(ITestOutputHelper output) : B7TestBase(output)
{
    [Fact]
    public async Task IT_B7_059_Replacement_Requests_Lists_Only_Open_Approved()
    {
        var before = await S7();
        var r = await Req("manager", Get, $"{WOM}/replacement-requests");
        r.Code.Should().Be(200, r.ToString());
        var ids = IdSet(r, "requestId");
        ids.Should().Contain(K.Rq1.RequestId).And.NotContain(K.Rq2.RequestId);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_060_Create_Replacement_WorkOrder_From_Approved_Request()
    {
        var max = await MaxWoIdAsync();
        var body = new { RequestId = K.Rq1.RequestId, CuttingTeamId = S0.TCut01.TeamId, DueDate = T.AddDays(3), Note = "Cắt bù lỗi" };

        var r = await Req("manager", Post, $"{WOM}/replacement", body);

        r.Code.Should().Be(200, r.ToString());
        var wo = (await WoAfterAsync(max)).Should().ContainSingle().Subject;
        wo.Status.Should().Be(WorkOrderStatuses.Open);
        wo.SourceRequestId.Should().Be(K.Rq1.RequestId);
        wo.CuttingTeamId.Should().Be(S0.TCut01.TeamId);
        var lines = await WolsAsync(wo.WorkOrderId);
        lines.Should().ContainSingle().Which.Should().Match<WorkOrderLine>(l =>
            l.OrderLineId == K.Ol3.OrderLineId && l.ProductComponentMaterialId == K.Pcm1.ProductComponentMaterialId && l.PlannedQty == 5);
        var list = await Req("manager", Get, $"{WOM}/replacement-requests");
        IdSet(list, "requestId").Should().NotContain(K.Rq1.RequestId);
    }

    [Fact]
    public async Task IT_B7_061_Replacement_For_Request_Already_Used_Returns_409()
    {
        var before = await S7();
        var r = await Req("manager", Post, $"{WOM}/replacement", new { RequestId = K.Rq2.RequestId, CuttingTeamId = S0.TCut01.TeamId, DueDate = T.AddDays(3), Note = (string?)null });
        Rejected(r, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_063_Replacement_Invalid_Inputs_Are_Rejected()
    {
        var pending = new Request
        {
            RequestCode = "YC-B7-90", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.PendingApproval, OrderId = K.Od2.OrderId, CreatedAt = Clock.Now,
        };
        var wrongType = new Request
        {
            RequestCode = "YC-B7-91", RequestTypeCode = RequestTypes.SpareParts, Status = RequestStatuses.Approved, OrderId = K.Od2.OrderId, CreatedAt = Clock.Now,
        };
        Db.Requests.AddRange(pending, wrongType);
        await Db.SaveChangesAsync();
        var before = await S7();
        object Rb(int id, int team, string due = "") => new { RequestId = id, CuttingTeamId = team, DueDate = due == "" ? T.AddDays(3).ToString("yyyy-MM-dd") : due, Note = (string?)null };

        Rejected(await Req("manager", Post, $"{WOM}/replacement", Rb(await MissingRequestIdAsync(), S0.TCut01.TeamId)), 404);
        Rejected(await Req("manager", Post, $"{WOM}/replacement", Rb(pending.RequestId, S0.TCut01.TeamId)), 400, 409);
        Rejected(await Req("manager", Post, $"{WOM}/replacement", Rb(wrongType.RequestId, S0.TCut01.TeamId)), 400, 409);
        Rejected(await Req("manager", Post, $"{WOM}/replacement", Rb(K.Rq1.RequestId, S0.TSew01.TeamId)), 400);
        Rejected(await Req("manager", Post, $"{WOM}/replacement", Rb(K.Rq1.RequestId, S0.TCut01.TeamId, "0001-01-01")), 400);

        await Same7(before);
    }

    private async Task<int> MissingRequestIdAsync()
    {
        await using var db = NewDb();
        return db.Requests.Max(x => x.RequestId) + 1_000_000;
    }

    [Fact]
    public async Task IT_B7_064_FabricCheck_Is_Read_Only_And_Uses_PascalCase_Status()
    {
        var before = await S7();
        var body = new { OrderId = K.Od2.OrderId, CuttingTeamId = S0.TCut01.TeamId, Lines = new[] { WL(K.Ol3.OrderLineId, 30) } };

        var r = await Req("manager", Post, $"{WOM}/fabric-check", body);

        r.Code.Should().Be(200, r.ToString());
        var checks = r.Data.ValueKind == System.Text.Json.JsonValueKind.Array ? r.Data.EnumerateArray().ToList() : r.Items();
        foreach (var c in checks) B1Json.Str(c, "status").Should().BeOneOf("Enough", "Short", "Unknown");
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_065_FabricCheck_Invalid_Input_Is_Rejected()
    {
        var before = await S7();
        object Fb(int orderId, object[] lines) => new { OrderId = orderId, CuttingTeamId = S0.TCut01.TeamId, Lines = lines };
        Rejected(await Req("manager", Post, $"{WOM}/fabric-check", Fb(K.Od2.OrderId, [])), 400);
        Rejected(await Req("manager", Post, $"{WOM}/fabric-check", Fb(await MissingIdAsync(), [WL(K.Ol3.OrderLineId, 5)])), 404);
        Rejected(await Req("manager", Post, $"{WOM}/fabric-check", Fb(K.Od2.OrderId, [WL(K.Ol3.OrderLineId, -1)])), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_066_WarehouseKeeper_Reads_Fabrics_And_Fabric_Needs()
    {
        var before = await S7();
        var fabrics = await Req("warehouse", Get, $"{WOW}/{K.Wo1.WorkOrderId}/fabrics");
        var needs = await Req("warehouse", Get, $"{WOW}/{K.Wo1.WorkOrderId}/fabric-needs?color=Đen&sizeCode=M");
        fabrics.Code.Should().Be(200, fabrics.ToString());
        needs.Code.Should().Be(200, needs.ToString());
        var item = needs.Items().SingleOrDefault(i => B1Json.Int(i, "materialId") == B.Ma1.MaterialId);
        item.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined, "vải MA1 phải có nhu cầu theo màu Đen / size M");
        B1Json.Dec(item, "needMeters").Should().BeGreaterThan(0m);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_067_Fabric_Needs_Boundary_Filters()
    {
        var before = await S7();
        var other = await Req("warehouse", Get, $"{WOW}/{K.Wo1.WorkOrderId}/fabric-needs?color=Xanh");
        var longSize = await Req("warehouse", Get, $"{WOW}/{K.Wo1.WorkOrderId}/fabric-needs?sizeCode=ABCDEFGHIJK");
        var missing = await Req("warehouse", Get, $"{WOW}/{await MissingWoIdAsync()}/fabrics");
        other.Code.Should().Be(200, other.ToString());
        other.Items().Should().BeEmpty();
        Rejected(longSize, 200, 400);
        if (longSize.Code == 200) longSize.Items().Should().BeEmpty();
        Rejected(missing, 404);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_068_Material_Allowance_Handles_ExceptRequestId()
    {
        var before = await S7();
        var url = $"{WOT}/{K.Wo2.WorkOrderId}/material-allowance";
        var plain = await Req("tl1", Get, url);
        var withReq = await Req("tl1", Get, $"{url}?exceptRequestId={K.Rq1.RequestId}");
        var unknown = await Req("tl1", Get, $"{url}?exceptRequestId=999999");
        var bad = await Req("tl1", Get, $"{url}?exceptRequestId=abc");
        plain.Code.Should().Be(200, plain.ToString());
        withReq.Code.Should().Be(200, withReq.ToString());
        unknown.Code.Should().Be(200, unknown.ToString());
        Rejected(bad, 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_069_Status_And_Order_Filters_Match_Db()
    {
        var before = await S7();
        var open = await Req("manager", Get, $"{WOM}?Status=OPEN&PageSize=100");
        var pascal = await Req("manager", Get, $"{WOM}?Status=Open&PageSize=100");
        var xyz = await Req("manager", Get, $"{WOM}?Status=Xyz&PageSize=100");
        var byOrder = await Req("manager", Get, $"{WOM}?OrderId={K.Od2.OrderId}&PageSize=100");
        open.Code.Should().Be(200, open.ToString());
        var ids = IdSet(open, "workOrderId");
        ids.Should().Contain([K.Wo1.WorkOrderId, K.Wo3.WorkOrderId]);
        await using (var db = NewDb())
            ids.Should().BeEquivalentTo(db.WorkOrders.Where(w => w.Status == WorkOrderStatuses.Open).Select(w => w.WorkOrderId).ToList());
        Rejected(pascal, 200);
        Note("Status=Open (Pascal) → " + IdSet(pascal, "workOrderId").Count + " dòng");
        Rejected(xyz, 200, 400);
        if (xyz.Code == 200) xyz.Items().Should().BeEmpty();
        byOrder.Code.Should().Be(200, byOrder.ToString());
        IdSet(byOrder, "workOrderId").Should().Contain(K.Wo1.WorkOrderId).And.NotContain(0);
        await using (var db = NewDb())
            IdSet(byOrder, "workOrderId").Should().BeEquivalentTo(db.WorkOrders.Where(w => w.OrderId == K.Od2.OrderId).Select(w => w.WorkOrderId).ToList());
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_070_Paging_Sorting_And_Keyword_Boundaries()
    {
        var before = await S7();
        foreach (var (qs, size) in new[] { ("PageSize=0", 20), ("PageSize=1", 1), ("PageSize=100", 100), ("PageSize=101", 100), ("PageSize=-1", 20), ("Page=0", 20) })
        {
            foreach (var who in new[] { "manager", "tl1" })
            {
                var url = (who == "manager" ? WOM : WOT) + "?" + qs;
                var r = await Req(who, Get, url);
                r.Code.Should().Be(200, $"{url}: {r}");
                if (qs.StartsWith("PageSize")) B1Json.Int(r.Data, "pageSize").Should().Be(size, url);
                r.Items().Count.Should().BeLessThanOrEqualTo(size);
            }
        }

        var asc = await Req("manager", Get, $"{WOM}?SortDir=ASC&PageSize=100");
        var other = await Req("manager", Get, $"{WOM}?SortDir=xyz&PageSize=100");
        asc.Code.Should().Be(200, asc.ToString());
        other.Code.Should().Be(200, other.ToString());
        asc.Items().Select(i => B1Json.Int(i, "workOrderId")).Reverse().Should().Equal(other.Items().Select(i => B1Json.Int(i, "workOrderId")));

        Rejected(await Req("manager", Get, $"{WOM}?Keyword={new string('a', 101)}"), 200, 400);
        var inj = await Req("manager", Get, $"{WOM}?Keyword=" + Uri.EscapeDataString("'; DROP TABLE WorkOrder;--"));
        Rejected(inj, 200);
        inj.Items().Should().BeEmpty();
        await Same7(before);
    }
}
