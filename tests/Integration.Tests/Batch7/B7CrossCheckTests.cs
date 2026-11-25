using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_127–132 — view VwWorkOrderLineProgress, giá trị trạng thái, body lỗi, rollback tổng hợp.</summary>
public sealed class B7CrossCheckTests(ITestOutputHelper output) : B7TestBase(output)
{
    private static System.Text.Json.JsonElement LineOf(Api r, int orderLineId, int? pcmId) =>
        B1Json.Prop(r.Data, "lines")!.Value.EnumerateArray()
            .Single(l => B1Json.Int(l, "orderLineId") == orderLineId && B1Json.Int(l, "productComponentMaterialId") == pcmId);

    private async Task<(int Cut, int Handed)> ViewAsync(int workOrderLineId)
    {
        await using var db = NewDb();
        var cut = await db.Database.SqlQuery<int>($"SELECT CAST(CutQty AS int) AS [Value] FROM dbo.vw_WorkOrderLineProgress WHERE WorkOrderLineId = {workOrderLineId}").ToListAsync();
        var handed = await db.Database.SqlQuery<int>($"SELECT CAST(HandedToWarehouseQty AS int) AS [Value] FROM dbo.vw_WorkOrderLineProgress WHERE WorkOrderLineId = {workOrderLineId}").ToListAsync();
        return (cut.SingleOrDefault(), handed.SingleOrDefault());
    }

