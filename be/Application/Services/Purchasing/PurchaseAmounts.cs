using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Mốc trả đủ của đơn mua và trạng thái thanh toán theo các lần thanh toán (DEV-ALL-136).</summary>
public static class PurchaseAmounts
{
    /// <summary>
    /// Mốc "trả đủ": đơn Chốt không bù đã có Tổng giá trị thực tế (DEV-ALL-129) thì lấy số đó, còn lại là tổng giá trị đơn mua.
    /// Cần nạp sẵn <see cref="PurchaseOrder.PurchaseOrderLines"/>.
    /// </summary>
    public static decimal Actual(PurchaseOrder po) =>
        po.GoodsStatus == PurchaseGoodsStatuses.ClosedShort && po.ActualAmount is { } actual
            ? actual
            : po.PurchaseOrderLines.Sum(l => l.OrderedQty * l.UnitPrice);

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
