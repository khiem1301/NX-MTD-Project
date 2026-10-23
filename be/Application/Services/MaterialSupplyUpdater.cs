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
    }

    /// <summary>AWAITING (chưa nhận gì) / PARTIAL / COMPLETE (mọi NVL của đơn đã nhận đủ hoặc đã chốt không bù).</summary>
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
        var notReplenished = await _uow.Repository<PurchaseOrderLine>().Query()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value) && l.NotReplenishedQty > 0)
            .GroupBy(l => l.OrderMaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.NotReplenishedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var complete = materials.All(m =>
            received.GetValueOrDefault(m.OrderMaterialId) + notReplenished.GetValueOrDefault(m.OrderMaterialId) >= m.RequiredQty);
        var anyReceived = materials.Any(m => received.GetValueOrDefault(m.OrderMaterialId) > 0);
        order.MaterialStatus = complete
            ? OrderMaterialStatuses.Complete
            : anyReceived ? OrderMaterialStatuses.Partial : OrderMaterialStatuses.Awaiting;
    }
}