    [Fact]
    public async Task IT_B7_127_Progress_Is_Identical_Across_Prefixes_And_Matches_View()
    {
        var before = await S7();
        var m = await Req("manager", Get, $"{WOM}/{K.Wo2.WorkOrderId}");
        var w = await Req("warehouse", Get, $"{WOW}/{K.Wo2.WorkOrderId}");
        var t = await Req("tl1", Get, $"{WOT}/{K.Wo2.WorkOrderId}");
        foreach (var r in new[] { m, w, t })
        {
            r.Code.Should().Be(200, r.ToString());
            var line = LineOf(r, K.Ol3.OrderLineId, null);
            B1Json.Int(line, "cutQty").Should().Be(40);
            B1Json.Int(line, "handedQty").Should().Be(40);
        }

        (await ViewAsync(K.Wol3.WorkOrderLineId)).Should().Be((40, 40));
        (await CutSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Should().Be(40);
        (await HandedSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Should().Be(40);
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_128", "cancel")]
    [InlineData("IT_B7_128", "reject")]
    public async Task Cancelled_Or_Rejected_Handover_Is_Not_Counted_As_Handed(string caseId, string kind)
    {
        caseId.Should().StartWith("IT_B7_");
        var r = kind == "cancel"
            ? await Req("tl1", Post, $"{CHT}/{K.Ch1.CutHandoverId}/cancel")
            : await Req("warehouse", Post, $"{CHW}/{K.Ch1.CutHandoverId}/reject", new { Reason = "Sai size" });
        r.Code.Should().Be(200, r.ToString());

        var detail = await Req("manager", Get, $"{WOM}/{K.Wo2.WorkOrderId}");

        B1Json.Int(LineOf(detail, K.Ol3.OrderLineId, null), "handedQty").Should().Be(10);
        (await ViewAsync(K.Wol3.WorkOrderLineId)).Handed.Should().Be(10);
    }

    [Fact]
    public async Task IT_B7_129_Whole_Set_And_Single_Component_Progress_Are_Separate()
    {
        var wol = new NX_MTD.Domain.Entities.WorkOrderLine
        {
            WorkOrderId = K.Wo2.WorkOrderId, OrderLineId = K.Ol3.OrderLineId, ProductComponentMaterialId = K.Pcm1.ProductComponentMaterialId, PlannedQty = 10,
        };
        Db.WorkOrderLines.Add(wol);
        await Db.SaveChangesAsync();
        await AddLayAsync(K.Wo2.WorkOrderId, "MORNING", 2, (K.Ol3.OrderLineId, K.Pcm1.ProductComponentMaterialId, 7));

        var r = await Req("manager", Get, $"{WOM}/{K.Wo2.WorkOrderId}");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(LineOf(r, K.Ol3.OrderLineId, null), "cutQty").Should().Be(40);
        B1Json.Int(LineOf(r, K.Ol3.OrderLineId, K.Pcm1.ProductComponentMaterialId), "cutQty").Should().Be(7);
        (await ViewAsync(wol.WorkOrderLineId)).Cut.Should().Be(7);
        (await ViewAsync(K.Wol3.WorkOrderLineId)).Cut.Should().Be(40);
    }

    [Fact]
    public async Task IT_B7_130_All_Persisted_Status_Values_Are_Upper_Snake()
    {
        var created = await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 5)]));
        created.Code.Should().Be(200, created.ToString());
        (await Req("manager", Post, $"{WOM}/{K.Wo1.WorkOrderId}/cancel")).Code.Should().Be(200);
        (await Req("tl1", Post, CLT, LayBody(K.Wo6.WorkOrderId, [LL(K.Ol3.OrderLineId, 1, K.Pcm1.ProductComponentMaterialId)], null, "OVERTIME"))).ShouldNotBeServerError();
        (await Req("warehouse", Post, $"{CHW}/{K.Ch1.CutHandoverId}/confirm", ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30)))).Code.Should().Be(200);
        (await Req("warehouse", Post, $"{CHW}/{K.Ch9.CutHandoverId}/reject", new { Reason = "x" })).Code.Should().Be(200);

        await using var db = NewDb();
        (await db.WorkOrders.Select(x => x.Status).Distinct().ToListAsync()).Should().OnlyContain(s => WorkOrderStatuses.All.Contains(s));
        (await db.CuttingLays.Select(x => x.Status).Distinct().ToListAsync()).Should().OnlyContain(s => CuttingLayStatuses.All.Contains(s));
        (await db.CuttingLays.Select(x => x.ShiftCode).Distinct().ToListAsync()).Should().OnlyContain(s => ShiftCodes.All.Contains(s));
        (await db.CutHandovers.Select(x => x.Status).Distinct().ToListAsync()).Should()
            .OnlyContain(s => s == HandoverStatuses.Pending || s == HandoverStatuses.Confirmed || s == HandoverStatuses.Rejected || s == HandoverStatuses.Cancelled);
        (await db.WorkOrders.CountAsync(x => x.Status == "Enough" || x.Status == "Short" || x.Status == "Unknown")).Should().Be(0);
    }

    [Fact]
    public async Task IT_B7_131_Malformed_Bodies_Return_400_Or_415_Never_500()
    {
        var before = await S7();
        var targets = new (string Who, HttpMethod M, string Url, Func<Task<string>> Token)[]
        {
            ("manager", Post, WOM, TkMgr),
            ("tl1", Post, CLT, () => TokenAsync(S0.Tl2)),
            ("warehouse", Post, $"{CHW}/{K.Ch1.CutHandoverId}/confirm", TkWk),
        };
        foreach (var (who, m, url, token) in targets)
        {
            Rejected(await Req(who, m, url, "null"), 400, 415);
            Rejected(await Req(who, m, url, "{ \"lines\": [ bad json"), 400, 415);
            using var req = new HttpRequestMessage(m, url) { Content = new StringContent("plain text", Encoding.UTF8, "text/plain") };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token());
            using var res = await Client.SendAsync(req);
            ((int)res.StatusCode).Should().BeOneOf(400, 415);
        }

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_132_Failed_Requests_Leave_No_Audit_Stock_Or_Partial_Rows()
    {
        await CutOnlyForSetupAsync();
        var audit = await MaxAuditAsync();
        var st = await MaxStAsync();
        var before = await S7();

        Rejected(await Req("manager", Post, WOM, WoBody([WL(K.Ol3.OrderLineId, 10), WL(K.Ol3.OrderLineId, 10)])), 400, 409);
        Rejected(await Req("tl1", Post, CLT, LayBody(K.Wo1.WorkOrderId, [LL(K.Ol3.OrderLineId, 5), LL(K.Ol3.OrderLineId, 5)])), 400, 409);
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 11))), 400, 409);
        var lineId = (await ChlsAsync(K.Ch2.CutHandoverId)).Single().CutHandoverLineId;
        Rejected(await Req("warehouse", Post, $"{CHW}/{K.Ch2.CutHandoverId}/confirm", ConfirmBody(CF(lineId, 10))), 409);

        (await MaxAuditAsync()).Should().Be(audit);
        (await MaxStAsync()).Should().Be(st);
        await Same7(before);
    }

    private Task CutOnlyForSetupAsync() => AddLayAsync(K.Wo2.WorkOrderId, "MORNING", 2, (K.Ol3.OrderLineId, null, 10));
}
