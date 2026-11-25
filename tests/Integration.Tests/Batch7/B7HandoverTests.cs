using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7_096–126 — bàn giao BTP: tổ trưởng tạo/hủy (TL/cut-handovers), thủ kho xác nhận/từ chối (WK/cut-handovers). Ca đồng thời nằm ở B7ConcurrencyTests.</summary>
public sealed class B7HandoverTests(ITestOutputHelper output) : B7TestBase(output)
{
    private static string Confirm(int id) => $"{CHW}/{id}/confirm";
    private static string Reject(int id) => $"{CHW}/{id}/reject";

    /// <summary>WO2 đã cắt thêm 10 (CL2) nên tổng cắt = 50 và còn được giao 10.</summary>
    private Task<CuttingLay> CutTenMoreAsync() => AddLayAsync(K.Wo2.WorkOrderId, "MORNING", 2, (K.Ol3.OrderLineId, null, 10));

    private object Ol3Hand(int qty) => HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, qty));

    private async Task<(int Cut, int Handed, int Remaining)> HandoverableAsync(int woId, int orderLineId, int? pcmId = null)
    {
        var r = await Req("tl1", Get, $"{CHT}/handoverable?workOrderId={woId}");
        r.Code.Should().Be(200, r.ToString());
        var item = r.Items().Single(i => B1Json.Int(i, "orderLineId") == orderLineId && B1Json.Int(i, "productComponentMaterialId") == pcmId);
        return (B1Json.Int(item, "cutQty")!.Value, B1Json.Int(item, "handedQty")!.Value, B1Json.Int(item, "remainingQty")!.Value);
    }

    // ---------------------------------------------------------------- 096–109 tổ trưởng

    [Fact]
    public async Task IT_B7_096_Handoverable_Equals_Cut_Minus_Active_Handed()
    {
        var before = await S7();
        var (cut, handed, remaining) = await HandoverableAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId);
        (cut, handed, remaining).Should().Be((40, 40, 0));
        (await CutSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId) - await HandedSumAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Should().Be(remaining);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_097_Handoverable_With_Missing_Or_Unknown_WorkOrder_Is_Graceful()
    {
        var before = await S7();
        var none = await Req("tl1", Get, $"{CHT}/handoverable");
        var unknown = await Req("tl1", Get, $"{CHT}/handoverable?workOrderId={await MissingWoIdAsync()}");
        foreach (var r in new[] { none, unknown })
        {
            Rejected(r, 200, 400, 403, 404);
            if (r.Code == 200) r.Items().Should().BeEmpty();
        }

        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_098_Create_Handover_Persists_Pending_Document()
    {
        await CutTenMoreAsync();
        var max = await MaxChIdAsync();

        var r = await Req("tl1", Post, CHT, Ol3Hand(10));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "handoverCode").Should().NotBeNullOrWhiteSpace();
        var ch = (await ChAfterAsync(max)).Should().ContainSingle().Subject;
        ch.Status.Should().Be(HandoverStatuses.Pending);
        ch.ConfirmedBy.Should().BeNull();
        ch.WorkOrderId.Should().Be(K.Wo2.WorkOrderId);
        NearVnNow(ch.HandedAt, "HandedAt");
        var line = (await ChlsAsync(ch.CutHandoverId)).Should().ContainSingle().Subject;
        line.HandedQty.Should().Be(10);
        line.ConfirmedQty.Should().BeNull();
    }

    [Fact]
    public async Task IT_B7_099_Handover_Over_Cut_Is_Rejected()
    {
        await CutTenMoreAsync();
        var before = await S7();
        Rejected(await Req("tl1", Post, CHT, Ol3Hand(11)), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_101_Duplicate_Handover_Lines_Are_Rejected()
    {
        await CutTenMoreAsync();
        var before = await S7();
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 3), HL(K.Ol3.OrderLineId, 3))), 400, 409);
        await Same7(before);
    }

    [Theory]
    [InlineData("IT_B7_102", 0L)]
    [InlineData("IT_B7_102", -1L)]
    [InlineData("IT_B7_102", 2147483648L)]
    [InlineData("IT_B7_102", null)]
    public async Task HandedQty_Boundary(string caseId, long? qty)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        object body = qty is null
            ? HandoverBody(K.Wo2.WorkOrderId)
            : new { WorkOrderId = K.Wo2.WorkOrderId, Lines = new[] { new { OrderLineId = K.Ol3.OrderLineId, ProductComponentMaterialId = (int?)null, HandedQty = qty.Value } } };
        Rejected(await Req("tl1", Post, CHT, body), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_103_Handover_Lines_Without_Cut_Or_Foreign_Are_Rejected()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol4.OrderLineId, 1))), 400, 404);
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(D.Ol9.OrderLineId, 1))), 400, 404);
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo2.WorkOrderId, HL(K.Ol3.OrderLineId, 1, 999999))), 400, 404);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_104_Handover_For_Cancelled_Or_Uncut_WorkOrder_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo5.WorkOrderId, HL(K.Ol3.OrderLineId, 1))), 400, 409);
        Rejected(await Req("tl1", Post, CHT, HandoverBody(K.Wo1.WorkOrderId, HL(K.Ol3.OrderLineId, 1))), 400, 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_107_Cancel_Pending_Handover_Frees_Quantity()
    {
        var r = await Req("tl1", Post, $"{CHT}/{K.Ch1.CutHandoverId}/cancel");

        r.Code.Should().Be(200, r.ToString());
        (await ChAsync(K.Ch1.CutHandoverId)).Status.Should().Be(HandoverStatuses.Cancelled);
        (await ChlsAsync(K.Ch1.CutHandoverId)).Should().HaveCount(1);
        (await HandoverableAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Remaining.Should().Be(30);
    }

    [Fact]
    public async Task IT_B7_108_Cancel_Finished_Handovers_Returns_409()
    {
        var before = await S7();
        foreach (var id in new[] { K.Ch2.CutHandoverId, K.ChRej.CutHandoverId, K.ChCan.CutHandoverId })
            Rejected(await Req("tl1", Post, $"{CHT}/{id}/cancel"), 409);
        await Same7(before);
    }

    // ---------------------------------------------------------------- 110–126 thủ kho

    [Fact]
    public async Task IT_B7_110_Confirm_Posts_Stock_And_Stamps_Confirmer()
    {
        var stock = await BtpTotalAsync(K.Ol3.OrderLineId);
        var st = await MaxStAsync();

        var r = await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30)));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "confirmedByName").Should().Be(S0.Wh.Employee.FullName);
        var ch = await ChAsync(K.Ch1.CutHandoverId);
        ch.Status.Should().Be(HandoverStatuses.Confirmed);
        ch.ConfirmedBy.Should().Be(S0.Wh.EmployeeId);
        NearVnNow(ch.ConfirmedAt, "ConfirmedAt");
        (await ChlsAsync(K.Ch1.CutHandoverId)).Single().ConfirmedQty.Should().Be(30);
        (await BtpTotalAsync(K.Ol3.OrderLineId)).Should().Be(stock + 30);
        (await ChStAsync(K.Ch1.CutHandoverId)).Should().NotBeEmpty().And.OnlyContain(t => t.StockTransactionId > st);
    }

    [Fact]
    public async Task IT_B7_111_Confirm_With_Discrepancy_Stocks_Only_Confirmed_Qty()
    {
        var stock = await BtpTotalAsync(K.Ol3.OrderLineId);

        var r = await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 28, "Thiếu 2 chi tiết")));

        r.Code.Should().Be(200, r.ToString());
        var line = (await ChlsAsync(K.Ch1.CutHandoverId)).Single();
        line.ConfirmedQty.Should().Be(28);
        line.DiscrepancyReason.Should().Be("Thiếu 2 chi tiết");
        (await BtpTotalAsync(K.Ol3.OrderLineId)).Should().Be(stock + 28);
    }

    [Theory]
    [InlineData("IT_B7_112", null)]
    [InlineData("IT_B7_112", "")]
    public async Task IT_B7_112_Discrepancy_Requires_Reason(string caseId, string? reason)
    {
        caseId.Should().StartWith("IT_B7_");
        var before = await S7();
        Rejected(await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 28, reason))), 400);
        await Same7(before);
        (await ChAsync(K.Ch1.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Theory]
    [InlineData("IT_B7_113", 31L, "400")]
    [InlineData("IT_B7_113", -1L, "400")]
    [InlineData("IT_B7_113", 0L, "200|400")]
    [InlineData("IT_B7_113", 2147483648L, "400")]
    public async Task ConfirmedQty_Boundary(string caseId, long qty, string expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var body = new { Lines = new[] { new { CutHandoverLineId = K.Chl1.CutHandoverLineId, ConfirmedQty = qty, DiscrepancyReason = (string?)"Không nhận" } } };

        var r = await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), body);

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(expected.Split('|').Select(int.Parse).ToArray(), r.ToString());
        NoLeak(r);
        var line = (await ChlsAsync(K.Ch1.CutHandoverId)).Single();
        if (r.Code != 200) line.ConfirmedQty.Should().BeNull();
        else line.ConfirmedQty.Should().BeInRange(0, line.HandedQty);
    }

    [Theory]
    [InlineData("IT_B7_114", 500, 200)]
    [InlineData("IT_B7_114", 501, 400)]
    public async Task DiscrepancyReason_Length(string caseId, int length, int expected)
    {
        caseId.Should().StartWith("IT_B7_");
        var reason = Vn(length).Trim().PadRight(length, 'a');

        var r = await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 28, reason)));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        var line = (await ChlsAsync(K.Ch1.CutHandoverId)).Single();
        if (expected == 200) line.DiscrepancyReason.Should().Be(reason);
        else line.ConfirmedQty.Should().BeNull();
    }

    [Fact]
    public async Task IT_B7_115_Confirm_Must_Cover_Every_Line()
    {
        await AddLayAsync(K.Wo1.WorkOrderId, "AFTERNOON", 1, (K.Ol3.OrderLineId, null, 20), (K.Ol4.OrderLineId, null, 10));
        var (ch, lines) = await AddHandoverAsync(K.Wo1.WorkOrderId, (K.Ol3.OrderLineId, null, 5), (K.Ol4.OrderLineId, null, 5));
        var before = await S7();

        Rejected(await Req("warehouse", Post, Confirm(ch.CutHandoverId), ConfirmBody(CF(lines[0].CutHandoverLineId, 5))), 400);
        Rejected(await Req("warehouse", Post, Confirm(ch.CutHandoverId), ConfirmBody()), 400);

        await Same7(before);
        (await ChAsync(ch.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_116_Line_Of_Another_Handover_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl9.CutHandoverLineId, 4))), 400, 404);
        await Same7(before);
        (await ChAsync(K.Ch9.CutHandoverId)).Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_117_Repeated_Line_In_Confirm_Is_Rejected()
    {
        var before = await S7();
        Rejected(await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 15), CF(K.Chl1.CutHandoverLineId, 15))), 400);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_118_Confirm_Twice_Returns_409_And_No_Double_Stock()
    {
        var before = await S7();
        var lineId = (await ChlsAsync(K.Ch2.CutHandoverId)).Single().CutHandoverLineId;
        Rejected(await Req("warehouse", Post, Confirm(K.Ch2.CutHandoverId), ConfirmBody(CF(lineId, 10))), 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_120_Last_Confirm_Completes_WorkOrder()
    {
        await CutTenMoreAsync();
        var (ch3, lines3) = await AddHandoverAsync(K.Wo2.WorkOrderId, (K.Ol3.OrderLineId, null, 10));

        var first = await Req("warehouse", Post, Confirm(K.Ch1.CutHandoverId), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 30)));
        first.Code.Should().Be(200, first.ToString());
        (await WoAsync(K.Wo2.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cutting, "chưa đủ 50 BTP vào kho");

        var last = await Req("warehouse", Post, Confirm(ch3.CutHandoverId), ConfirmBody(CF(lines3[0].CutHandoverLineId, 10)));

        last.Code.Should().Be(200, last.ToString());
        var wo = await WoAsync(K.Wo2.WorkOrderId);
        wo.Status.Should().Be(WorkOrderStatuses.Completed);
        NearVnNow(wo.CompletedAt, "CompletedAt");
    }

    [Fact]
    public async Task IT_B7_122_Reject_Pending_Handover_Keeps_Stock_And_Frees_Quantity()
    {
        var stock = await BtpTotalAsync(K.Ol3.OrderLineId);
        var st = await MaxStAsync();

        var r = await Req("warehouse", Post, Reject(K.Ch1.CutHandoverId), new { Reason = "Sai size, đóng gói lẫn" });

        r.Code.Should().Be(200, r.ToString());
        var ch = await ChAsync(K.Ch1.CutHandoverId);
        ch.Status.Should().Be(HandoverStatuses.Rejected);
        ch.RejectReason.Should().Be("Sai size, đóng gói lẫn");
        Note($"ConfirmedBy={ch.ConfirmedBy}, ConfirmedAt={ch.ConfirmedAt}");
        (await BtpTotalAsync(K.Ol3.OrderLineId)).Should().Be(stock);
        (await MaxStAsync()).Should().Be(st);
        (await HandoverableAsync(K.Wo2.WorkOrderId, K.Ol3.OrderLineId)).Remaining.Should().Be(30);
    }

    [Theory]
    [InlineData("IT_B7_123", "empty", 400)]
    [InlineData("IT_B7_123", "null", 400)]
    [InlineData("IT_B7_123", "nobody", 400)]
    [InlineData("IT_B7_123", "len500", 200)]
    [InlineData("IT_B7_123", "len501", 400)]
    public async Task Reject_Reason_Boundary(string caseId, string kind, int expected)
    {
        caseId.Should().StartWith("IT_B7_");
        object? body = kind switch
        {
            "empty" => new { Reason = "" },
            "null" => new { Reason = (string?)null },
            "len500" => new { Reason = Vn(500).Trim().PadRight(500, 'a') },
            "len501" => new { Reason = Vn(501).Trim().PadRight(501, 'a') },
            _ => null,
        };

        var r = await Req("warehouse", Post, Reject(K.Ch1.CutHandoverId), body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        (await ChAsync(K.Ch1.CutHandoverId)).Status.Should().Be(expected == 200 ? HandoverStatuses.Rejected : HandoverStatuses.Pending);
    }

    [Fact]
    public async Task IT_B7_124_Reject_Finished_Handovers_Returns_409()
    {
        var before = await S7();
        foreach (var id in new[] { K.Ch2.CutHandoverId, K.ChCan.CutHandoverId })
            Rejected(await Req("warehouse", Post, Reject(id), new { Reason = "x" }), 409);
        await Same7(before);
    }

    [Fact]
    public async Task IT_B7_126_Confirm_And_Reject_Missing_Or_NonInteger_Id_Return_404()
    {
        var before = await S7();
        var missing = await MissingChIdAsync();
        Rejected(await Req("warehouse", Post, Confirm(missing), ConfirmBody(CF(K.Chl1.CutHandoverLineId, 1))), 404);
        Rejected(await Req("warehouse", Post, Reject(missing), new { Reason = "x" }), 404);
        Rejected(await Req("warehouse", Post, $"{CHW}/abc/confirm", ConfirmBody()), 404);
        Rejected(await Req("warehouse", Post, $"{CHW}/abc/reject", new { Reason = "x" }), 404);
        await Same7(before);
    }
}
