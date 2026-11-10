using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Tính lại trạng thái hàng của đơn mua (<c>PurchaseOrder.GoodsStatus</c>) và <c>CustomerOrder.MaterialStatus</c> sau khi
/// phiếu nhập được xác nhận hoặc Quản đốc chốt "không bù". Chỉ sửa entity đang theo dõi, KHÔNG SaveChanges; gọi SAU KHI
/// phiếu đã được lưu trạng thái CONFIRMED (cùng transaction) để các tổng đọc từ DB đã gồm phiếu đó.
/// </summary>
public sealed class MaterialSupplyUpdater
{
    private readonly IUnitOfWork _uow;

    public MaterialSupplyUpdater(IUnitOfWork uow) => _uow = uow;

    /// <summary>Số đã nhận (phiếu CONFIRMED) theo từng dòng đơn mua.</summary>
    public async Task<Dictionary<int, decimal>> ReceivedByPurchaseLineAsync(IReadOnlyCollection<int> purchaseLineIds,
        CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query()
            .Where(l => l.PurchaseOrderLineId != null && purchaseLineIds.Contains(l.PurchaseOrderLineId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    /// <summary>
    /// NVL khách cấp mà Quản đốc đã chốt "không bù" (hủy giao phần thiếu): Thủ kho không lập thêm phiếu nhập cho các NVL này.
    /// Phiếu mua từ nhà cung cấp đã chặn bằng <c>NotReplenishedQty</c> nên chỉ tính phiếu khách cấp.
    /// </summary>
    public async Task<HashSet<int>> ClosedWithoutReplenishAsync(IReadOnlyCollection<int> orderMaterialIds, CancellationToken ct) =>
        (await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(l => l.Request.RequestTypeCode == RequestTypes.ShortDelivery
                        && l.Request.ShortageResolution == ShortageResolutions.NoReplenish
                        && l.MaterialReceiptLine != null && l.MaterialReceiptLine.PurchaseOrderLineId == null
                        && l.MaterialReceiptLine.OrderMaterialId != null
                        && orderMaterialIds.Contains(l.MaterialReceiptLine.OrderMaterialId.Value))
            .Select(l => l.MaterialReceiptLine!.OrderMaterialId!.Value)
            .Distinct().ToListAsync(ct)).ToHashSet();

    public async Task RecalculatePurchaseOrderAsync(int purchaseOrderId, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query().Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);
        if (po is null || !PurchaseGoodsStatuses.Placed.Contains(po.GoodsStatus)) return;

        var received = await ReceivedByPurchaseLineAsync(po.PurchaseOrderLines.Select(l => l.PurchaseOrderLineId).ToList(), ct);
        var lines = po.PurchaseOrderLines.ToList();
        var allSettled = lines.All(l => received.GetValueOrDefault(l.PurchaseOrderLineId) + l.NotReplenishedQty >= l.OrderedQty);
        var anyReceived = lines.Any(l => received.GetValueOrDefault(l.PurchaseOrderLineId) > 0);
        var anyNotReplenished = lines.Any(l => l.NotReplenishedQty > 0);

        po.GoodsStatus = allSettled
            ? (anyNotReplenished ? PurchaseGoodsStatuses.ClosedShort : PurchaseGoodsStatuses.Received)
            : (anyReceived ? PurchaseGoodsStatuses.PartiallyReceived : PurchaseGoodsStatuses.Ordered);

        // Chốt không bù: phần còn lại không trả thêm → thanh toán một phần thành đã thanh toán.
        if (po.GoodsStatus == PurchaseGoodsStatuses.ClosedShort
            && po.PaymentStatus == PurchasePaymentStatuses.PartiallyPaid)
            po.PaymentStatus = PurchasePaymentStatuses.Paid;
    }

    /// <summary>
    /// AWAITING (chưa nhận gì) / PARTIAL / COMPLETE (mọi NVL của đơn đã nhận đủ số cần). Chốt "không bù" NVL mua từ NCC chỉ
    /// đóng đơn mua (CLOSED_SHORT), không làm đơn hàng thành COMPLETE: số cần chưa đủ thì vẫn PARTIAL (swimlane: Partially Received).
    /// </summary>
    public async Task RecalculateMaterialStatusAsync(int orderId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return;
        var materials = await _uow.Repository<OrderMaterial>().Query().Where(m => m.OrderId == orderId)
            .Select(m => new { m.OrderMaterialId, m.RequiredQty }).ToListAsync(ct);
        if (materials.Count == 0) return;

        var ids = materials.Select(m => m.OrderMaterialId).ToList();
        var received = await _uow.Repository<MaterialReceiptLine>().Query()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OrderMaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var complete = materials.All(m => received.GetValueOrDefault(m.OrderMaterialId) >= m.RequiredQty);
        var anyReceived = materials.Any(m => received.GetValueOrDefault(m.OrderMaterialId) > 0);
        order.MaterialStatus = complete
            ? OrderMaterialStatuses.Complete
            : anyReceived ? OrderMaterialStatuses.Partial : OrderMaterialStatuses.Awaiting;
    }
}
