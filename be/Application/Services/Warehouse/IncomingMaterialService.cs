using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// NVL chờ nhận. Khách cấp: dòng "NVL của đơn" có số khách cấp lớn hơn số đã nhận (theo phiếu nhập đã xác nhận). Nhà cung cấp: dòng đơn mua
/// đã đặt hàng (ORDERED / PARTIALLY_RECEIVED) còn phải nhận. Cùng cách tính "đã nhận" với OrderMaterialService và PurchaseOrderService.
/// </summary>
public sealed class IncomingMaterialService : IIncomingMaterialService
{
    private readonly IUnitOfWork _uow;
    private readonly MaterialSupplyUpdater _supply;

    public IncomingMaterialService(IUnitOfWork uow, MaterialSupplyUpdater supply)
    {
        _uow = uow; _supply = supply;
    }

    public async Task<Result<IncomingMaterialsDto>> GetAsync(CancellationToken ct)
    {
        var customer = await CustomerAsync(ct);
        var supplier = await SupplierAsync(ct);
        var closedShort = await ClosedShortAsync(ct);
        return Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto(customer, supplier, closedShort));
    }

    private async Task<List<IncomingCustomerMaterialDto>> CustomerAsync(CancellationToken ct)
    {
        var closed = OrderStatuses.Closed.ToArray();   // EF không dịch Contains trên IReadOnlySet
        var rows = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => om.CustomerQty > 0 && !closed.Contains(om.Order.Status))
            .Select(om => new
            {
                om.OrderMaterialId, om.OrderId, om.Order.OrderCode, om.Material.MaterialCode, om.Material.MaterialName,
                MaterialColor = om.Material.Color, om.Material.Unit, om.CustomerQty, om.ExpectedDeliveryCount,
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.OrderMaterialId).ToArray();
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OrderMaterialId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var counts = await _uow.Repository<VwOrderMaterialDelivery>().Query().AsNoTracking()
            .Where(v => ids.Contains(v.OrderMaterialId))
            .ToDictionaryAsync(v => v.OrderMaterialId, v => v.ReceivedCount, ct);
        var closedShort = await _supply.ClosedWithoutReplenishAsync(ids, ct);

        return rows
            .Select(r => new IncomingCustomerMaterialDto(r.OrderMaterialId, r.OrderId, r.OrderCode, r.MaterialCode, r.MaterialName,
                r.MaterialColor, r.Unit, r.CustomerQty, received.GetValueOrDefault(r.OrderMaterialId),
                r.CustomerQty - received.GetValueOrDefault(r.OrderMaterialId), r.ExpectedDeliveryCount,
                counts.GetValueOrDefault(r.OrderMaterialId)))
            .Where(r => r.OutstandingQty > 0 && !closedShort.Contains(r.OrderMaterialId))
            .OrderBy(r => r.OrderCode).ThenBy(r => r.MaterialCode)
            .ToList();
    }

    /// <summary>Số lượng đã nhận (phiếu nhập đã xác nhận) theo dòng đơn mua.</summary>
    private Task<Dictionary<int, decimal>> ReceivedByLineAsync(int[] lineIds, CancellationToken ct) =>
        _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.PurchaseOrderLineId != null && lineIds.Contains(l.PurchaseOrderLineId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    /// <summary>Số lần giao đã nhận theo đơn mua.</summary>
    private Task<Dictionary<int, int>> DeliveryCountsAsync(int[] purchaseOrderIds, CancellationToken ct) =>
        _uow.Repository<VwPurchaseOrderDelivery>().Query().AsNoTracking()
            .Where(v => purchaseOrderIds.Contains(v.PurchaseOrderId))
            .ToDictionaryAsync(v => v.PurchaseOrderId, v => v.ReceivedCount, ct);

    /// <summary>Dòng của đơn mua CLOSED_SHORT: Thủ kho thấy đơn đã đóng, không còn nhận tiếp, kèm số thiếu đã chốt.</summary>
    private async Task<List<ClosedShortPurchaseDto>> ClosedShortAsync(CancellationToken ct)
    {
        var rows = await _uow.Repository<PurchaseOrderLine>().Query().AsNoTracking()
            .Where(l => l.PurchaseOrder.GoodsStatus == PurchaseGoodsStatuses.ClosedShort)
            .OrderByDescending(l => l.PurchaseOrderId).ThenBy(l => l.PurchaseOrderLineId)
            .Select(l => new
            {
                l.PurchaseOrderId, l.PurchaseOrder.PoCode, l.PurchaseOrder.OrderId,
                OrderCode = l.PurchaseOrder.Order == null ? null : l.PurchaseOrder.Order.OrderCode,
                SupplierName = l.PurchaseOrder.Supplier == null ? null : l.PurchaseOrder.Supplier.SupplierName,
                l.PurchaseOrderLineId, l.OrderedQty, l.NotReplenishedQty,
                MaterialCode = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                MaterialName = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                Unit = l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                MaterialColor = l.OrderMaterial != null ? l.OrderMaterial.Material.Color : l.Material!.Color,
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var received = await ReceivedByLineAsync(rows.Select(r => r.PurchaseOrderLineId).ToArray(), ct);
        return rows
            .Select(r => new ClosedShortPurchaseDto(r.PurchaseOrderId, r.PoCode, r.OrderId, r.OrderCode, r.SupplierName,
                r.PurchaseOrderLineId, r.MaterialCode, r.MaterialName, r.MaterialColor, r.Unit,
                r.OrderedQty, received.GetValueOrDefault(r.PurchaseOrderLineId), r.NotReplenishedQty))
            .ToList();
    }

    private async Task<List<IncomingSupplierMaterialDto>> SupplierAsync(CancellationToken ct)
    {
        var receivable = new[] { PurchaseGoodsStatuses.Ordered, PurchaseGoodsStatuses.PartiallyReceived };
        var rows = await _uow.Repository<PurchaseOrderLine>().Query().AsNoTracking()
            .Where(l => receivable.Contains(l.PurchaseOrder.GoodsStatus))
            .Select(l => new
            {
                l.PurchaseOrderId, l.PurchaseOrder.PoCode, l.PurchaseOrder.OrderId,
                OrderCode = l.PurchaseOrder.Order == null ? null : l.PurchaseOrder.Order.OrderCode,
                SupplierName = l.PurchaseOrder.Supplier == null ? null : l.PurchaseOrder.Supplier.SupplierName,
                l.PurchaseOrder.GoodsStatus, l.PurchaseOrder.ExpectedDeliveryCount, l.PurchaseOrderLineId, l.OrderedQty, l.NotReplenishedQty,
                MaterialCode = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                MaterialName = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                Unit = l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                MaterialColor = l.OrderMaterial != null ? l.OrderMaterial.Material.Color : l.Material!.Color,
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.PurchaseOrderLineId).ToArray();
        var received = await ReceivedByLineAsync(ids, ct);
        var counts = await DeliveryCountsAsync(rows.Select(r => r.PurchaseOrderId).Distinct().ToArray(), ct);

        return rows
            .Select(r =>
            {
                var got = received.GetValueOrDefault(r.PurchaseOrderLineId);
                return new IncomingSupplierMaterialDto(r.PurchaseOrderId, r.PoCode, r.OrderId, r.OrderCode, r.SupplierName,
                    r.ExpectedDeliveryCount, counts.GetValueOrDefault(r.PurchaseOrderId), r.PurchaseOrderLineId,
                    r.MaterialCode, r.MaterialName, r.MaterialColor, r.Unit,
                    r.OrderedQty, got, r.OrderedQty - got - r.NotReplenishedQty, r.GoodsStatus);
            })
            .Where(r => r.OutstandingQty > 0)
            .OrderBy(r => r.PoCode).ThenBy(r => r.MaterialCode)
            .ToList();
    }
}
