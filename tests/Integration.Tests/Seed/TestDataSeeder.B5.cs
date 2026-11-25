using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>S0 của IT_B5: chỉ bổ sung PO1–PO6, dòng đơn mua, lịch giao, lần thanh toán; Supplier/Material/Order dùng lại từ B2 + B4.</summary>
public sealed partial class TestDataSeeder
{
    public async Task<B5Context> SeedBatch5BaseAsync(B2Context b2, B4Context d, S0Context s0, CancellationToken ct = default)
    {
        var k = new B5Context();

        foreach (var o in await _db.PurchaseOrders.Where(x => x.PoCode.StartsWith("PO-B5-")).ToListAsync(ct))
        {
            MovedKeys.Add(("PurchaseOrder", "PurchaseOrderId", o.PurchaseOrderId, "PoCode", o.PoCode));
            o.PoCode = $"MOVED-{o.PurchaseOrderId}";
        }

        await _db.SaveChangesAsync(ct);

        k.Po1 = Po("PO-B5-01", d.Od2.OrderId, b2.Su1, PurchaseOrderStatuses.PendingFinalisation, PurchaseOrderStatuses.Unpaid, null);
        k.Po2 = Po("PO-B5-02", null, b2.Su2, PurchaseGoodsStatuses.Ordered, PurchaseOrderStatuses.Unpaid, _t.AddDays(-5));
        k.Po3 = Po("PO-B5-03", null, b2.Su1, PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.PartiallyPaid, _t.AddDays(-5));
        k.Po4 = Po("PO-B5-04", null, null, PurchaseGoodsStatuses.Rejected, PurchaseOrderStatuses.Unpaid, null);
        k.Po4.RejectReason = "NCC hết hàng";
        k.Po5 = Po("PO-B5-05", null, b2.Su2, PurchaseGoodsStatuses.PartiallyReceived, PurchaseOrderStatuses.Unpaid, _t.AddDays(-5));
        k.Po6 = Po("PO-B5-06", null, b2.Su1, PurchaseGoodsStatuses.Ordered, PurchasePaymentStatuses.Paid, _t.AddDays(-5));
        foreach (var p in new[] { k.Po2, k.Po3, k.Po5, k.Po6 }) p.ExpectedDeliveryCount = 1;
        k.Po3.PaidAmount = 8_000_000m; k.Po3.PaymentDate = _t.AddDays(-1); k.Po3.PaymentMethod = PaymentMethods.BankTransfer;
        k.Po6.PaidAmount = 2_000_000m; k.Po6.PaymentDate = _t.AddDays(-1); k.Po6.PaymentMethod = PaymentMethods.BankTransfer;
        _db.PurchaseOrders.AddRange(k.Po1, k.Po2, k.Po3, k.Po4, k.Po5, k.Po6);
        await _db.SaveChangesAsync(ct);

        k.Pol1 = Pol(k.Po1, b2.Ma1, d.Om1, 100m, 50_000m);
        k.Pol2 = Pol(k.Po1, b2.Ma3, d.Om2, 500m, 2_000m);
        k.Pol3 = Pol(k.Po2, b2.Ma1, null, 200m, 48_000m);
        k.Pol31 = Pol(k.Po3, b2.Ma1, null, 100m, 100_000m);
        k.Pol5 = Pol(k.Po5, b2.Ma1, null, 100m, 50_000m);
        k.Pol6 = Pol(k.Po6, b2.Ma1, null, 40m, 50_000m);
        _db.PurchaseOrderLines.AddRange(k.Pol1, k.Pol2, k.Pol3, k.Pol31, k.Pol5, k.Pol6);

        _db.DeliveryScheduleDates.AddRange(
            new DeliveryScheduleDate { PurchaseOrderId = k.Po1.PurchaseOrderId, SeqNo = 1, DeliveryDate = _t.AddDays(7) },
            new DeliveryScheduleDate { PurchaseOrderId = k.Po1.PurchaseOrderId, SeqNo = 2, DeliveryDate = _t.AddDays(14) },
            new DeliveryScheduleDate { PurchaseOrderId = k.Po2.PurchaseOrderId, SeqNo = 1, DeliveryDate = _t.AddDays(3) });

        k.Pp1 = Pay(k.Po3, 5_000_000m, _now.AddHours(-30));
        k.Pp2 = Pay(k.Po3, 3_000_000m, _now.AddHours(-1));
        k.Pp9 = Pay(k.Po6, 2_000_000m, _now.AddHours(-1));
        _db.PurchasePayments.AddRange(k.Pp1, k.Pp2, k.Pp9);
        await _db.SaveChangesAsync(ct);

        var receipt = new MaterialReceipt
        {
            ReceiptCode = "GRN-B5-01", PurchaseOrderId = k.Po5.PurchaseOrderId, Source = MaterialSources.Supplier, ReceiptDate = _t.AddDays(-1),
            Status = ReceiptStatuses.Confirmed, SubmittedAt = _now.AddDays(-1), ConfirmedBy = s0.Wh.EmployeeId, ConfirmedAt = _now.AddDays(-1),
        };
        _db.MaterialReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);
        _db.MaterialReceiptLines.Add(new MaterialReceiptLine
        {
            MaterialReceiptId = receipt.MaterialReceiptId, PurchaseOrderLineId = k.Pol5.PurchaseOrderLineId, MaterialId = b2.Ma1.MaterialId, AcceptedQty = 40m,
        });
        await _db.SaveChangesAsync(ct);
        return k;
    }

    private PurchaseOrder Po(string code, int? orderId, Supplier? supplier, string goods, string payment, DateOnly? orderDate) => new()
    {
        PoCode = code, OrderId = orderId, SupplierId = supplier?.SupplierId, GoodsStatus = goods, PaymentStatus = payment,
        OrderDate = orderDate, CreatedAt = _now.AddDays(-5),
    };

    private static PurchaseOrderLine Pol(PurchaseOrder po, Material m, OrderMaterial? om, decimal qty, decimal price) => new()
    {
        PurchaseOrderId = po.PurchaseOrderId, MaterialId = om is null ? m.MaterialId : null, OrderMaterialId = om?.OrderMaterialId, OrderedQty = qty, UnitPrice = price,
    };

    private static PurchasePayment Pay(PurchaseOrder po, decimal amount, DateTime createdAt) => new()
    {
        PurchaseOrderId = po.PurchaseOrderId, PaymentDate = DateOnly.FromDateTime(createdAt), Amount = amount,
        PaymentMethod = PaymentMethods.BankTransfer, CreatedAt = createdAt,
    };
}
