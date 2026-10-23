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

    public IncomingMaterialService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<IncomingMaterialsDto>> GetAsync(CancellationToken ct)
    {
        var customer = await CustomerAsync(ct);
        var supplier = await SupplierAsync(ct);
        return Result<IncomingMaterialsDto>.Success(new IncomingMaterialsDto(customer, supplier));
    }

    private async Task<List<IncomingCustomerMaterialDto>> CustomerAsync(CancellationToken ct)
    {
        var closed = OrderStatuses.Closed.ToArray();   // EF không dịch Contains trên IReadOnlySet
        var rows = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => om.CustomerQty > 0 && !closed.Contains(om.Order.Status))
            .Select(om => new
            {
                om.OrderMaterialId, om.OrderId, om.Order.OrderCode, om.Material.MaterialCode, om.Material.MaterialName,
                om.MaterialColor, om.Material.Unit, om.CustomerQty, om.CustomerExpectedDate,
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

        return rows
            .Select(r => new IncomingCustomerMaterialDto(r.OrderMaterialId, r.OrderId, r.OrderCode, r.MaterialCode, r.MaterialName,
                r.MaterialColor, r.Unit, r.CustomerQty, received.GetValueOrDefault(r.OrderMaterialId),
                r.CustomerQty - received.GetValueOrDefault(r.OrderMaterialId), r.CustomerExpectedDate))
            .Where(r => r.OutstandingQty > 0)
            .OrderBy(r => r.ExpectedDate ?? DateOnly.MaxValue).ThenBy(r => r.OrderCode).ThenBy(r => r.MaterialCode)
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
                l.PurchaseOrder.ExpectedDeliveryDate, l.PurchaseOrderLineId, l.OrderedQty, l.NotReplenishedQty,
                MaterialCode = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                MaterialName = l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                Unit = l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                MaterialColor = l.OrderMaterial != null ? l.OrderMaterial.MaterialColor : l.MaterialColor ?? string.Empty,
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.PurchaseOrderLineId).ToArray();
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.PurchaseOrderLineId != null && ids.Contains(l.PurchaseOrderLineId.Value)
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        return rows
            .Select(r =>
            {
                var got = received.GetValueOrDefault(r.PurchaseOrderLineId);
                return new IncomingSupplierMaterialDto(r.PurchaseOrderId, r.PoCode, r.OrderId, r.OrderCode, r.SupplierName,
                    r.ExpectedDeliveryDate, r.PurchaseOrderLineId, r.MaterialCode, r.MaterialName, r.MaterialColor, r.Unit,
                    r.OrderedQty, got, r.OrderedQty - got - r.NotReplenishedQty);
            })
            .Where(r => r.OutstandingQty > 0)
            .OrderBy(r => r.ExpectedDeliveryDate ?? DateOnly.MaxValue).ThenBy(r => r.PoCode).ThenBy(r => r.MaterialCode)
            .ToList();
    }
}
