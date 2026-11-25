using Integration.Tests.Infrastructure;
using Integration.Tests.Batch1;
using FluentAssertions;
using Integration.Tests.Batch4;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>Nền cho ca đồng thời / fault-injection IT_B5: S0 (Batch 1 + B2 + B4 + B5) được COMMIT, request song song dùng connection độc lập, tự dọn sau test.</summary>
public abstract class B5ConcurrentTestBase : B4ConcurrentTestBase
{
    protected B5Context P { get; private set; } = null!;

    protected B5ConcurrentTestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb)
    {
        await base.OnSeededAsync(seeder, seedDb);
        P = await seeder.SeedBatch5BaseAsync(B, D, S0);
    }

    protected static readonly DateOnly T = FixedClock.Default.Today;
    protected static readonly string PO = M + "purchase-orders";
    protected static readonly string TRK = M + "purchase-order-tracking";
    protected static readonly string PAY = AC + "purchase-payments";

    protected static DateOnly[] Dates(int n) => Enumerable.Range(1, n).Select(i => T.AddDays(7 * i)).ToArray();

    protected static object PL(int materialId, decimal qty, decimal price) =>
        new { MaterialId = (int?)materialId, NewMaterialName = (string?)null, NewMaterialUnit = (string?)null, NewMaterialColor = (string?)null, OrderedQty = qty, UnitPrice = price };

    protected Dictionary<string, object?> Mk(int lines = 1, string? note = null) => new()
    {
        ["OrderId"] = null, ["SupplierId"] = B.Su1.SupplierId, ["OrderDate"] = T, ["ExpectedDeliveryCount"] = 1, ["Note"] = note,
        ["Lines"] = lines == 1 ? new[] { PL(B.Ma1.MaterialId, 10m, 10_000m) } : [PL(B.Ma1.MaterialId, 10m, 10_000m), PL(B.Ma3.MaterialId, 20m, 1_000m)],
        ["ExpectedDeliveryDates"] = Dates(1),
    };

    protected static object PLine(int lineId, decimal qty, decimal price) => new { PurchaseOrderLineId = lineId, OrderedQty = qty, UnitPrice = price };

    protected Dictionary<string, object?> PlaceBody(byte[] rowVer, List<PurchaseOrderLine> lines, decimal p1, int count = 2) => new()
    {
        ["SupplierId"] = B.Su1.SupplierId, ["OrderDate"] = T, ["ExpectedDeliveryCount"] = count, ["Note"] = "Chốt",
        ["Lines"] = lines.Select((l, i) => PLine(l.PurchaseOrderLineId, l.OrderedQty, i == 0 ? p1 : l.UnitPrice)).ToArray(),
        ["RowVer"] = rowVer, ["ExpectedDeliveryDates"] = Dates(count),
    };

    protected static Dictionary<string, object?> Conf(byte[] rowVer, decimal paid = 9_600_000m) => new()
    {
        ["PaymentDate"] = T, ["PaidAmount"] = paid, ["PaymentMethod"] = "BankTransfer", ["PaymentNote"] = "UNC", ["RowVer"] = rowVer, ["ActualAmount"] = paid,
    };

    protected static Dictionary<string, object?> Upd(byte[] rowVer, decimal amount) => new()
    {
        ["PaymentDate"] = T.AddDays(-1), ["Amount"] = amount, ["PaymentMethod"] = "Cash", ["PaymentNote"] = "Sửa", ["Reason"] = "Nhập sai", ["RowVer"] = rowVer,
    };

    protected static Dictionary<string, object?> Reason(byte[] rowVer, string reason) => new() { ["Reason"] = reason, ["RowVer"] = rowVer };

    // ------------------------------------------------------------------ dữ liệu riêng từng vòng

    /// <summary>PO mới trong DB: <paramref name="pending"/> = chưa đặt (2 dòng, 2 ngày giao); ngược lại ORDERED/UNPAID 1 dòng 200 × 48000. <paramref name="paid"/> = có 1 lần thanh toán vừa nhập.</summary>
    protected async Task<PurchaseOrder> NewPoAsync(bool pending = false, bool paid = false)
    {
        await using var db = NewDb();
        var po = new PurchaseOrder
        {
            PoCode = "PO-C5-" + Guid.NewGuid().ToString("N")[..10], SupplierId = B.Su1.SupplierId,
            GoodsStatus = pending ? PurchaseOrderStatuses.PendingFinalisation : PurchaseGoodsStatuses.Ordered,
            PaymentStatus = paid ? PurchasePaymentStatuses.PartiallyPaid : PurchaseOrderStatuses.Unpaid,
            OrderDate = pending ? null : T.AddDays(-1), ExpectedDeliveryCount = pending ? null : (short)1, CreatedAt = Clock.Now,
        };
        if (paid) { po.PaidAmount = 3_000_000m; po.PaymentDate = T; po.PaymentMethod = PaymentMethods.BankTransfer; }
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();
        if (pending)
        {
            db.PurchaseOrderLines.AddRange(
                new PurchaseOrderLine { PurchaseOrderId = po.PurchaseOrderId, MaterialId = B.Ma1.MaterialId, OrderedQty = 100m, UnitPrice = 50_000m },
                new PurchaseOrderLine { PurchaseOrderId = po.PurchaseOrderId, MaterialId = B.Ma3.MaterialId, OrderedQty = 500m, UnitPrice = 2_000m });
            db.DeliveryScheduleDates.AddRange(
                new DeliveryScheduleDate { PurchaseOrderId = po.PurchaseOrderId, SeqNo = 1, DeliveryDate = T.AddDays(7) },
                new DeliveryScheduleDate { PurchaseOrderId = po.PurchaseOrderId, SeqNo = 2, DeliveryDate = T.AddDays(14) });
        }
        else
        {
            db.PurchaseOrderLines.Add(new PurchaseOrderLine { PurchaseOrderId = po.PurchaseOrderId, MaterialId = B.Ma1.MaterialId, OrderedQty = 200m, UnitPrice = 48_000m });
            db.DeliveryScheduleDates.Add(new DeliveryScheduleDate { PurchaseOrderId = po.PurchaseOrderId, SeqNo = 1, DeliveryDate = T.AddDays(3) });
        }

        if (paid)
            db.PurchasePayments.Add(new PurchasePayment
            {
                PurchaseOrderId = po.PurchaseOrderId, PaymentDate = T, Amount = 3_000_000m, PaymentMethod = PaymentMethods.BankTransfer, CreatedAt = Clock.Now.AddHours(-1),
            });
        await db.SaveChangesAsync();
        return po;
    }

    protected async Task<PurchaseOrder> PoAsync(int id)
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().SingleAsync(p => p.PurchaseOrderId == id);
    }

    protected async Task<List<PurchaseOrderLine>> PolsAsync(int poId)
    {
        await using var db = NewDb();
        return await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrderId == poId).OrderBy(l => l.PurchaseOrderLineId).ToListAsync();
    }

    protected async Task<List<PurchasePayment>> PaysAsync(int poId)
    {
        await using var db = NewDb();
        return await db.PurchasePayments.AsNoTracking().Where(p => p.PurchaseOrderId == poId).OrderBy(p => p.PurchasePaymentId).ToListAsync();
    }

    protected async Task<List<DeliveryScheduleDate>> PoDsdAsync(int poId)
    {
        await using var db = NewDb();
        return await db.DeliveryScheduleDates.AsNoTracking().Where(d => d.PurchaseOrderId == poId).OrderBy(d => d.SeqNo).ToListAsync();
    }

    protected async Task<int> CountPosAsync()
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().CountAsync();
    }

    protected static int PoIdOf(Api r) => Integration.Tests.Batch1.B1Json.Int(r.Data, "purchaseOrderId") ?? -1;

    // ------------------------------------------------------------------ fault-injection (trigger test-only, luôn gỡ sau khi dùng)

    protected async Task WithTriggerAsync(string table, string name, Func<Task> act)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        async Task Exec(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec($"IF OBJECT_ID('dbo.{name}','TR') IS NOT NULL DROP TRIGGER dbo.{name}");
        await Exec($"CREATE TRIGGER dbo.{name} ON dbo.{table} AFTER INSERT, UPDATE AS BEGIN THROW 51000, 'it-b5 injected fault', 1; END");
        try { await act(); }
        finally { await Exec($"IF OBJECT_ID('dbo.{name}','TR') IS NOT NULL DROP TRIGGER dbo.{name}"); }
    }

    protected static void FailedGracefully(Api r)
    {
        r.Code.Should().BeGreaterThanOrEqualTo(400, "lỗi DB phải khiến request thất bại, không được báo thành công. " + r);
        r.Body.Should().NotContainAny("SqlException", "StackTrace", "   at ", "it-b5 injected fault", "trg_it_b5");
    }
}
