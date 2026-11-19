using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Giá trị thực tế của đơn mua và trạng thái thanh toán theo các lần thanh toán (DEV-ALL-117).</summary>
public static class PurchaseAmounts
{
    /// <summary>Giá trị tự tính: phần đã chốt không bù không tính (đơn chưa chốt thiếu thì bằng giá trị đơn mua).</summary>
    public static decimal Computed(IEnumerable<PurchaseOrderLine> lines) =>
        lines.Sum(l => (l.OrderedQty - l.NotReplenishedQty) * l.UnitPrice);

    /// <summary>Giá trị thực tế: số Kế toán nhập (nếu có) hoặc giá trị tự tính. Cần nạp sẵn <see cref="PurchaseOrder.PurchaseOrderLines"/>.</summary>
    public static decimal Actual(PurchaseOrder po) => po.ActualAmountOverride ?? Computed(po.PurchaseOrderLines);

    public static decimal Paid(PurchaseOrder po) => po.PurchasePayments.Sum(p => p.Amount);

    /// <summary>
    /// Đồng bộ tổng đã trả, thông tin lần trả gần nhất và trạng thái thanh toán của đơn theo các dòng thanh toán.
    /// Đơn chưa có lần trả nào thì giữ nguyên trạng thái.
    /// </summary>
    public static void Sync(PurchaseOrder po)
    {
        var last = po.PurchasePayments.OrderBy(p => p.CreatedAt).ThenBy(p => p.PurchasePaymentId).LastOrDefault();
        if (last is null) return;
        var paid = Paid(po);
        po.PaidAmount = paid;
        po.PaymentDate = last.PaymentDate;
        po.PaymentMethod = last.PaymentMethod;
        po.PaymentNote = last.PaymentNote;
        po.PaymentStatus = paid >= Actual(po) ? PurchasePaymentStatuses.Paid : PurchasePaymentStatuses.PartiallyPaid;
    }
}
