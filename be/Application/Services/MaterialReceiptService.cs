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
/// nguồn nhà cung cấp lập theo đơn mua (TH-12). Thủ kho chỉ nhập số đạt; khi phiếu đã xác nhận đủ số lần giao dự kiến
/// (<c>ExpectedDeliveryCount</c>, mặc định 1) mà còn thiếu hàng thì tự sinh đơn SHORT_DELIVERY (TH-13). Phiếu bổ sung là
/// phiếu có <c>ShortageRequestId</c>, ghi nhận hàng giao bù theo đơn đó.
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
            .Select(r => new MaterialReceiptListItemDto(r.MaterialReceiptId, r.ReceiptCode,
                r.ShortageRequestId == null ? ReceiptTypes.New : ReceiptTypes.Supplementary, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source!, r.PurchaseOrderId,
                r.PurchaseOrder == null ? null : r.PurchaseOrder.PoCode, r.ReceiptDate, r.Status, r.SubmittedAt,
                r.ConfirmedAt, r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.MaterialReceiptLines.Count))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialReceiptListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialReceiptDto>> GetByIdAsync(int receiptId, CancellationToken ct)
    {
        var dto = await Receipts.AsNoTracking().Where(r => r.MaterialReceiptId == receiptId)
            .Select(r => new MaterialReceiptDto(r.MaterialReceiptId, r.ReceiptCode,
                r.ShortageRequestId == null ? ReceiptTypes.New : ReceiptTypes.Supplementary, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source!, r.PurchaseOrderId,
                r.PurchaseOrder == null ? null : r.PurchaseOrder.PoCode,
                r.ShortageRequest == null ? null : r.ShortageRequest.MaterialReceiptId,
                r.ShortageRequest == null || r.ShortageRequest.MaterialReceipt == null
                    ? null : r.ShortageRequest.MaterialReceipt.ReceiptCode, r.ShortageRequestId, null,
                r.ReceiptDate, r.Status, r.ReturnReason, r.Note, r.SubmittedAt, r.ConfirmedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.MaterialReceiptLines.OrderBy(l => l.MaterialReceiptLineId).Select(l => new MaterialReceiptLineDto(
                    l.MaterialReceiptLineId, l.OrderMaterialId, l.PurchaseOrderLineId, l.OriginalReceiptLineId,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialId : l.MaterialId!.Value,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Color : l.Material!.Color,
                    l.PurchaseOrderLine == null ? null : l.PurchaseOrderLine.UnitPrice, null,
                    l.AcceptedQty)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();

        var shortDeliveryId = await _uow.Repository<Request>().Query().AsNoTracking()
            .Where(q => q.MaterialReceiptId == receiptId && q.RequestTypeCode == RequestTypes.ShortDelivery)
            .Select(q => (int?)q.RequestId).FirstOrDefaultAsync(ct);

        // Phiếu bổ sung: số cần bù của từng dòng = ReplenishQty Quản đốc đã chọn trên đơn thiếu NVL.
        if (dto.ShortageRequestId is { } shortageId)
        {
            var toReplenish = await _uow.Repository<RequestLine>().Query().AsNoTracking()
                .Where(q => q.RequestId == shortageId && q.MaterialReceiptLineId != null && q.ReplenishQty != null)
                .ToDictionaryAsync(q => q.MaterialReceiptLineId!.Value, q => q.ReplenishQty, ct);
            dto = dto with
            {
                Lines = dto.Lines.Select(l => l.OriginalReceiptLineId is { } o && toReplenish.TryGetValue(o, out var qty)
                    ? l with { ToReplenishQty = qty } : l).ToList(),
            };
        }
        return Result<MaterialReceiptDto>.Success(dto with { ShortDeliveryRequestId = shortDeliveryId });
    }

    public async Task<Result<MaterialReceiptDto>> CreateAsync(SaveMaterialReceiptDto dto, CancellationToken ct)
    {
        var resolved = await ResolveAsync(dto, null, ct);
        if (!resolved.IsSuccess) return WarehouseFailures.From<MaterialReceiptDto>(resolved);

        var created = await _uow.InTransactionAsync(async () =>
        {
            var receipt = new MaterialReceipt
            {
                ReceiptCode = await _codes.NextAsync<MaterialReceipt>(DocumentPrefixes.MaterialReceipt, r => r.ReceiptCode, ct),
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
        if (receipt.ShortageRequestId != null   // phiếu bổ sung không sửa
            || receipt.Status is not (ReceiptStatuses.Pending or ReceiptStatuses.Returned))
            return WarehouseFailures.InvalidStatus<MaterialReceiptDto>();

        var resolved = await ResolveAsync(dto, receiptId, ct);
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
                ShortageRequestId = request.RequestId,
                OrderId = original.OrderId,
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
                        MaterialId = src.OrderMaterialId is null ? src.MaterialId : null,
                        AcceptedQty = l.AcceptedQty,
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

            // Kiểm lại trần ngay lúc xác nhận (phiếu có thể lập trước khi phiếu khác được xác nhận): tổng đã nhận không vượt
            // số đặt (trừ phần chốt không bù) hoặc số khách cấp.
            var overLimit = await FindOverLimitAsync(receipt, ct);
            if (overLimit is not null) return Result.Failure(ErrorCodes.ReceiptExceedsPurchase, overLimit);

            // Swimlane III.1.4: Thủ kho chụp ảnh chứng từ giao hàng trước khi Quản đốc xác nhận.
            var hasPhoto = await _uow.Repository<Attachment>().Query().AnyAsync(a =>
                a.OwnerType == AttachmentOwnerTypes.MaterialReceipt && a.OwnerId == receiptId && a.IsCurrent, ct);
            if (!hasPhoto)
                return Result.Failure(ErrorCodes.ReceiptPhotoRequired, WarehouseMessages.ReceiptPhotoRequired);

            var today = _clock.Today;
            var movements = receipt.MaterialReceiptLines.Where(l => l.AcceptedQty > 0)
                .Select(l => new StockMovement(
                    l.OrderMaterial is { } om
                        ? StockKey.Material(om.MaterialId)
                        : StockKey.Material(l.MaterialId!.Value),
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
            if (receipt.ShortageRequestId is { } requestId)
            {
                await CompleteWhenReplenishedAsync(requestId, ct);
            }
            else
            {
                var shortages = await FindShortagesAsync(receipt, ct);
                if (shortages.Count > 0)
                {
                    await StageShortDeliveryAsync(receipt, shortages, ct);
                    message = $"{message} {WarehouseMessages.ShortDeliveryCreated}";   // MSG31
                }
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

    /// <summary>Mô tả dòng vượt trần khi xác nhận; null nếu mọi dòng còn trong số còn phải nhận.</summary>
    private async Task<string?> FindOverLimitAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        var messages = new List<string>();
        var polIds = receipt.MaterialReceiptLines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToList();
        if (polIds.Count > 0)
        {
            var lines = await _uow.Repository<PurchaseOrderLine>().Query().AsNoTracking()
                .Where(p => polIds.Contains(p.PurchaseOrderLineId)).ToDictionaryAsync(p => p.PurchaseOrderLineId, ct);
            var others = await _supply.ReceivedByPurchaseLineAsync(polIds, ct);   // chỉ phiếu đã xác nhận, phiếu này còn chờ
            foreach (var l in receipt.MaterialReceiptLines.Where(l => l.PurchaseOrderLineId != null))
            {
                var pol = lines[l.PurchaseOrderLineId!.Value];
                var outstanding = pol.OrderedQty - others.GetValueOrDefault(pol.PurchaseOrderLineId) - pol.NotReplenishedQty;
                if (l.AcceptedQty > outstanding) messages.Add($"còn {outstanding:0.###}, nhập {l.AcceptedQty:0.###}");
            }
            if (messages.Count > 0) return $"{WarehouseMessages.ReceiptExceedsPurchase}: {string.Join("; ", messages)}.";
        }

        var omIds = receipt.MaterialReceiptLines.Where(l => l.PurchaseOrderLineId == null && l.OrderMaterialId != null)
            .Select(l => l.OrderMaterialId!.Value).ToList();
        if (omIds.Count == 0) return null;
        var materials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(m => omIds.Contains(m.OrderMaterialId)).ToDictionaryAsync(m => m.OrderMaterialId, ct);
        var received = await ConfirmedCustomerReceivedAsync(omIds, receipt.MaterialReceiptId, ct);
        foreach (var l in receipt.MaterialReceiptLines.Where(l => l.PurchaseOrderLineId == null && l.OrderMaterialId != null))
        {
            var outstanding = materials[l.OrderMaterialId!.Value].CustomerQty - received.GetValueOrDefault(l.OrderMaterialId.Value);
            if (l.AcceptedQty > outstanding) messages.Add($"còn {outstanding:0.###}, nhập {l.AcceptedQty:0.###}");
        }
        return messages.Count == 0 ? null : $"{WarehouseMessages.ReceiptExceedsCustomer}: {string.Join("; ", messages)}.";
    }

    /// <summary>Số đã nhận (phiếu khách cấp ĐÃ XÁC NHẬN, không có đơn mua) theo NVL của đơn, bỏ qua một phiếu nếu cần.</summary>
    private async Task<Dictionary<int, decimal>> ConfirmedCustomerReceivedAsync(IReadOnlyCollection<int> orderMaterialIds,
        int? exceptReceiptId, CancellationToken ct)
    {
        var ids = orderMaterialIds.ToArray();
        return await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value) && l.PurchaseOrderLineId == null
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed
                        && (exceptReceiptId == null || l.MaterialReceiptId != exceptReceiptId))
            .GroupBy(l => l.OrderMaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
    }

    // ---------- Thiếu hàng (tự tính) → đơn SHORT_DELIVERY cho Quản đốc ----------

    /// <summary>
    /// Dòng của phiếu vừa xác nhận còn thiếu so với đơn mua / số khách cấp, khi nguồn đã giao đủ số lần dự kiến.
    /// Số thiếu = đặt (hoặc khách cấp) − đã nhận − đã chốt không bù. Bỏ qua dòng đã có đơn thiếu NVL đang chờ xử lý.
    /// </summary>
    private async Task<List<(MaterialReceiptLine Line, decimal Qty)>> FindShortagesAsync(MaterialReceipt receipt,
        CancellationToken ct)
    {
        var result = new List<(MaterialReceiptLine, decimal)>();
        var open = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(q => q.Request.RequestTypeCode == RequestTypes.ShortDelivery
                        && q.Request.Status == RequestStatuses.PendingProcessing && q.MaterialReceiptLine != null)
            .Select(q => new { q.MaterialReceiptLine!.PurchaseOrderLineId, q.MaterialReceiptLine.OrderMaterialId })
            .ToListAsync(ct);

        if (receipt.PurchaseOrderId is { } poId)
        {
            var po = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking().Include(p => p.PurchaseOrderLines)
                .FirstAsync(p => p.PurchaseOrderId == poId, ct);
            var receivedCount = await Receipts.CountAsync(r => r.PurchaseOrderId == poId && r.Status == ReceiptStatuses.Confirmed, ct);
            if (receivedCount < (po.ExpectedDeliveryCount ?? 1)) return result;

            var received = await _supply.ReceivedByPurchaseLineAsync(po.PurchaseOrderLines.Select(l => l.PurchaseOrderLineId).ToList(), ct);
            foreach (var line in receipt.MaterialReceiptLines.Where(l => l.PurchaseOrderLineId != null))
            {
                var pol = po.PurchaseOrderLines.First(p => p.PurchaseOrderLineId == line.PurchaseOrderLineId);
                var outstanding = pol.OrderedQty - received.GetValueOrDefault(pol.PurchaseOrderLineId) - pol.NotReplenishedQty;
                if (outstanding > 0 && open.All(o => o.PurchaseOrderLineId != pol.PurchaseOrderLineId))
                    result.Add((line, outstanding));
            }
            return result;
        }

        var omIds = receipt.MaterialReceiptLines.Where(l => l.OrderMaterialId != null).Select(l => l.OrderMaterialId!.Value).ToList();
        var materials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(m => omIds.Contains(m.OrderMaterialId)).ToDictionaryAsync(m => m.OrderMaterialId, ct);
        var deliveries = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.OrderMaterialId != null && omIds.Contains(l.OrderMaterialId.Value)
                        && l.MaterialReceipt.PurchaseOrderId == null && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .Select(l => new { OrderMaterialId = l.OrderMaterialId!.Value, l.MaterialReceiptId, l.AcceptedQty })
            .ToListAsync(ct);
        foreach (var line in receipt.MaterialReceiptLines.Where(l => l.OrderMaterialId != null))
        {
            var om = materials[line.OrderMaterialId!.Value];
            var mine = deliveries.Where(d => d.OrderMaterialId == om.OrderMaterialId).ToList();
            var outstanding = om.CustomerQty - mine.Sum(d => d.AcceptedQty);
            if (mine.Select(d => d.MaterialReceiptId).Distinct().Count() >= (om.ExpectedDeliveryCount ?? 1)
                && outstanding > 0 && open.All(o => o.OrderMaterialId != om.OrderMaterialId))
                result.Add((line, outstanding));
        }
        return result;
    }

    private async Task StageShortDeliveryAsync(MaterialReceipt receipt, IReadOnlyList<(MaterialReceiptLine Line, decimal Qty)> shortages,
        CancellationToken ct)
    {
        var draft = new RequestDraft(RequestTypes.ShortDelivery, receipt.OrderId, null, null, null,
            receipt.MaterialReceiptId, null, null, $"Thiếu NVL — phiếu {receipt.ReceiptCode}",
            WarehouseMessages.ShortDeliveryCreated, Array.Empty<RequestLineInput>(), null);
        var request = await _requests.StageSystemRequestAsync(draft, ct);
        foreach (var (line, qty) in shortages)
        {
            request.RequestLines.Add(new RequestLine
            {
                MaterialId = line.OrderMaterial?.MaterialId ?? line.MaterialId,
                Quantity = qty,
                MaterialReceiptLineId = line.MaterialReceiptLineId,
            });
        }
    }

    /// <summary>Số đã nhận bù của từng dòng phiếu gốc (phiếu bổ sung đã xác nhận; có thể gồm cả phiếu đang chờ).</summary>
    private async Task<Dictionary<int, decimal>> ReplenishedByLineAsync(int shortageRequestId, bool includePending,
        CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query()
            .Where(l => l.OriginalReceiptLineId != null && l.MaterialReceipt.ShortageRequestId == shortageRequestId
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

    private sealed record ResolvedLine(int? OrderMaterialId, int? PurchaseOrderLineId, int? MaterialId, decimal AcceptedQty);

    private sealed record ResolvedReceipt(int? OrderId, int? PurchaseOrderId, IReadOnlyList<ResolvedLine> Lines);

    private Task<Result<ResolvedReceipt>> ResolveAsync(SaveMaterialReceiptDto dto, int? exceptReceiptId, CancellationToken ct) =>
        dto.Source == MaterialSources.Supplier ? ResolveSupplierAsync(dto, exceptReceiptId, ct) : ResolveCustomerAsync(dto, exceptReceiptId, ct);

    /// <summary>
    /// Một NVL chỉ có một phiếu chờ xác nhận tại một thời điểm: tránh Thủ kho gửi nhiều lần cùng một lô rồi Quản đốc
    /// xác nhận cả loạt làm tồn kho tăng gấp. Phiếu bổ sung (có ShortageRequestId) và phiếu đã trả lại không tính.
    /// </summary>
    private async Task<Result<ResolvedReceipt>?> CheckNoPendingDuplicateAsync(IReadOnlyCollection<int> orderMaterialIds,
        IReadOnlyCollection<int> purchaseLineIds, int? exceptReceiptId, CancellationToken ct)
    {
        var orderMaterials = orderMaterialIds.ToArray();
        var purchaseLines = purchaseLineIds.ToArray();
        var codes = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReceipt.Status == ReceiptStatuses.Pending && l.MaterialReceipt.ShortageRequestId == null
                        && (exceptReceiptId == null || l.MaterialReceiptId != exceptReceiptId)
                        && ((l.PurchaseOrderLineId != null && purchaseLines.Contains(l.PurchaseOrderLineId.Value))
                            || (l.PurchaseOrderLineId == null && l.OrderMaterialId != null && orderMaterials.Contains(l.OrderMaterialId.Value))))
            .Select(l => l.MaterialReceipt.ReceiptCode).Distinct().ToListAsync(ct);
        return codes.Count == 0
            ? null
            : Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptDuplicatePending,
                string.Format(WarehouseMessages.ReceiptDuplicatePending, string.Join(", ", codes)));
    }

    private async Task<Result<ResolvedReceipt>> ResolveCustomerAsync(SaveMaterialReceiptDto dto, int? exceptReceiptId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<ResolvedReceipt>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var ids = dto.Lines.Select(l => l.OrderMaterialId ?? 0).ToList();
        if (ids.Any(i => i <= 0))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReceiptLineTarget);
        var materials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => ids.Contains(om.OrderMaterialId) && om.OrderId == dto.OrderId).ToListAsync(ct);
        if (materials.Count != ids.Count)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderMaterialNotInOrder);

        var duplicate = await CheckNoPendingDuplicateAsync(ids, [], exceptReceiptId, ct);
        if (duplicate is not null) return duplicate;

        // Số nhận không vượt số khách cấp còn phải nhận (đã trừ các phiếu khách cấp đã xác nhận).
        var receivedByMaterial = await ConfirmedCustomerReceivedAsync(ids, null, ct);
        var over = dto.Lines.Select(l => (Line: l, Om: materials.First(m => m.OrderMaterialId == l.OrderMaterialId)))
            .Select(x => (x.Line, Outstanding: x.Om.CustomerQty - receivedByMaterial.GetValueOrDefault(x.Om.OrderMaterialId)))
            .Where(x => x.Line.AcceptedQty > x.Outstanding)
            .Select(x => $"còn {x.Outstanding:0.###}, nhập {x.Line.AcceptedQty:0.###}").ToList();
        if (over.Count > 0)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptExceedsPurchase,
                $"{WarehouseMessages.ReceiptExceedsCustomer}: {string.Join("; ", over)}.");

        return Result<ResolvedReceipt>.Success(new ResolvedReceipt(order.OrderId, null,
            dto.Lines.Select(l => new ResolvedLine(l.OrderMaterialId, null, null, l.AcceptedQty)).ToList()));
    }

    private async Task<Result<ResolvedReceipt>> ResolveSupplierAsync(SaveMaterialReceiptDto dto, int? exceptReceiptId, CancellationToken ct)
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

        var duplicate = await CheckNoPendingDuplicateAsync([], dto.Lines.Select(l => l.PurchaseOrderLineId!.Value).ToList(), exceptReceiptId, ct);
        if (duplicate is not null) return duplicate;

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
                    ? new ResolvedLine(pol.OrderMaterialId, pol.PurchaseOrderLineId, null, l.AcceptedQty)
                    : new ResolvedLine(null, pol.PurchaseOrderLineId, pol.MaterialId, l.AcceptedQty);
            }).ToList()));
    }

    private static void Apply(MaterialReceipt receipt, SaveMaterialReceiptDto dto, ResolvedReceipt resolved)
    {
        receipt.OrderId = resolved.OrderId;
        receipt.PurchaseOrderId = resolved.PurchaseOrderId;
        receipt.ReceiptDate = dto.ReceiptDate;
        receipt.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        receipt.MaterialReceiptLines = resolved.Lines.Select(l => new MaterialReceiptLine
        {
            OrderMaterialId = l.OrderMaterialId,
            PurchaseOrderLineId = l.PurchaseOrderLineId,
            MaterialId = l.MaterialId,
            AcceptedQty = l.AcceptedQty,
        }).ToList();
    }
}
