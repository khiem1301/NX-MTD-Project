using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6 — ca đồng thời / fault-injection (connection độc lập, COMMIT, tự dọn): mỗi ca chạy <c>Rounds</c> vòng.</summary>
public sealed class B6ConcurrencyTests(ITestOutputHelper output) : B6ConcurrentTestBase(output)
{
    private static readonly string[] Leaks = ["UQ_", "UX_", "FK_", "trg_", "usp_Stock_Post", "SqlException", "StackTrace", "   at "];

    private static void NoLeaks(params Api[] results)
    {
        foreach (var r in results) r.Body.Should().NotContainAny(Leaks);
    }

    private async Task<int> CountMrWithNoteAsync(string tag)
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().CountAsync(x => x.Note == tag);
    }

    private async Task<int> CountMrAsync()
    {
        await using var db = NewDb();
        return await db.MaterialReceipts.AsNoTracking().CountAsync();
    }

    [Fact]
    public async Task IT_B6_031_Concurrent_Creates_Get_Distinct_Receipt_Codes()
    {
        var wk = await TkWk();
        for (var i = 0; i < Rounds; i++)
        {
            var tag = "C31-" + Guid.NewGuid().ToString("N")[..8];
            var bodies = new List<Dictionary<string, object?>>();
            for (var k = 0; k < 5; k++)
            {
                var b = CustBody(tag);
                b["Lines"] = new[] { CL(await NewOmAsync(), 10m) };
                bodies.Add(b);
            }

            var results = await RaceAsync(bodies.Select<Dictionary<string, object?>, Func<Task<Api>>>(b => () => PostAsync(MRW, wk, b)).ToArray());

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            var codes = results.Where(IsSuccess).Select(r => B1Json.Str(r.Data, "receiptCode")).ToList();
            codes.Should().OnlyHaveUniqueItems($"vòng {i}");
            await using var db = NewDb();
            var mrs = await db.MaterialReceipts.AsNoTracking().Where(x => x.Note == tag).ToListAsync();
            mrs.Should().HaveCount(codes.Count);
            foreach (var mr in mrs) (await MrlAsync(mr.MaterialReceiptId)).Should().HaveCount(1, "không có phiếu mồ côi / thiếu dòng");
        }
    }

    [Fact]
    public async Task IT_B6_032_Fault_In_Line_Insert_Rolls_Back_Whole_Receipt()
    {
        var wk = await TkWk();
        var tag = "C32-" + Guid.NewGuid().ToString("N")[..8];
        var before = await CountMrAsync();
        var body = CustBody(tag);
        body["Lines"] = new[] { CL(R.Om1.OrderMaterialId, 10m), CL(R.Om2.OrderMaterialId, 10m) };

        await WithTriggerAsync("MaterialReceiptLine", "trg_it_b6_mrl_fault", async () =>
        {
            var r = await PostAsync(MRW, wk, body);
            Out.WriteLine($"[?] fault create -> {r.Code}");
            FailedGracefully(r);
            NoLeaks(r);
        });

        (await CountMrWithNoteAsync(tag)).Should().Be(0);
        (await CountMrAsync()).Should().Be(before);
    }

    [Fact]
    public async Task IT_B6_033_Mismatched_Line_Material_Is_Blocked_By_Db_Or_Handled_On_Confirm()
    {
        var (mr, _) = await NewPendingAsync();
        var onHand = await OnHandAsync(B.Ma1.MaterialId);
        Exception? error = null;
        try
        {
            await using var db = NewDb();
            db.MaterialReceiptLines.Add(new MaterialReceiptLine
            {
                MaterialReceiptId = mr.MaterialReceiptId, OrderMaterialId = R.Om2.OrderMaterialId, MaterialId = B.Ma1.MaterialId, AcceptedQty = 5m,
            });
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) { error = e; }

        if (error is not null)
        {
            (await MrlAsync(mr.MaterialReceiptId)).Should().HaveCount(1, "trigger chặn dòng lệch NVL");
            return;
        }

        var r = await ConfirmAsync(mr.MaterialReceiptId, await TkMgr());
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(400, 409);
        NoLeaks(r);
        (await MrAsync(mr.MaterialReceiptId)).Status.Should().Be(ReceiptStatuses.Pending);
        (await OnHandAsync(B.Ma1.MaterialId)).Should().Be(onHand);
    }

    [Fact]
    public async Task IT_B6_039_Concurrent_Updates_Keep_One_Consistent_Line()
    {
        var wk = await TkWk();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, ln) = await NewPendingAsync(100m);
            var results = await RaceAsync(
                () => PutAsync($"{MRW}/{mr.MaterialReceiptId}", wk, BodyFor(ln[0], 120m)),
                () => PutAsync($"{MRW}/{mr.MaterialReceiptId}", wk, BodyFor(ln[0], 130m)));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
            results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1);
            var lines = await MrlAsync(mr.MaterialReceiptId);
            lines.Should().ContainSingle($"vòng {i}");
            lines[0].AcceptedQty.Should().BeOneOf(120m, 130m);
            Out.WriteLine($"[?] PUT song song không dùng RowVer: {Summary(results)}");
        }
    }

    [Fact]
    public async Task IT_B6_043_Concurrent_Update_And_Confirm_Keep_Ledger_Equal_To_Final_Lines()
    {
        var wk = await TkWk();
        var mgr = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, ln) = await NewPendingAsync(40m);
            var results = await RaceAsync(
                () => PutAsync($"{MRW}/{mr.MaterialReceiptId}", wk, BodyFor(ln[0], 55m)),
                () => ConfirmAsync(mr.MaterialReceiptId, mgr));

            AssertRaceNoServerError(results);
            var final = await MrAsync(mr.MaterialReceiptId);
            var lines = await MrlAsync(mr.MaterialReceiptId);
            var tx = await StAsync(mr.MaterialReceiptId);
            if (final.Status == ReceiptStatuses.Confirmed)
                tx.Sum(t => t.QtyChange).Should().Be(lines.Sum(l => l.AcceptedQty), $"vòng {i}: thẻ kho khớp dòng phiếu tại thời điểm xác nhận. {Summary(results)}");
            else
                tx.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task IT_B6_047_Concurrent_Confirm_Posts_Stock_Once()
    {
        var mgr = await TkMgr();
        var adm = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, _) = await NewPendingAsync(40m);
            var onHand = await OnHandAsync(Mat);
            var results = await RaceAsync(() => ConfirmAsync(mr.MaterialReceiptId, mgr), () => ConfirmAsync(mr.MaterialReceiptId, adm));

            OneWinner(results, 409);
            (await OnHandAsync(Mat)).Should().Be(onHand + 40m, $"vòng {i}: chỉ tăng một lần");
            (await StAsync(mr.MaterialReceiptId)).Should().ContainSingle();
        }
    }

    [Fact]
    public async Task IT_B6_048_Concurrent_Confirm_Creates_One_ShortDelivery()
    {
        var mgr = await TkMgr();
        var adm = await TkAdm();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, _) = await NewPendingAsync(10m, supplier: true);
            var onHand = await OnHandAsync(Mat);
            var results = await RaceAsync(() => ConfirmAsync(mr.MaterialReceiptId, mgr), () => ConfirmAsync(mr.MaterialReceiptId, adm));

            OneWinner(results, 409);
            (await ShortOfAsync(mr.MaterialReceiptId)).Should().ContainSingle($"vòng {i}: đúng 1 SHORT_DELIVERY");
            (await OnHandAsync(Mat)).Should().Be(onHand + 10m);
        }
    }

    [Fact]
    public async Task IT_B6_049_Fault_In_Ledger_Insert_Rolls_Back_Confirm() => await FaultInStockPostAsync("StockTransaction");

    [Fact]
    public async Task IT_B6_050_Fault_In_StockBalance_Write_Rolls_Back_Confirm() => await FaultInStockPostAsync("StockBalance");

    private async Task FaultInStockPostAsync(string table)
    {
        var (mr, _) = await NewPendingAsync(40m, lines: 2);
        var rv = await RowVerOfMrAsync(mr.MaterialReceiptId);
        var onHand2 = await OnHandAsync(Mat);
        var onHand3 = await OnHandAsync(Mat2);
        var mgr = await TkMgr();

        await WithTriggerAsync(table, "trg_it_b6_stock_fault", async () =>
        {
            var r = await ConfirmAsync(mr.MaterialReceiptId, mgr);
            Out.WriteLine($"[?] fault confirm ({table}) -> {r.Code}");
            FailedGracefully(r);
            NoLeaks(r);
        });

        var after = await MrAsync(mr.MaterialReceiptId);
        after.Status.Should().Be(ReceiptStatuses.Pending);
        after.ConfirmedBy.Should().BeNull();
        after.RowVer.Should().Equal(rv);
        (await OnHandAsync(Mat)).Should().Be(onHand2);
        (await OnHandAsync(Mat2)).Should().Be(onHand3);
        (await StAsync(mr.MaterialReceiptId)).Should().BeEmpty();
        (await ShortOfAsync(mr.MaterialReceiptId)).Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B6_059_Concurrent_Confirm_And_Return_End_In_One_State()
    {
        var mgr = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, _) = await NewPendingAsync(40m);
            var onHand = await OnHandAsync(Mat);
            var results = await RaceAsync(
                () => ConfirmAsync(mr.MaterialReceiptId, mgr),
                () => PostAsync($"{MRM}/{mr.MaterialReceiptId}/return", mgr, new { Reason = "Sai số lượng" }));

            OneWinner(results, 409);
            var final = await MrAsync(mr.MaterialReceiptId);
            if (final.Status == ReceiptStatuses.Confirmed)
            {
                (await StAsync(mr.MaterialReceiptId)).Should().ContainSingle();
                (await OnHandAsync(Mat)).Should().Be(onHand + 40m);
            }
            else
            {
                final.Status.Should().Be(ReceiptStatuses.Returned);
                (await StAsync(mr.MaterialReceiptId)).Should().BeEmpty();
                (await OnHandAsync(Mat)).Should().Be(onHand);
            }
        }
    }

    [Fact]
    public async Task IT_B6_069_Concurrent_Supplementary_Receipts_Never_Over_Replenish()
    {
        var wk = await TkWk();
        var mgr = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (req, line) = await NewShortAsync(40m, ShortageResolutions.Replenish);
            object Body() => new
            {
                ShortageRequestId = req.RequestId, ReceiptDate = T, Note = (string?)null,
                Lines = new[] { new { RequestLineId = line.RequestLineId, AcceptedQty = 40m } },
            };

            var created = await RaceAsync(() => PostAsync(MRW + "/supplementary", wk, Body()), () => PostAsync(MRW + "/supplementary", wk, Body()));
            AssertRaceNoServerError(created);
            var ids = created.Where(IsSuccess).Select(r => (int)B1Json.Int(r.Data, "materialReceiptId")!).ToList();
            ids.Should().NotBeEmpty(Summary(created));
            var confirms = new List<Api>();
            foreach (var id in ids) confirms.Add(await ConfirmAsync(id, mgr));

            AssertRaceNoServerError(confirms.ToArray());
            var accepted = 0m;
            foreach (var id in ids)
                if ((await MrAsync(id)).Status == ReceiptStatuses.Confirmed) accepted += (await MrlAsync(id)).Sum(l => l.AcceptedQty);
            accepted.Should().BeLessThanOrEqualTo(40m, $"vòng {i}: ReplenishedQty không vượt số giao bù. {Summary(confirms)}");
            var posted = 0m;
            foreach (var id in ids) posted += (await StAsync(id)).Sum(t => t.QtyChange);
            posted.Should().Be(accepted);
        }
    }

    [Fact]
    public async Task IT_B6_084_Concurrent_Resolve_Has_One_Winner()
    {
        var mgr = await TkMgr();
        for (var i = 0; i < Rounds; i++)
        {
            var (req, line) = await NewShortAsync(100m);
            object Replenish() => new
            {
                Resolution = "REPLENISH", Lines = new[] { new { RequestLineId = line.RequestLineId, ReplenishQty = 100m } }, Note = "bù",
                ExpectedDeliveryCount = (short?)1, ExpectedDeliveryDates = new[] { T.AddDays(5) },
            };
            object NoReplenish() => new { Resolution = "NO_REPLENISH", Lines = (object?)null, Note = "không bù", ExpectedDeliveryCount = (short?)null, ExpectedDeliveryDates = (DateOnly[]?)null };

            var results = await RaceAsync(() => PostAsync($"{SDM}/{req.RequestId}/resolve", mgr, Replenish()), () => PostAsync($"{SDM}/{req.RequestId}/resolve", mgr, NoReplenish()));

            OneWinner(results, 409);
            var final = await ReqAsync(req.RequestId);
            final.ShortageResolution.Should().BeOneOf("REPLENISH", "NO_REPLENISH");
            await using var db = NewDb();
            var dsd = await db.DeliveryScheduleDates.AsNoTracking().CountAsync(x => x.RequestId == req.RequestId);
            dsd.Should().Be(final.ShortageResolution == "REPLENISH" ? 1 : 0, "không có lịch giao mồ côi");
        }
    }

    [Fact]
    public async Task IT_B6_086_Fault_In_Schedule_Insert_Rolls_Back_Resolve()
    {
        var (req, line) = await NewShortAsync(100m);
        var rv = (await ReqAsync(req.RequestId)).RowVer;
        var mgr = await TkMgr();
        var body = new
        {
            Resolution = "REPLENISH", Lines = new[] { new { RequestLineId = line.RequestLineId, ReplenishQty = 100m } }, Note = "bù",
            ExpectedDeliveryCount = (short?)1, ExpectedDeliveryDates = new[] { T.AddDays(5) },
        };

        await WithTriggerAsync("DeliveryScheduleDate", "trg_it_b6_dsd_fault", async () =>
        {
            var r = await PostAsync($"{SDM}/{req.RequestId}/resolve", mgr, body);
            Out.WriteLine($"[?] fault resolve -> {r.Code}");
            FailedGracefully(r);
            NoLeaks(r);
        });

        var after = await ReqAsync(req.RequestId);
        after.ShortageResolution.Should().BeNull();
        after.Status.Should().Be(RequestStatuses.PendingProcessing);
        after.RowVer.Should().Equal(rv);
        await using var db = NewDb();
        (await db.RequestLines.AsNoTracking().SingleAsync(x => x.RequestLineId == line.RequestLineId)).ReplenishQty.Should().BeNull();
    }

    [Fact]
    public async Task IT_B6_119_Concurrent_Uploads_Leave_One_Current_Attachment()
    {
        var wk = await TkWk();
        for (var i = 0; i < Rounds; i++)
        {
            var (mr, _) = await NewPendingAsync(photo: false);
            var results = await RaceAsync(() => UploadFor(wk, mr.MaterialReceiptId), () => UploadFor(wk, mr.MaterialReceiptId), () => UploadFor(wk, mr.MaterialReceiptId));

            AssertRaceNoServerError(results);
            results.Should().OnlyContain(r => r.Code == 200, Summary(results));
            var rows = await CurrentAttAsync(mr.MaterialReceiptId);
            rows.Should().HaveCount(3);
            rows.Count(a => a.IsCurrent).Should().Be(1, $"vòng {i}: đúng 1 ảnh hiện hành");
        }
    }

    [Fact]
    public async Task IT_B6_120_Fault_In_Attachment_Insert_Keeps_Previous_Current_Photo()
    {
        var wk = await TkWk();
        var (mr, _) = await NewPendingAsync(photo: false);
        var first = await UploadFor(wk, mr.MaterialReceiptId);
        first.Code.Should().Be(200, first.ToString());
        var firstId = B1Json.Prop(first.Data, "attachmentId")!.Value.GetInt64();

        await WithTriggerAsync("Attachment", "trg_it_b6_att_fault", async () =>
        {
            var r = await UploadFor(wk, mr.MaterialReceiptId);
            Out.WriteLine($"[?] fault upload -> {r.Code}");
            FailedGracefully(r);
            NoLeaks(r);
        });

        var rows = await CurrentAttAsync(mr.MaterialReceiptId);
        rows.Should().ContainSingle();
        rows[0].AttachmentId.Should().Be(firstId);
        rows[0].IsCurrent.Should().BeTrue("ảnh cũ không bị gỡ khi ảnh mới chưa lưu được");
    }

    [Fact]
    public async Task IT_B6_121_Ledger_Rejects_Direct_Update_Delete_And_Insert()
    {
        var (mr, _) = await NewPendingAsync(40m);
        (await ConfirmAsync(mr.MaterialReceiptId, await TkMgr())).Code.Should().Be(200);
        var tx = (await StAsync(mr.MaterialReceiptId)).Single();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var sql in new[]
        {
            $"UPDATE dbo.StockTransaction SET QtyChange = 0 WHERE StockTransactionId = {tx.StockTransactionId}",
            $"DELETE FROM dbo.StockTransaction WHERE StockTransactionId = {tx.StockTransactionId}",
            $"INSERT INTO dbo.StockTransaction (StockBalanceId, SourceDocType, SourceDocId, SourceLineId, QtyChange, BalanceAfter, TransactionDate) VALUES ({tx.StockBalanceId}, 'MATERIAL_RECEIPT', {mr.MaterialReceiptId}, 999999, 1, 1, '2026-11-24')",
        })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var act = async () => await cmd.ExecuteNonQueryAsync();
            await act.Should().ThrowAsync<SqlException>(sql);
        }

        var after = (await StAsync(mr.MaterialReceiptId)).Should().ContainSingle().Subject;
        after.QtyChange.Should().Be(40m);
    }

    [Fact]
    public async Task IT_B6_125_Repeated_Rounds_Have_No_500_No_Leaks_No_Duplicates()
    {
        var wk = await TkWk();
        var mgr = await TkMgr();
        var adm = await TkAdm();
        for (var i = 0; i < 10; i++)
        {
            var before = await CountMrAsync();
            var zero = await PostAsync(MRW, wk, CustBody(q1: 0m));
            zero.ShouldNotBeServerError();
            zero.Code.Should().Be(400);
            NoLeaks(zero);
            (await CountMrAsync()).Should().Be(before);

            var (mr, _) = await NewPendingAsync(10m);
            var onHand = await OnHandAsync(Mat);
            var confirms = await RaceAsync(() => ConfirmAsync(mr.MaterialReceiptId, mgr), () => ConfirmAsync(mr.MaterialReceiptId, adm));
            OneWinner(confirms, 409);
            NoLeaks(confirms);
            (await OnHandAsync(Mat)).Should().Be(onHand + 10m);
            (await StAsync(mr.MaterialReceiptId)).Should().ContainSingle();

            var (req, line) = await NewShortAsync(100m);
            var resolves = await RaceAsync(
                () => PostAsync($"{SDM}/{req.RequestId}/resolve", mgr, new
                {
                    Resolution = "REPLENISH", Lines = new[] { new { RequestLineId = line.RequestLineId, ReplenishQty = 100m } }, Note = "bù",
                    ExpectedDeliveryCount = (short?)1, ExpectedDeliveryDates = new[] { T.AddDays(5) },
                }),
                () => PostAsync($"{SDM}/{req.RequestId}/resolve", adm, new { Resolution = "NO_REPLENISH", Note = "không bù" }));
            OneWinner(resolves, 409);
            NoLeaks(resolves);
            (await ShortOfAsync((await ReqAsync(req.RequestId)).MaterialReceiptId!.Value)).Should().ContainSingle();

            var (up, _) = await NewPendingAsync(photo: false);
            var uploads = await RaceAsync(() => UploadFor(wk, up.MaterialReceiptId), () => UploadFor(wk, up.MaterialReceiptId), () => UploadFor(wk, up.MaterialReceiptId));
            AssertRaceNoServerError(uploads);
            NoLeaks(uploads);
            (await CurrentAttAsync(up.MaterialReceiptId)).Count(a => a.IsCurrent).Should().Be(1);
        }
    }
}
