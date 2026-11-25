using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_001–022 — phân quyền theo prefix, [CuttingTeamOnly], cách ly dữ liệu theo tổ.</summary>
public sealed class B7RbacTests(ITestOutputHelper output) : B7TestBase(output)
{
    private static void CuttingOnly(Api r)
    {
        r.Code.Should().Be(403, r.ToString());
        (r.ErrorCode ?? "").Replace("_", "").ToLowerInvariant().Should().Be("warehousecuttingteamonly", r.ToString());
    }

    [Fact]
    public async Task IT_B7_001_Anonymous_Gets_401_On_All_Prefixes()
    {
        await Deny7("anonymous", [401], (Get, WOM, null), (Get, CLT, null), (Get, CHW, null));
    }

    [Fact]
    public async Task IT_B7_002_Expired_Or_Forged_Token_Cannot_Confirm()
    {
        var before = await S7();
        foreach (var who in new[] { "exp", "forged" })
            Rejected(await Req(who, Post, $"{CHW}/{K.Ch1.CutHandoverId}/confirm", ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30))), 401);

        var ch = await ChAsync(K.Ch1.CutHandoverId);
        ch.Status.Should().Be(HandoverStatuses.Pending);
        (await ChlsAsync(K.Ch1.CutHandoverId)).Single(l => l.CutHandoverLineId == K.Chl1.CutHandoverLineId).ConfirmedQty.Should().BeNull();
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_003_Admin_Is_Forbidden_On_WarehouseKeeper_Prefix()
    {
        await Deny7("admin", [403],
            (Get, WOW, null), (Get, CHW, null),
            (Post, $"{CHW}/{K.Ch1.CutHandoverId}/confirm", ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30))),
            (Post, $"{CHW}/{K.Ch1.CutHandoverId}/reject", new { Reason = "x" }));
        (await ChAsync(K.Ch1.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_004_Admin_And_Manager_Are_Forbidden_On_TeamLeader_Prefix()
    {
        foreach (var who in new[] { "admin", "manager" })
            await Deny7(who, [403],
                (Get, WOT, null), (Post, CLT, LayBody(K.Wo1.WorkOrderId)), (Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 1))));
    }

    [Fact]
    public async Task IT_B7_005_Admin_Can_Call_Manager_Prefix()
    {
        var list = await Req("admin", Get, WOM);
        list.Code.Should().Be(200, list.ToString());
        var max = await MaxWoIdAsync();
        var r = await Req("admin", Post, WOM, WoBody());
        r.Code.Should().Be(200, r.ToString());
        var created = await WoAfterAsync(max);
        created.Should().ContainSingle().Which.Status.Should().Be(WorkOrderStatuses.Open);
        (await WolsAsync(created[0].WorkOrderId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task IT_B7_006_WarehouseKeeper_Is_Forbidden_On_Manager_And_TeamLeader_Prefix()
    {
        await Deny7("warehouse", [403],
            (Post, WOM, WoBody()), (Get, CLT, null), (Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 1))));
    }

    [Fact]
    public async Task IT_B7_007_CuttingLeader_Cannot_Cancel_Or_Confirm_Own_Documents()
    {
        await Deny7("tl1", [403],
            (Get, WOM, null), (Post, $"{WOM}/{K.Wo1.WorkOrderId}/cancel", null), (Get, CHW, null),
            (Post, $"{CHW}/{K.Ch1.CutHandoverId}/confirm", ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30))));
        (await WoAsync(K.Wo1.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Open);
        (await ChAsync(K.Ch1.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_008_SewingTeamLeader_Gets_CuttingTeamOnly_403()
    {
        var before = await S7();
        foreach (var (m, url, body) in new (HttpMethod, string, object?)[]
                 {
                     (Get, WOT, null), (Get, CLT, null), (Post, CLT, LayBody(K.Wo1.WorkOrderId)),
                     (Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 1))),
                 })
        {
            var r = await Req("tlsew", m, url, body);
            r.ShouldNotBeServerError();
            CuttingOnly(r);
        }

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_009_TeamLeader_Without_Team_Gets_403_Not_500()
    {
        var before = await S7();
        foreach (var (m, url, body) in new (HttpMethod, string, object?)[] { (Get, WOT, null), (Post, CLT, LayBody(K.Wo1.WorkOrderId)) })
        {
            var r = await Req("tlnone", m, url, body);
            r.ShouldNotBeServerError();
            CuttingOnly(r);
        }

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_010_Deputy_Has_Leader_Rights_In_Own_Team()
    {
        (await Req("td1", Get, WOT)).Code.Should().Be(200);
        var max = await MaxLayIdAsync();
        var r = await Req("td1", Post, CLT, LayBody(K.Wo1.WorkOrderId));
        r.Code.Should().Be(200, r.ToString());
        var lay = (await LaysAfterAsync(max)).Should().ContainSingle().Subject;
        lay.CuttingTeamId.Should().Be(S0.TCut01.TeamId);
        lay.WorkOrderId.Should().Be(K.Wo1.WorkOrderId);
    }

    [Theory]
    [InlineData("IT_B7_011", "accountant")]
    [InlineData("IT_B7_011", "qaqc")]
    [InlineData("IT_B7_011", "worker")]
    public async Task Rbac_Other_Roles_Are_Forbidden_Everywhere(string caseId, string who)
    {
        caseId.Should().StartWith("IT_B7_");
        await Deny7(who, [403], (Get, WOM, null), (Get, WOT, null), (Get, WOW, null));
    }

    [Fact]
    public async Task IT_B7_012_Unrouted_Verbs_Return_404_Or_405()
    {
        await Deny7("tl1", [404, 405],
            (Put, $"{CHT}/{K.Ch1.CutHandoverId}", HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 1))),
            (Del, $"{CLT}/{K.Cl1.CuttingLayId}", null),
            (Post, WOT, WoBody()));
    }

    [Fact]
    public async Task IT_B7_013_Controller_Route_Casing_Is_Consistent()
    {
        var before = await S7();
        var pascal = await Req("manager", Get, "/api/v1/manager/WorkOrders");
        var kebab = await Req("manager", Get, "/api/v1/manager/work-orders");
        Note($"WorkOrders={pascal.Code}, work-orders={kebab.Code}");
        new[] { pascal.Code, kebab.Code }.Should().Contain(200, "ít nhất một dạng route phải hoạt động");
        new[] { pascal.Code, kebab.Code }.Should().OnlyContain(c => c == 200 || c == 404);
        await Same7(before);
    }

    // ---------------------------------------------------------------- 014–022 IDOR

    [Fact]
    public async Task IT_B7_014_TeamLeader_Sees_Only_Own_Team_WorkOrders()
    {
        var r = await Req("tl1", Get, WOT + "?PageSize=100");
        r.Code.Should().Be(200, r.ToString());
        var ids = IdSet(r, "workOrderId");
        ids.Should().Contain([K.Wo1.WorkOrderId, K.Wo2.WorkOrderId, K.Wo4.WorkOrderId, K.Wo5.WorkOrderId]);
        ids.Should().NotContain(K.Wo3.WorkOrderId);
        await using var db = NewDb();
        var expected = db.WorkOrders.Where(w => w.CuttingTeamId == S0.TCut01.TeamId).Select(w => w.WorkOrderId).ToList();
        ids.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IT_B7_015_TeamLeader_Cannot_Read_Other_Team_WorkOrder()
    {
        var before = await S7();
        foreach (var url in new[] { $"{WOT}/{K.Wo3.WorkOrderId}", $"{WOT}/{K.Wo3.WorkOrderId}/material-allowance" })
        {
            var r = await Req("tl1", Get, url);
            Rejected(r, 403, 404);
            r.Body.Should().NotContain(K.Wo3.WorkOrderCode);
        }

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_016_TeamLeader_Cannot_Read_Or_Update_Other_Team_Lay()
    {
        var before = await S7();
        Rejected(await Req("tl1", Get, $"{CLT}/{K.Cl9.CuttingLayId}"), 403, 404);
        Rejected(await Req("tl1", Put, $"{CLT}/{K.Cl9.CuttingLayId}", LayBody(K.Wo3.WorkOrderId, [LL(K.Ol4.OrderLineId, 3, K.Pcm2.ProductComponentMaterialId)])), 403, 404);
        await Same7(before);
        (await LayLinesAsync(K.Cl9.CuttingLayId)).Single().ActualQty.Should().Be(4);
    }

    [Fact]
    public async Task IT_B7_017_TeamLeader_Cannot_Create_Lay_For_Other_Team_WorkOrder()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo3.WorkOrderId, [LL(K.Ol4.OrderLineId, 1, K.Pcm2.ProductComponentMaterialId)])), 400, 403, 404);
        await Same7(before);
        (await LaysAsync(K.Wo3.WorkOrderId)).Should().NotContain(l => l.CuttingTeamId == S0.TCut01.TeamId);
    }

    [Fact]
    public async Task IT_B7_018_TeamLeader_Cannot_Touch_Other_Team_Handovers()
    {
        var before = await S7();
        Rejected(await Req("tl1", Get, $"{CHT}/{K.Ch9.CutHandoverId}"), 403, 404);
        Rejected(await Req("tl1", Post, $"{CHT}/{K.Ch9.CutHandoverId}/cancel"), 403, 404);
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo3.WorkOrderId, HL(K.Ol4.OrderLineId, 1, K.Pcm2.ProductComponentMaterialId))), 400, 403, 404);
        var h = await Req("tl1", Get, $"{CHT}/handoverable?workOrderId={K.Wo3.WorkOrderId}");
        Rejected(h, 200, 403, 404);
        if (h.Code == 200) h.Items().Should().BeEmpty();
        await Same7(before);
        (await ChAsync(K.Ch9.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_019_List_Filters_Do_Not_Leak_Other_Team_Documents()
    {
        var before = await S7();
        var lays = await Req("tl1", Get, $"{CLT}?WorkOrderId={K.Wo3.WorkOrderId}");
        var hos = await Req("tl1", Get, $"{CHT}?WorkOrderId={K.Wo3.WorkOrderId}");
        lays.Code.Should().Be(200, lays.ToString());
        hos.Code.Should().Be(200, hos.ToString());
        lays.Items().Should().BeEmpty();
        hos.Items().Should().BeEmpty();
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_020_TeamId_Zero_Never_Returns_All_Teams()
    {
        var before = await S7();
        var r = await Req("tlnone", Get, WOT);
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(403, 200);
        if (r.Code == 200) r.Items().Should().BeEmpty();
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_021_Manager_And_WarehouseKeeper_See_All_Teams_Consistently()
    {
        var before = await S7();
        var m = await Req("manager", Get, $"{WOM}/{K.Wo3.WorkOrderId}");
        var w = await Req("warehouse", Get, $"{WOW}/{K.Wo3.WorkOrderId}");
        var h = await Req("warehouse", Get, $"{CHW}/{K.Ch9.CutHandoverId}");
        m.Code.Should().Be(200, m.ToString());
        w.Code.Should().Be(200, w.ToString());
        h.Code.Should().Be(200, h.ToString());
        B1Json.Str(m.Data, "workOrderCode").Should().Be(K.Wo3.WorkOrderCode);
        B1Json.Str(w.Data, "workOrderCode").Should().Be(B1Json.Str(m.Data, "workOrderCode"));
        B1Json.Str(w.Data, "status").Should().Be(B1Json.Str(m.Data, "status"));
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_022_Token_Scope_After_Team_Change_Follows_Claim_Or_Db()
    {
        var oldToken = await TokenAsync(S0.Tl2);
        await using (var db = NewDb())
        {
            // Mỗi tổ chỉ một tổ trưởng: chuyển tổ trưởng T_CUT02 sang tổ đệm trước rồi mới chuyển TK_TL1 sang T_CUT02.
            var other = await db.Employees.SingleAsync(e => e.EmployeeId == K.Tl2.EmployeeId);
            other.TeamId = K.TCutOld.TeamId;
            await db.SaveChangesAsync();
            var emp = await db.Employees.SingleAsync(e => e.EmployeeId == S0.Tl2.EmployeeId);
            emp.TeamId = K.TCut02.TeamId;
            await db.SaveChangesAsync();
        }

        var stale = await GetAsync(WOT + "?PageSize=100", oldToken);
        stale.Code.Should().BeOneOf(200, 401, 403);
        if (stale.Code == 200)
        {
            Note("token cũ vẫn dùng TeamId theo claim cũ: " + string.Join(",", IdSet(stale, "workOrderId")));
        }

        var fresh = await Req("tl1", Get, WOT + "?PageSize=100");
        fresh.Code.Should().Be(200, fresh.ToString());
        IdSet(fresh, "workOrderId").Should().Contain(K.Wo3.WorkOrderId).And.NotContain(K.Wo1.WorkOrderId);
    }
}
