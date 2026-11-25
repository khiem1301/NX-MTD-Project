using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>IT_B7 — ca đồng thời + fault-injection (S0 commit thật, connection độc lập, tự dọn). Mỗi ca chạy <c>Rounds</c> vòng, mỗi vòng dùng dữ liệu riêng.</summary>
public sealed class B7ConcurrencyTests(ITestOutputHelper output) : B7ConcurrentTestBase(output)
{
    private static Func<Task<Api>>[] Times(int n, Func<Task<Api>> call) => Enumerable.Range(0, n).Select(_ => call).ToArray();

    // ---------------------------------------------------------------- kịch bản dùng chung

    private async Task RaceCreateOverCapAsync()
    {
        var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 170);
        var tk = await TkMgr();
        var results = await RaceAsync(
            () => PostAsync(WOM, tk, WoBody(wo.OrderId, [WL(l3.OrderLineId, 30)])),
            () => PostAsync(WOM, tk, WoBody(wo.OrderId, [WL(l3.OrderLineId, 30)])));
        OneWinner(results, 400, 409);
        (await PlanSumOfOrderAsync(wo.OrderId, l3.OrderLineId)).Should().BeLessThanOrEqualTo(200, "race không được làm vượt trần số đặt");
    }

    private async Task RaceLaysAsync()
    {
        var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 100);
        var tk = await TkTl1();
        var results = await RaceAsync(Times(3, () => PostAsync(CLT, tk, LayBody(wo.WorkOrderId, l3.OrderLineId, 1))));
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
        results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1, Summary(results));
        var lays = await LaysAsync(wo.WorkOrderId);
        lays.Should().HaveCount(results.Count(IsSuccess));
        lays.Select(l => l.LaySequence).Should().OnlyHaveUniqueItems("UQ_CL (team, date, shift, seq)");
        (await OrphanCountAsync()).Should().Be(0);
    }

    private async Task RaceConfirmsAsync()
    {
        var (_, l3, ch, chl) = await NewPendingAsync();
        var tk = await TkWk();
        var results = await RaceAsync(Times(2, () => PostAsync($"{CHW}/{ch.CutHandoverId}/confirm", tk, ConfirmBody(chl.CutHandoverLineId, 30))));
        OneWinner(results, 409);
        var done = await ChAsync(ch.CutHandoverId);
        done.Status.Should().Be(HandoverStatuses.Confirmed);
        done.ConfirmedBy.Should().Be(S0.Wh.EmployeeId);
        (await ChStAsync(ch.CutHandoverId)).Should().HaveCount(1, "thẻ kho chỉ được ghi một lần");
        (await BtpOnHandAsync(l3.OrderLineId)).Should().Be(30m, "tồn kho chỉ tăng một lần");
    }

    // ---------------------------------------------------------------- lệnh cắt

    [Fact]
    public async Task IT_B7_041_Parallel_Create_Gives_Distinct_Codes()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (od, _, l4) = await NewOdAsync();
            var tk = await TkMgr();
            var results = await RaceAsync(Times(5, () => PostAsync(WOM, tk, WoBody(od.OrderId, [WL(l4.OrderLineId, 1, K.Pcm1.ProductComponentMaterialId)]))));
            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            var wos = await WoOfOrderAsync(od.OrderId);
            wos.Should().HaveCount(results.Count(IsSuccess));
            wos.Select(w => w.WorkOrderCode).Should().OnlyHaveUniqueItems();
            (await OrphanCountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task IT_B7_042_Parallel_Create_Cannot_Exceed_Ordered_Qty()
    {
        for (var i = 0; i < Rounds; i++) await RaceCreateOverCapAsync();
    }

    [Fact]
    public async Task IT_B7_043_Blocked_Second_Line_Rolls_Back_Whole_WorkOrder()
    {
        var (od, l3, l4) = await NewOdAsync();
        var tk = await TkMgr();
        await WithTriggerAsync("WorkOrderLine", "trg_it_b7_wol", async () =>
        {
            var r = await PostAsync(WOM, tk, WoBody(od.OrderId, [WL(l3.OrderLineId, 5), WL(l4.OrderLineId, 5)]));
            FailedGracefully(r);
        }, "(SELECT COUNT(*) FROM dbo.WorkOrderLine w WHERE w.WorkOrderId IN (SELECT WorkOrderId FROM inserted)) >= 2");
        (await WoOfOrderAsync(od.OrderId)).Should().BeEmpty();
        (await OrphanCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IT_B7_049_Parallel_Update_Has_No_Duplicate_Lines()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 100);
            var tk = await TkMgr();
            var results = await RaceAsync(
                () => PutAsync($"{WOM}/{wo.WorkOrderId}", tk, WoBody(wo.OrderId, [WL(l3.OrderLineId, 100)])),
                () => PutAsync($"{WOM}/{wo.WorkOrderId}", tk, WoBody(wo.OrderId, [WL(l3.OrderLineId, 110)])));
            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            var lines = await WolsAsync(wo.WorkOrderId);
            lines.Should().ContainSingle().Which.PlannedQty.Should().BeOneOf(100, 110);
            Out.WriteLine("[?] Không có khóa lạc quan: " + Summary(results));
        }
    }

    [Fact]
    public async Task IT_B7_057_Cancel_Races_With_First_Lay()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 100);
            var mgr = await TkMgr();
            var tl = await TkTl1();
            var results = await RaceAsync(
                () => PostAsync($"{WOM}/{wo.WorkOrderId}/cancel", mgr),
                () => PostAsync(CLT, tl, LayBody(wo.WorkOrderId, l3.OrderLineId, 5)));
            OneWinner(results, 400, 409);
            var cur = await WoAsync(wo.WorkOrderId);
            var lays = await LaysAsync(wo.WorkOrderId);
            (cur.Status == WorkOrderStatuses.Cancelled && lays.Any(l => l.Status == CuttingLayStatuses.Recorded)).Should().BeFalse("lệnh đã hủy không được có bàn cắt mới");
        }
    }

    [Fact]
    public async Task IT_B7_062_Parallel_Replacement_For_Same_Request_Creates_One_WorkOrder()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (od, l3, _) = await NewOdAsync();
            Request rq;
            await using (var db = NewDb())
            {
                rq = new Request
                {
                    RequestCode = "YC-C7-" + Guid.NewGuid().ToString("N")[..10], RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved,
                    OrderId = od.OrderId, CreatedAt = Clock.Now,
                };
                db.Requests.Add(rq);
                await db.SaveChangesAsync();
                db.RequestLines.Add(new RequestLine
                {
                    RequestId = rq.RequestId, OrderLineId = l3.OrderLineId, ProductComponentMaterialId = K.Pcm1.ProductComponentMaterialId, Quantity = 5m,
                });
                await db.SaveChangesAsync();
            }

            var tk = await TkMgr();
            var body = new { RequestId = rq.RequestId, CuttingTeamId = S0.TCut01.TeamId, DueDate = T.AddDays(3), Note = (string?)null };
            var results = await RaceAsync(Times(2, () => PostAsync($"{WOM}/replacement", tk, body)));
            OneWinner(results, 409);
            var wos = await WoOfOrderAsync(od.OrderId);
            wos.Count(w => w.SourceRequestId == rq.RequestId).Should().Be(1);
        }
    }

    // ---------------------------------------------------------------- bàn cắt

    [Fact]
    public async Task IT_B7_073_Parallel_Lays_Get_Unique_Sequences()
    {
        for (var i = 0; i < Rounds; i++) await RaceLaysAsync();
    }

    [Fact]
    public async Task IT_B7_088_Failed_Fabric_Insert_Rolls_Back_Lay_And_WorkOrder_Status()
    {
        var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 100);
        var tl = await TkTl1();
        await WithTriggerAsync("CuttingLayFabric", "trg_it_b7_clf", async () =>
            FailedGracefully(await PostAsync(CLT, tl, LayBody(wo.WorkOrderId, l3.OrderLineId, 5))));
        (await LaysAsync(wo.WorkOrderId)).Should().BeEmpty();
        (await WoAsync(wo.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Open, "không được chuyển sang CUTTING khi bàn cắt thất bại");
        (await OrphanCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IT_B7_093_Parallel_Lay_Updates_Keep_Single_Consistent_Line()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Open, 100);
            var tl = await TkTl1();
            var created = await PostAsync(CLT, tl, LayBody(wo.WorkOrderId, l3.OrderLineId, 40, "MORNING"));
            created.Code.Should().Be(200, created.ToString());
            var layId = IdOf(created, "cuttingLayId");
            var results = await RaceAsync(
                () => PutAsync($"{CLT}/{layId}", tl, LayBody(wo.WorkOrderId, l3.OrderLineId, 42, "MORNING")),
                () => PutAsync($"{CLT}/{layId}", tl, LayBody(wo.WorkOrderId, l3.OrderLineId, 44, "MORNING")));
            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            (await LayLinesAsync(layId)).Should().ContainSingle().Which.ActualQty.Should().BeOneOf(42, 44);
            (await OrphanCountAsync()).Should().Be(0);
        }
    }

    // ---------------------------------------------------------------- bàn giao

    [Fact]
    public async Task IT_B7_100_Parallel_Handovers_Cannot_Exceed_Cut()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (wo, l3, _, _) = await NewPendingAsync(plan: 50, cut: 50, handed: 40);
            var tl = await TkTl1();
            var results = await RaceAsync(Times(2, () => PostAsync(CHT, tl, HandoverBody(wo.WorkOrderId, l3.OrderLineId, 10))));
            OneWinner(results, 400, 409);
            (await HandedSumAsync(wo.WorkOrderId, l3.OrderLineId)).Should().BeLessThanOrEqualTo(await CutSumAsync(wo.WorkOrderId, l3.OrderLineId));
        }
    }

    [Fact]
    public async Task IT_B7_105_Parallel_Handover_Creation_Gives_Distinct_Codes()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (wo, l3, _, _) = await NewPendingAsync(plan: 50, cut: 50, handed: 10);
            var tl = await TkTl1();
            var results = await RaceAsync(Times(5, () => PostAsync(CHT, tl, HandoverBody(wo.WorkOrderId, l3.OrderLineId, 1))));
            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            var all = await ChOfWoAsync(wo.WorkOrderId);
            all.Should().HaveCount(1 + results.Count(IsSuccess));
            all.Select(h => h.HandoverCode).Should().OnlyHaveUniqueItems();
            (await OrphanCountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task IT_B7_106_Blocked_Second_Handover_Line_Rolls_Back_Header()
    {
        var (wo, l3, _, _) = await NewPendingAsync(plan: 50, cut: 40, handed: 30);
        OrderLine l4;
        await using (var db = NewDb())
        {
            l4 = db.OrderLines.Single(x => x.OrderId == wo.OrderId && x.OrderedQty == 100);
            db.WorkOrderLines.Add(new WorkOrderLine { WorkOrderId = wo.WorkOrderId, OrderLineId = l4.OrderLineId, PlannedQty = 10 });
            await db.SaveChangesAsync();
            var lay = new CuttingLay
            {
                WorkOrderId = wo.WorkOrderId, CuttingTeamId = S0.TCut01.TeamId, WorkDate = T, ShiftCode = "AFTERNOON", LaySequence = (short)(200 + Random.Shared.Next(1, 30_000)),
                Status = CuttingLayStatuses.Recorded, CreatedAt = Clock.Now,
            };
            db.CuttingLays.Add(lay);
            await db.SaveChangesAsync();
            db.CuttingLayLines.Add(new CuttingLayLine { CuttingLayId = lay.CuttingLayId, OrderLineId = l4.OrderLineId, ActualQty = 5 });
            db.CuttingLayFabrics.Add(new CuttingLayFabric { CuttingLayId = lay.CuttingLayId, MaterialId = B.Ma1.MaterialId, UsedM = 1m });
            await db.SaveChangesAsync();
        }

        var before = (await ChOfWoAsync(wo.WorkOrderId)).Count;
        var tl = await TkTl1();
        var body = new
        {
            WorkOrderId = wo.WorkOrderId,
            Lines = new[]
            {
                new { OrderLineId = l3.OrderLineId, ProductComponentMaterialId = (int?)null, HandedQty = 1 },
                new { OrderLineId = l4.OrderLineId, ProductComponentMaterialId = (int?)null, HandedQty = 1 },
            },
        };
        await WithTriggerAsync("CutHandoverLine", "trg_it_b7_chl", async () => FailedGracefully(await PostAsync(CHT, tl, body)),
            "(SELECT COUNT(*) FROM dbo.CutHandoverLine c WHERE c.CutHandoverId IN (SELECT CutHandoverId FROM inserted)) >= 2");

        (await ChOfWoAsync(wo.WorkOrderId)).Should().HaveCount(before, "không có phiếu mới");
        (await OrphanCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IT_B7_109_Cancel_Races_With_Confirm()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (_, l3, ch, chl) = await NewPendingAsync();
            var tl = await TkTl1();
            var wk = await TkWk();
            var results = await RaceAsync(
                () => PostAsync($"{CHT}/{ch.CutHandoverId}/cancel", tl),
                () => PostAsync($"{CHW}/{ch.CutHandoverId}/confirm", wk, ConfirmBody(chl.CutHandoverLineId, 30)));
            OneWinner(results, 400, 409);
            var status = (await ChAsync(ch.CutHandoverId)).Status;
            status.Should().BeOneOf(HandoverStatuses.Cancelled, HandoverStatuses.Confirmed);
            (await ChStAsync(ch.CutHandoverId)).Should().HaveCount(status == HandoverStatuses.Confirmed ? 1 : 0);
            (await BtpOnHandAsync(l3.OrderLineId)).Should().Be(status == HandoverStatuses.Confirmed ? 30m : 0m);
        }
    }

    [Fact]
    public async Task IT_B7_119_Parallel_Confirms_Post_Stock_Once()
    {
        for (var i = 0; i < Rounds; i++) await RaceConfirmsAsync();
    }

    [Fact]
    public async Task IT_B7_121_Stock_Post_Failure_Rolls_Back_Confirm()
    {
        var (wo, l3, ch, chl) = await NewPendingAsync();
        var wk = await TkWk();
        await WithTriggerAsync("StockBalance", "trg_it_b7_sb", async () =>
            FailedGracefully(await PostAsync($"{CHW}/{ch.CutHandoverId}/confirm", wk, ConfirmBody(chl.CutHandoverLineId, 30))));

        var cur = await ChAsync(ch.CutHandoverId);
        cur.Status.Should().Be(HandoverStatuses.Pending);
        cur.ConfirmedBy.Should().BeNull();
        (await ChlsAsync(ch.CutHandoverId)).Single().ConfirmedQty.Should().BeNull();
        (await ChStAsync(ch.CutHandoverId)).Should().BeEmpty();
        (await BtpOnHandAsync(l3.OrderLineId)).Should().Be(0m);
        (await WoAsync(wo.WorkOrderId)).Status.Should().Be(WorkOrderStatuses.Cutting);
    }

    [Fact]
    public async Task IT_B7_125_Confirm_Races_With_Reject()
    {
        for (var i = 0; i < Rounds; i++)
        {
            var (_, l3, ch, chl) = await NewPendingAsync();
            var wk = await TkWk();
            var results = await RaceAsync(
                () => PostAsync($"{CHW}/{ch.CutHandoverId}/confirm", wk, ConfirmBody(chl.CutHandoverLineId, 30)),
                () => PostAsync($"{CHW}/{ch.CutHandoverId}/reject", wk, new { Reason = "Sai size" }));
            OneWinner(results, 400, 409);
            var status = (await ChAsync(ch.CutHandoverId)).Status;
            status.Should().BeOneOf(HandoverStatuses.Confirmed, HandoverStatuses.Rejected);
            (await ChStAsync(ch.CutHandoverId)).Should().HaveCount(status == HandoverStatuses.Confirmed ? 1 : 0);
            (await BtpOnHandAsync(l3.OrderLineId)).Should().Be(status == HandoverStatuses.Confirmed ? 30m : 0m);
        }
    }

    // ---------------------------------------------------------------- lặp 10 vòng

    [Fact]
    public async Task IT_B7_133_Repeated_Races_Never_Return_500_Or_Break_Invariants()
    {
        for (var i = 0; i < 10; i++)
        {
            await RaceCreateOverCapAsync();
            await RaceLaysAsync();
            await RaceConfirmsAsync();
        }

        await using var db = NewDb();
        var dup = db.CuttingLays.GroupBy(l => new { l.CuttingTeamId, l.WorkDate, l.ShiftCode, l.LaySequence }).Count(g => g.Count() > 1);
        dup.Should().Be(0);
    }
}
