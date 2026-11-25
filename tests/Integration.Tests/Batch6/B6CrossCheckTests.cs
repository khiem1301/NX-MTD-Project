using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_122–124 — đối chiếu chéo thẻ kho, quy ước HOA/thường, không dữ liệu nửa vời.</summary>
public sealed class B6CrossCheckTests(ITestOutputHelper output) : B6TestBase(output)
{
    [Fact]
    public async Task IT_B6_122_OnHand_Equals_Sum_Of_Ledger_And_Latest_BalanceAfter()
    {
        (await Confirm(R.Mr1.MaterialReceiptId)).Code.Should().Be(200);
        (await Confirm(R.Mr2.MaterialReceiptId)).Code.Should().Be(200);
        var supp = await Call("warehouse", Post, MRW + "/supplementary", SuppBody());
        supp.Code.Should().Be(200, supp.ToString());
        await SeedUploadAsync(NX_MTD.Domain.Constants.AttachmentOwnerTypes.MaterialReceipt, ReceiptIdOf(supp), "warehouse", "DOCUMENT_PHOTO");
        (await Confirm(ReceiptIdOf(supp))).Code.Should().Be(200);

        await using var db = NewDb();
        var balances = await db.StockBalances.AsNoTracking().ToListAsync();
        var txs = await db.StockTransactions.AsNoTracking().ToListAsync();
        foreach (var sb in balances)
        {
            var mine = txs.Where(t => t.StockBalanceId == sb.StockBalanceId).OrderBy(t => t.StockTransactionId).ToList();
            if (mine.Count == 0) continue;
            sb.OnHandQty.Should().Be(mine.Sum(t => t.QtyChange), $"StockBalance {sb.StockBalanceId}: OnHandQty = tổng thẻ kho");
            mine[^1].BalanceAfter.Should().Be(sb.OnHandQty, $"StockBalance {sb.StockBalanceId}: BalanceAfter mới nhất");
            decimal running = 0;
            foreach (var t in mine) { running += t.QtyChange; t.BalanceAfter.Should().Be(running); }
        }
    }

    [Fact]
    public async Task IT_B6_123_Status_And_Code_Values_Are_Upper_Case()
    {
        (await Call("warehouse", Post, MRW, CustBody())).Code.Should().Be(200);
        (await Confirm(R.Mr2.MaterialReceiptId)).Code.Should().Be(200);
        (await ReturnMr(R.Mr1.MaterialReceiptId, new { Reason = "x" })).Code.Should().Be(200);
        (await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 10m)], "n", 1, [T.AddDays(5)]))).Code.Should().Be(200);
        (await SeedUploadAsync(NX_MTD.Domain.Constants.AttachmentOwnerTypes.MaterialReceipt, R.Mr2.MaterialReceiptId) > 0).Should().BeTrue();

        await using var db = NewDb();
        var values = new List<string?>();
        values.AddRange(await db.MaterialReceipts.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.MaterialReceipts.AsNoTracking().Select(x => x.Source).Distinct().ToListAsync());
        values.AddRange(await db.Requests.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync());
        values.AddRange(await db.Requests.AsNoTracking().Select(x => x.ShortageResolution).Distinct().ToListAsync());
        values.AddRange(await db.Requests.AsNoTracking().Select(x => x.RequestTypeCode).Distinct().ToListAsync());
        values.AddRange(await db.Attachments.AsNoTracking().Select(x => x.Purpose).Distinct().ToListAsync());
        values.Where(v => v is not null).Should().OnlyContain(v => v == v!.ToUpperInvariant(), "mọi mã trạng thái lưu VIẾT HOA");
    }

    [Fact]
    public async Task IT_B6_124_Failed_Calls_Leave_No_Partial_Data_Or_Audit_Or_Ledger_Rows()
    {
        (await Confirm(R.Mr1.MaterialReceiptId)).Code.Should().Be(200);
        var audit = await AuditMaxAsync();
        var stMax = await MaxStAsync();
        var before = await Snap6();

        var dup = await Call("warehouse", Post, MRW, CustBody([CL(R.Om1.OrderMaterialId, 10m), CL(R.Om1.OrderMaterialId, 20m)]));
        var again = await Confirm(R.Mr1.MaterialReceiptId);
        var qty = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 101m)]));
        var nofile = await Upload("warehouse", NX_MTD.Domain.Constants.AttachmentOwnerTypes.MaterialReceipt, R.Mr2.MaterialReceiptId, "PHOTO", null);

        Graceful(dup, 400, 409);
        Graceful(again, 409);
        Graceful(qty, 400);
        Graceful(nofile, 400);
        (await AuditMaxAsync()).Should().Be(audit);
        (await MaxStAsync()).Should().Be(stMax);
        await Unchanged6(before);
    }
}
