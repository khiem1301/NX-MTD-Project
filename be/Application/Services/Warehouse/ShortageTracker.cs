using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Một NVL còn thiếu của đơn: nguồn là dòng đơn mua hoặc NVL khách cấp; dòng phiếu vừa xác nhận nếu NVL có trong phiếu.</summary>
public sealed record ShortageLine(int MaterialId, decimal Qty, int? PurchaseOrderLineId, int? OrderMaterialId, int? MaterialReceiptLineId);

/// <summary>
/// DEV-ALL-95: thiếu NVL tính theo cả đơn. Khi đơn mua / đơn hàng (khách cấp) đã nhận đủ số lần giao dự kiến, MỌI dòng còn thiếu của
/// đơn (kể cả dòng chưa về lần nào) vào một đơn SHORT_DELIVERY; dòng đơn thiếu trỏ thẳng về dòng đơn mua / NVL của đơn. Hàng bù của
/// một dòng = các dòng phiếu bổ sung của đơn thiếu đó có cùng nguồn.
/// </summary>
public sealed class ShortageTracker
{
    private readonly IUnitOfWork _uow;
    private readonly IRequestService _requests;
    private readonly MaterialSupplyUpdater _supply;
    private readonly DeliveryCountGuard _deliveries;

    public ShortageTracker(IUnitOfWork uow, IRequestService requests, MaterialSupplyUpdater supply, DeliveryCountGuard deliveries)
    {
        _uow = uow; _requests = requests; _supply = supply; _deliveries = deliveries;
    }

    /// <summary>Dòng phiếu bổ sung có cùng nguồn với dòng đơn thiếu NVL (dòng đơn mua, hoặc NVL khách cấp không qua đơn mua).</summary>
    public static bool SameSource(RequestLine line, int? purchaseOrderLineId, int? orderMaterialId) =>
        line.PurchaseOrderLineId is { } pol
            ? purchaseOrderLineId == pol
            : purchaseOrderLineId == null && orderMaterialId != null && orderMaterialId == line.OrderMaterialId;

    /// <summary>Phiếu thường vừa xác nhận: đơn đã đủ số lần giao thì trả mọi dòng còn thiếu chưa có đơn thiếu NVL đang chờ.</summary>
    public async Task<List<ShortageLine>> FindAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        var open = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(q => q.Request.RequestTypeCode == RequestTypes.ShortDelivery && q.Request.Status == RequestStatuses.PendingProcessing)
            .Select(q => new { q.PurchaseOrderLineId, q.OrderMaterialId }).ToListAsync(ct);
        var lineOf = receipt.MaterialReceiptLines;

