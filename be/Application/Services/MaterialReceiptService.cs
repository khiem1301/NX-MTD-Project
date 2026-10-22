using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC37 / UC39: phiếu nhập NVL — Thủ kho lập, Quản đốc xác nhận (tăng tồn) hoặc trả lại. Nguồn khách cấp lập theo đơn hàng,
/// nguồn nhà cung cấp lập theo đơn mua (TH-12). Có hàng thiếu / lỗi thì tự sinh đơn SHORT_DELIVERY (TH-13); phiếu bổ sung
/// (SUPPLEMENTARY) ghi nhận hàng giao bù theo đơn đó.
/// </summary>
public sealed class MaterialReceiptService : IMaterialReceiptService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IRequestService _requests;
    private readonly MaterialSupplyUpdater _supply;
    private readonly IClock _clock;
    private readonly ILogger<MaterialReceiptService> _logger;

    public MaterialReceiptService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        IRequestService requests, MaterialSupplyUpdater supply, IClock clock, ILogger<MaterialReceiptService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _requests = requests; _supply = supply; _clock = clock; _logger = logger;
    }

    private IQueryable<MaterialReceipt> Receipts => _uow.Repository<MaterialReceipt>().Query();

    public async Task<Result<PagedResult<MaterialReceiptListItemDto>>> GetPagedAsync(MaterialReceiptQuery query,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Receipts.AsNoTracking()
            .Where(r => string.IsNullOrEmpty(query.Status) || r.Status == query.Status)
            .Where(r => query.OrderId == null || r.OrderId == query.OrderId)
            .Where(r => query.PurchaseOrderId == null || r.PurchaseOrderId == query.PurchaseOrderId)
            .Where(r => string.IsNullOrEmpty(query.Source) || r.Source == query.Source)
            .Where(r => string.IsNullOrEmpty(keyword) || r.ReceiptCode.Contains(keyword)
                || (r.Order != null && r.Order.OrderCode.Contains(keyword))
                || (r.PurchaseOrder != null && r.PurchaseOrder.PoCode.Contains(keyword)))
            .OrderByDescending(r => r.MaterialReceiptId)
            .Select(r => new MaterialReceiptListItemDto(r.MaterialReceiptId, r.ReceiptCode, r.ReceiptType, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source, r.PurchaseOrderId,
                r.PurchaseOrder == null ? null : r.PurchaseOrder.PoCode, r.ReceiptDate, r.Status, r.SubmittedAt,
                r.ConfirmedAt, r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.MaterialReceiptLines.Count))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialReceiptListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialReceiptDto>> GetByIdAsync(int receiptId, CancellationToken ct)
    {
        var dto = await Receipts.AsNoTracking().Where(r => r.MaterialReceiptId == receiptId)
            .Select(r => new MaterialReceiptDto(r.MaterialReceiptId, r.ReceiptCode, r.ReceiptType, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source, r.PurchaseOrderId,
                r.PurchaseOrder == null ? null : r.PurchaseOrder.PoCode, r.OriginalReceiptId,
                r.OriginalReceipt == null ? null : r.OriginalReceipt.ReceiptCode, r.ShortageRequestId, null,
                r.ReceiptDate, r.Status, r.ReturnReason, r.Note, r.SubmittedAt, r.ConfirmedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.MaterialReceiptLines.OrderBy(l => l.MaterialReceiptLineId).Select(l => new MaterialReceiptLineDto(
                    l.MaterialReceiptLineId, l.OrderMaterialId, l.PurchaseOrderLineId, l.OriginalReceiptLineId,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialId : l.MaterialId!.Value,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialColor : l.MaterialColor ?? string.Empty,
                    l.PurchaseOrderLine == null ? null : l.PurchaseOrderLine.UnitPrice, l.ToReplenishQty,
                    l.AcceptedQty, l.ShortQty, l.DefectiveQty)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();

        var shortDeliveryId = await _uow.Repository<Request>().Query().AsNoTracking()
            .Where(q => q.MaterialReceiptId == receiptId && q.RequestTypeCode == RequestTypes.ShortDelivery)
            .Select(q => (int?)q.RequestId).FirstOrDefaultAsync(ct);
        return Result<MaterialReceiptDto>.Success(dto with { ShortDeliveryRequestId = shortDeliveryId });
    }

    public async Task<Result<MaterialReceiptDto>> CreateAsync(SaveMaterialReceiptDto dto, CancellationToken ct)
    {
        var resolved = await ResolveAsync(dto, ct);
        if (!resolved.IsSuccess) return WarehouseFailures.From<MaterialReceiptDto>(resolved);

        var created = await _uow.InTransactionAsync(async () =>
        {
            var receipt = new MaterialReceipt
            {
                ReceiptCode = await _codes.NextAsync<MaterialReceipt>(DocumentPrefixes.MaterialReceipt, r => r.ReceiptCode, ct),
                ReceiptType = ReceiptTypes.New,
                Status = ReceiptStatuses.Pending,
                SubmittedAt = _clock.Now,
            };
            Apply(receipt, dto, resolved.Data!);
            await _uow.Repository<MaterialReceipt>().AddAsync(receipt);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(receipt.MaterialReceiptId);
        });
        return await GetByIdAsync(created.Data, ct);
    }

    public async Task<Result<MaterialReceiptDto>> UpdateAsync(int receiptId, SaveMaterialReceiptDto dto,
        CancellationToken ct)
    {
        var receipt = await Receipts.Include(r => r.MaterialReceiptLines)
            .FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
        if (receipt is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();
        if (receipt.ReceiptType != ReceiptTypes.New
            || receipt.Status is not (ReceiptStatuses.Pending or ReceiptStatuses.Returned))
            return WarehouseFailures.InvalidStatus<MaterialReceiptDto>();

        var resolved = await ResolveAsync(dto, ct);
        if (!resolved.IsSuccess) return WarehouseFailures.From<MaterialReceiptDto>(resolved);

        _uow.Repository<MaterialReceiptLine>().RemoveRange(receipt.MaterialReceiptLines.ToList());
        Apply(receipt, dto, resolved.Data!);
        receipt.Status = ReceiptStatuses.Pending;
        receipt.ReturnReason = null;
        receipt.SubmittedAt = _clock.Now;
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(receiptId, ct);
    }

    /// <summary>Phiếu bổ sung theo đơn thiếu NVL đã chọn "giao bù": nhận bù ≤ số còn phải bù của từng dòng.</summary>
    public async Task<Result<MaterialReceiptDto>> CreateSupplementaryAsync(SaveSupplementaryReceiptDto dto,
        CancellationToken ct)
    {
        var request = await _uow.Repository<Request>().Query().Include(q => q.RequestLines)
            .FirstOrDefaultAsync(q => q.RequestId == dto.ShortageRequestId && q.RequestTypeCode == RequestTypes.ShortDelivery, ct);
        if (request is null || request.MaterialReceiptId is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();
        if (request.Status != RequestStatuses.PendingProcessing)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotResolvable);
        if (request.ShortageResolution != ShortageResolutions.Replenish)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotReplenish);

        var original = await Receipts.Include(r => r.MaterialReceiptLines)
            .FirstAsync(r => r.MaterialReceiptId == request.MaterialReceiptId, ct);
        var requestLines = request.RequestLines.Where(l => l.MaterialReceiptLineId != null && l.ReplenishQty > 0)
            .ToDictionary(l => l.MaterialReceiptLineId!.Value);
        if (dto.Lines.Any(l => !requestLines.ContainsKey(l.OriginalReceiptLineId)))
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReceiptLineTarget);

        var done = await ReplenishedByLineAsync(request.RequestId, includePending: true, ct);
        var over = dto.Lines
            .Where(l => l.AcceptedQty > requestLines[l.OriginalReceiptLineId].ReplenishQty!.Value - done.GetValueOrDefault(l.OriginalReceiptLineId))
            .Select(l => $"còn {requestLines[l.OriginalReceiptLineId].ReplenishQty!.Value - done.GetValueOrDefault(l.OriginalReceiptLineId):0.###}, nhận {l.AcceptedQty:0.###}")
            .ToList();
        if (over.Count > 0)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageExceeded,
                $"{WarehouseMessages.SupplementaryExceedsReplenish}: {string.Join("; ", over)}.");

        var originalLines = original.MaterialReceiptLines.ToDictionary(l => l.MaterialReceiptLineId);
        var created = await _uow.InTransactionAsync(async () =>
        {
            var receipt = new MaterialReceipt
            {
                ReceiptCode = await _codes.NextAsync<MaterialReceipt>(DocumentPrefixes.MaterialReceipt, r => r.ReceiptCode, ct),
                ReceiptType = ReceiptTypes.Supplementary,
                OriginalReceiptId = original.MaterialReceiptId,
                ShortageRequestId = request.RequestId,
                OrderId = original.OrderId,
                Source = original.Source,
                PurchaseOrderId = original.PurchaseOrderId,
                ReceiptDate = dto.ReceiptDate,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                Status = ReceiptStatuses.Pending,
                SubmittedAt = _clock.Now,
                MaterialReceiptLines = dto.Lines.Select(l =>
                {
                    var src = originalLines[l.OriginalReceiptLineId];
                    return new MaterialReceiptLine
                    {
                        OrderMaterialId = src.OrderMaterialId,
                        PurchaseOrderLineId = src.PurchaseOrderLineId,
                        OriginalReceiptLineId = src.MaterialReceiptLineId,
                        ToReplenishQty = requestLines[l.OriginalReceiptLineId].ReplenishQty,
                        MaterialId = src.OrderMaterialId is null ? src.MaterialId : null,
                        MaterialColor = src.OrderMaterialId is null ? src.MaterialColor : null,
                        AcceptedQty = l.AcceptedQty, ShortQty = l.ShortQty, DefectiveQty = l.DefectiveQty,
                    };
                }).ToList(),
            };
            await _uow.Repository<MaterialReceipt>().AddAsync(receipt);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(receipt.MaterialReceiptId);
        });
        return await GetByIdAsync(created.Data, ct);
    }

    public Task<Result> ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var receipt = await Receipts.Include(r => r.MaterialReceiptLines).ThenInclude(l => l.OrderMaterial)
                .FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
            if (receipt is null) return WarehouseFailures.NotFound();
            if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var today = _clock.Today;
            var movements = receipt.MaterialReceiptLines.Where(l => l.AcceptedQty > 0)
                .Select(l => new StockMovement(
                    l.OrderMaterial is { } om
                        ? StockKey.Material(om.MaterialId, om.MaterialColor)
                        : StockKey.Material(l.MaterialId!.Value, l.MaterialColor ?? string.Empty),
                    l.AcceptedQty, StockDocTypes.MaterialReceipt, receipt.MaterialReceiptId, l.MaterialReceiptLineId, today))
                .ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            receipt.Status = ReceiptStatuses.Confirmed;
            receipt.ConfirmedBy = confirmedByEmployeeId;
            receipt.ConfirmedAt = _clock.Now;
            await _uow.SaveChangesAsync();

            // Trạng thái đơn mua / NVL của đơn đọc từ DB nên phải tính sau khi phiếu đã được lưu CONFIRMED.
            if (receipt.PurchaseOrderId is { } poId) await _supply.RecalculatePurchaseOrderAsync(poId, ct);
            if (receipt.OrderId is { } orderId) await _supply.RecalculateMaterialStatusAsync(orderId, ct);

            var message = WarehouseMessages.ReceiptConfirmed;
            if (receipt.ReceiptType == ReceiptTypes.New
                && receipt.MaterialReceiptLines.Any(l => l.ShortQty + l.DefectiveQty > 0))
            {
                await StageShortDeliveryAsync(receipt, ct);
                message = $"{message} {WarehouseMessages.ShortDeliveryCreated}";   // MSG31
            }
            else if (receipt.ReceiptType == ReceiptTypes.Supplementary && receipt.ShortageRequestId is { } requestId)
            {
                await CompleteWhenReplenishedAsync(requestId, ct);
            }

            await _uow.SaveChangesAsync();
            _logger.LogInformation("Material receipt {ReceiptCode} confirmed by employee {EmployeeId}",
                receipt.ReceiptCode, confirmedByEmployeeId);
            return Result.Success(message);
        });

    public async Task<Result> ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct)
    {
        var receipt = await Receipts.FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
        if (receipt is null) return WarehouseFailures.NotFound();
        if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();

        receipt.Status = ReceiptStatuses.Returned;
        receipt.ReturnReason = dto.Reason.Trim();
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReceiptReturned);
    }

    // ---------- Thiếu / lỗi → đơn SHORT_DELIVERY cho Quản đốc ----------

    private async Task StageShortDeliveryAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        var draft = new RequestDraft(RequestTypes.ShortDelivery, receipt.OrderId, null, null, null,
            receipt.MaterialReceiptId, null, null, $"Thiếu / lỗi NVL — phiếu {receipt.ReceiptCode}",
            WarehouseMessages.ShortDeliveryCreated, Array.Empty<RequestLineInput>(), null);
        var request = await _requests.StageSystemRequestAsync(draft, ct);
        foreach (var line in receipt.MaterialReceiptLines.Where(l => l.ShortQty + l.DefectiveQty > 0))
        {
            request.RequestLines.Add(new RequestLine
            {
                MaterialId = line.OrderMaterial?.MaterialId ?? line.MaterialId,
                MaterialColor = line.OrderMaterial?.MaterialColor ?? line.MaterialColor ?? string.Empty,
                Quantity = line.ShortQty + line.DefectiveQty,
                MaterialReceiptLineId = line.MaterialReceiptLineId,
            });
        }
    }

    /// <summary>Số đã nhận bù của từng dòng phiếu gốc (phiếu bổ sung đã xác nhận; có thể gồm cả phiếu đang chờ).</summary>
    private async Task<Dictionary<int, decimal>> ReplenishedByLineAsync(int shortageRequestId, bool includePending,
        CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query()
            .Where(l => l.OriginalReceiptLineId != null && l.MaterialReceipt.ShortageRequestId == shortageRequestId
                        && l.MaterialReceipt.ReceiptType == ReceiptTypes.Supplementary
                        && (l.MaterialReceipt.Status == ReceiptStatuses.Confirmed
                            || (includePending && l.MaterialReceipt.Status == ReceiptStatuses.Pending)))
            .GroupBy(l => l.OriginalReceiptLineId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    private async Task CompleteWhenReplenishedAsync(int requestId, CancellationToken ct)
    {
        var lines = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(l => l.RequestId == requestId && l.ReplenishQty > 0 && l.MaterialReceiptLineId != null).ToListAsync(ct);
        var done = await ReplenishedByLineAsync(requestId, includePending: false, ct);
        if (lines.Count > 0 && lines.All(l => done.GetValueOrDefault(l.MaterialReceiptLineId!.Value) >= l.ReplenishQty))
            await _requests.MarkCompletedAsync(requestId, "Đã nhận đủ hàng bù.", ct);
    }

    // ---------- Kiểm tra và dựng dòng phiếu ----------

    private sealed record ResolvedLine(int? OrderMaterialId, int? PurchaseOrderLineId, int? MaterialId,
        string? MaterialColor, decimal AcceptedQty, decimal ShortQty, decimal DefectiveQty);

    private sealed record ResolvedReceipt(int? OrderId, int? PurchaseOrderId, IReadOnlyList<ResolvedLine> Lines);

    private Task<Result<ResolvedReceipt>> ResolveAsync(SaveMaterialReceiptDto dto, CancellationToken ct) =>
        dto.Source == MaterialSources.Supplier ? ResolveSupplierAsync(dto, ct) : ResolveCustomerAsync(dto, ct);

    private async Task<Result<ResolvedReceipt>> ResolveCustomerAsync(SaveMaterialReceiptDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<ResolvedReceipt>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var ids = dto.Lines.Select(l => l.OrderMaterialId ?? 0).ToList();
        if (ids.Any(i => i <= 0))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReceiptLineTarget);
        var valid = await _uow.Repository<OrderMaterial>().Query()
            .CountAsync(om => ids.Contains(om.OrderMaterialId) && om.OrderId == dto.OrderId, ct);
        if (valid != ids.Count)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderMaterialNotInOrder);

        return Result<ResolvedReceipt>.Success(new ResolvedReceipt(order.OrderId, null,
            dto.Lines.Select(l => new ResolvedLine(l.OrderMaterialId, null, null, null, l.AcceptedQty, l.ShortQty, l.DefectiveQty)).ToList()));
    }

    private async Task<Result<ResolvedReceipt>> ResolveSupplierAsync(SaveMaterialReceiptDto dto, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking().Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == dto.PurchaseOrderId, ct);
        if (po is null) return WarehouseFailures.NotFound<ResolvedReceipt>();
        if (po.GoodsStatus is not (PurchaseGoodsStatuses.Ordered or PurchaseGoodsStatuses.PartiallyReceived))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.PurchaseOrderNotReceivable);
        if (po.OrderId is { } orderId)
        {
            var status = await _uow.Repository<CustomerOrder>().Query().Where(o => o.OrderId == orderId)
                .Select(o => o.Status).FirstAsync(ct);
            if (OrderStatuses.Closed.Contains(status))
                return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
        }

        var byId = po.PurchaseOrderLines.ToDictionary(l => l.PurchaseOrderLineId);
        if (dto.Lines.Any(l => l.PurchaseOrderLineId is null || !byId.ContainsKey(l.PurchaseOrderLineId.Value)))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.PurchaseLineNotInOrder);

        var received = await _supply.ReceivedByPurchaseLineAsync(byId.Keys.ToList(), ct);
        var over = dto.Lines.Select(l => (Line: l, Pol: byId[l.PurchaseOrderLineId!.Value]))
            .Select(x => (x.Line, Outstanding: x.Pol.OrderedQty - received.GetValueOrDefault(x.Pol.PurchaseOrderLineId) - x.Pol.NotReplenishedQty))
            .Where(x => x.Line.AcceptedQty > x.Outstanding)
            .Select(x => $"còn {x.Outstanding:0.###}, nhập {x.Line.AcceptedQty:0.###}")
            .ToList();
        if (over.Count > 0)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptExceedsPurchase,
                $"{WarehouseMessages.ReceiptExceedsPurchase}: {string.Join("; ", over)}.");

        return Result<ResolvedReceipt>.Success(new ResolvedReceipt(po.OrderId, po.PurchaseOrderId,
            dto.Lines.Select(l =>
            {
                var pol = byId[l.PurchaseOrderLineId!.Value];
                return pol.OrderMaterialId is not null
                    ? new ResolvedLine(pol.OrderMaterialId, pol.PurchaseOrderLineId, null, null, l.AcceptedQty, l.ShortQty, l.DefectiveQty)
                    : new ResolvedLine(null, pol.PurchaseOrderLineId, pol.MaterialId, pol.MaterialColor ?? string.Empty,
                        l.AcceptedQty, l.ShortQty, l.DefectiveQty);
            }).ToList()));
    }

    private static void Apply(MaterialReceipt receipt, SaveMaterialReceiptDto dto, ResolvedReceipt resolved)
    {
        receipt.OrderId = resolved.OrderId;
        receipt.Source = dto.Source;
        receipt.PurchaseOrderId = resolved.PurchaseOrderId;
        receipt.ReceiptDate = dto.ReceiptDate;
        receipt.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        receipt.MaterialReceiptLines = resolved.Lines.Select(l => new MaterialReceiptLine
        {
            OrderMaterialId = l.OrderMaterialId,
            PurchaseOrderLineId = l.PurchaseOrderLineId,
            MaterialId = l.MaterialId,
            MaterialColor = l.MaterialColor,
            AcceptedQty = l.AcceptedQty,
            ShortQty = l.ShortQty,
            DefectiveQty = l.DefectiveQty,
        }).ToList();
    }
}