        if (receipt.PurchaseOrderId is { } poId)
        {
            var openLines = open.Where(o => o.PurchaseOrderLineId != null).Select(o => o.PurchaseOrderLineId!.Value).ToHashSet();
            var lines = await PurchaseShortagesAsync(poId, openLines, ct);
            return lines.Select(l => l with
            {
                MaterialReceiptLineId = lineOf.FirstOrDefault(r => r.PurchaseOrderLineId == l.PurchaseOrderLineId)?.MaterialReceiptLineId,
            }).ToList();
        }
        if (receipt.OrderId is not { } orderId) return [];
        var openMaterials = open.Where(o => o.OrderMaterialId != null).Select(o => o.OrderMaterialId!.Value).ToHashSet();
        var customer = await CustomerShortagesAsync(orderId, openMaterials, ct);
        return customer.Select(l => l with
        {
            MaterialReceiptLineId = lineOf.FirstOrDefault(r => r.PurchaseOrderLineId == null && r.OrderMaterialId == l.OrderMaterialId)
                ?.MaterialReceiptLineId,
        }).ToList();
    }

    /// <summary>Dòng đơn mua còn thiếu (đặt − đã nhận − chốt không bù) khi đơn mua đã nhận đủ số lần NCC dự kiến giao.</summary>
    private async Task<List<ShortageLine>> PurchaseShortagesAsync(int purchaseOrderId, IReadOnlySet<int> openLines, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking()
            .Include(p => p.PurchaseOrderLines).ThenInclude(l => l.OrderMaterial)
            .FirstAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);
        if (await _deliveries.PurchaseOrderDeliveriesAsync(purchaseOrderId, null, ct) < (po.ExpectedDeliveryCount ?? 1)) return [];

        var received = await _supply.ReceivedByPurchaseLineAsync(po.PurchaseOrderLines.Select(l => l.PurchaseOrderLineId).ToList(), ct);
        return po.PurchaseOrderLines
            .Select(l => (Line: l, Qty: l.OrderedQty - received.GetValueOrDefault(l.PurchaseOrderLineId) - l.NotReplenishedQty))
            .Where(x => x.Qty > 0 && !openLines.Contains(x.Line.PurchaseOrderLineId))
            .Select(x => new ShortageLine(x.Line.OrderMaterial?.MaterialId ?? x.Line.MaterialId!.Value, x.Qty,
                x.Line.PurchaseOrderLineId, null, null))
            .ToList();
    }

    /// <summary>NVL khách cấp còn thiếu (khách cấp − đã nhận) khi đơn hàng đã nhận đủ số lần khách dự kiến giao.</summary>
    private async Task<List<ShortageLine>> CustomerShortagesAsync(int orderId, IReadOnlySet<int> openMaterials, CancellationToken ct)
    {
        var expected = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.OrderId == orderId).Select(o => o.CustomerDeliveryCount).FirstAsync(ct) ?? 1;
        if (await _deliveries.CustomerDeliveriesAsync(orderId, null, ct) < expected) return [];

        var materials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(m => m.OrderId == orderId && m.CustomerQty > 0).ToListAsync(ct);
        var ids = materials.Select(m => m.OrderMaterialId).ToArray();
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value) && l.PurchaseOrderLineId == null
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OrderMaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        return materials
            .Select(m => (Material: m, Qty: m.CustomerQty - received.GetValueOrDefault(m.OrderMaterialId)))
            .Where(x => x.Qty > 0 && !openMaterials.Contains(x.Material.OrderMaterialId))
            .Select(x => new ShortageLine(x.Material.MaterialId, x.Qty, null, x.Material.OrderMaterialId, null))
            .ToList();
    }

    /// <summary>Sinh đơn SHORT_DELIVERY gắn với phiếu vừa xác nhận, mỗi NVL còn thiếu một dòng (cùng transaction với phiếu).</summary>
    public async Task StageAsync(MaterialReceipt receipt, IReadOnlyList<ShortageLine> shortages, CancellationToken ct)
    {
        var draft = new RequestDraft(RequestTypes.ShortDelivery, receipt.OrderId, null, null, null,
            receipt.MaterialReceiptId, null, null, $"Thiếu NVL — phiếu {receipt.ReceiptCode}",
            WarehouseMessages.ShortDeliveryCreated, Array.Empty<RequestLineInput>(), null);
        var request = await _requests.StageSystemRequestAsync(draft, ct);
        foreach (var s in shortages)
        {
            request.RequestLines.Add(new RequestLine
            {
                MaterialId = s.MaterialId,
                Quantity = s.Qty,
                PurchaseOrderLineId = s.PurchaseOrderLineId,
                OrderMaterialId = s.OrderMaterialId,
                MaterialReceiptLineId = s.MaterialReceiptLineId,
            });
        }
    }

    /// <summary>Số đã nhận bù của từng dòng đơn thiếu NVL (phiếu bổ sung đã xác nhận; có thể gồm cả phiếu đang chờ).</summary>
    public async Task<Dictionary<int, decimal>> ReplenishedAsync(int shortageRequestId, bool includePending, CancellationToken ct)
    {
        var lines = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(l => l.RequestId == shortageRequestId).ToListAsync(ct);
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReceipt.ShortageRequestId == shortageRequestId
                        && (l.MaterialReceipt.Status == ReceiptStatuses.Confirmed
                            || (includePending && l.MaterialReceipt.Status == ReceiptStatuses.Pending)))
            .Select(l => new { l.PurchaseOrderLineId, l.OrderMaterialId, l.AcceptedQty }).ToListAsync(ct);
        return lines.ToDictionary(l => l.RequestLineId,
            l => received.Where(r => SameSource(l, r.PurchaseOrderLineId, r.OrderMaterialId)).Sum(r => r.AcceptedQty));
    }
}
