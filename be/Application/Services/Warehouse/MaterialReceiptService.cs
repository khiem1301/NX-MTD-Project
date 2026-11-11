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
    private readonly INotificationService _notifier;
    private readonly DeliveryCountGuard _deliveries;
    private readonly ShortageTracker _shortages;
    private readonly ILogger<MaterialReceiptService> _logger;

    public MaterialReceiptService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        IRequestService requests, MaterialSupplyUpdater supply, IClock clock, INotificationService notifier,
        DeliveryCountGuard deliveries, ShortageTracker shortages, ILogger<MaterialReceiptService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _requests = requests; _supply = supply; _clock = clock; _notifier = notifier;
        _deliveries = deliveries; _shortages = shortages; _logger = logger;
    }

    private const string ReceiptLink = "/warehouse/material-receipts";

    /// <summary>Phiếu nhập chờ xác nhận: Quản đốc là người xử lý tiếp.</summary>
    private Task NotifyPendingAsync(MaterialReceipt receipt, CancellationToken ct) =>
        _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse, "Phiếu nhập chờ xác nhận",
            $"Thủ kho đã lập phiếu nhập {receipt.ReceiptCode}, vui lòng xác nhận hoặc trả lại.", ReceiptLink), ct);

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
                    l.AcceptedQty, null, null)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();

        var shortDeliveryId = await _uow.Repository<Request>().Query().AsNoTracking()
            .Where(q => q.MaterialReceiptId == receiptId && q.RequestTypeCode == RequestTypes.ShortDelivery)
            .Select(q => (int?)q.RequestId).FirstOrDefaultAsync(ct);

        dto = await WithReplenishQtyAsync(await WithDeliveryProgressAsync(dto, ct), ct);
        return Result<MaterialReceiptDto>.Success(dto with { ShortDeliveryRequestId = shortDeliveryId });
    }

    /// <summary>
    /// Phiếu thường: lần giao thứ mấy / số lần giao dự kiến của cả đơn mua hoặc đơn hàng (khách cấp) — DEV-ALL-95. Mỗi dòng: số còn
    /// thiếu sau phiếu này (Cần bù) = đặt − không bù (đơn mua) hoặc khách cấp − đã nhận ở các phiếu đã xác nhận trước phiếu này − số
    /// phiếu này. Phiếu chưa xác nhận tính mọi phiếu đã xác nhận là "trước".
    /// </summary>
    private async Task<MaterialReceiptDto> WithDeliveryProgressAsync(MaterialReceiptDto dto, CancellationToken ct)
    {
        var polIds = dto.Lines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToList();
        var omIds = dto.Lines.Where(l => l.PurchaseOrderLineId == null && l.OrderMaterialId != null)
            .Select(l => l.OrderMaterialId!.Value).ToList();
        if (polIds.Count == 0 && omIds.Count == 0) return dto;

        var receiptId = dto.MaterialReceiptId;
        var confirmedAt = dto.Status == ReceiptStatuses.Confirmed ? dto.ConfirmedAt : null;
        var earlier = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReceiptId != receiptId && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed
                        && (confirmedAt == null || l.MaterialReceipt.ConfirmedAt < confirmedAt
                            || (l.MaterialReceipt.ConfirmedAt == confirmedAt && l.MaterialReceiptId < receiptId))
                        && ((l.PurchaseOrderLineId != null && polIds.Contains(l.PurchaseOrderLineId.Value))
                            || (l.PurchaseOrderLineId == null && l.OrderMaterialId != null && omIds.Contains(l.OrderMaterialId.Value))))
            .Select(l => new { l.PurchaseOrderLineId, l.OrderMaterialId, l.AcceptedQty })
            .ToListAsync(ct);
        var (purchaseQty, customerQty) = await ExpectedQtyAsync(polIds, omIds, ct);
        var (deliveryNo, deliveryCount) = dto.ShortageRequestId is null ? await DeliveryNoAsync(dto, ct) : (null, null);

        return dto with
        {
            Lines = dto.Lines.Select(l =>
            {
                var (expected, before) = l.PurchaseOrderLineId is { } pol && purchaseQty.TryGetValue(pol, out var p)
                    ? (p, earlier.Where(e => e.PurchaseOrderLineId == pol).ToList())
                    : l.PurchaseOrderLineId == null && l.OrderMaterialId is { } om && customerQty.TryGetValue(om, out var m)
                        ? (m, earlier.Where(e => e.PurchaseOrderLineId == null && e.OrderMaterialId == om).ToList())
                        : (0m, null);
                if (before is null) return l;
                return l with
                {
                    ToReplenishQty = Math.Max(0m, expected - before.Sum(e => e.AcceptedQty) - l.AcceptedQty),
                    DeliveryNo = deliveryNo,
                    ExpectedDeliveryCount = deliveryCount,
                };
            }).ToList(),
        };
    }

    /// <summary>Số phải nhận của từng dòng đơn mua (đặt − không bù) / NVL khách cấp (số khách cấp).</summary>
    private async Task<(Dictionary<int, decimal> Purchase, Dictionary<int, decimal> Customer)> ExpectedQtyAsync(
        IReadOnlyCollection<int> polIds, IReadOnlyCollection<int> omIds, CancellationToken ct)
    {
        var purchase = await _uow.Repository<PurchaseOrderLine>().Query().AsNoTracking()
            .Where(p => polIds.Contains(p.PurchaseOrderLineId))
            .ToDictionaryAsync(p => p.PurchaseOrderLineId, p => p.OrderedQty - p.NotReplenishedQty, ct);
        var customer = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(m => omIds.Contains(m.OrderMaterialId))
            .ToDictionaryAsync(m => m.OrderMaterialId, m => m.CustomerQty, ct);
        return (purchase, customer);
    }

    /// <summary>Phiếu thường là lần giao thứ mấy của đơn mua / đơn hàng (đếm phiếu thường đã xác nhận trước nó) và số lần dự kiến.</summary>
    private async Task<(int? No, int? Count)> DeliveryNoAsync(MaterialReceiptDto dto, CancellationToken ct)
    {
        var receiptId = dto.MaterialReceiptId;
        var confirmedAt = dto.Status == ReceiptStatuses.Confirmed ? dto.ConfirmedAt : null;
        var earlier = Receipts.AsNoTracking().Where(r => r.MaterialReceiptId != receiptId && r.Status == ReceiptStatuses.Confirmed
            && r.ShortageRequestId == null
            && (confirmedAt == null || r.ConfirmedAt < confirmedAt || (r.ConfirmedAt == confirmedAt && r.MaterialReceiptId < receiptId)));
        if (dto.PurchaseOrderId is { } poId)
        {
            var count = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking().Where(p => p.PurchaseOrderId == poId)
                .Select(p => p.ExpectedDeliveryCount).FirstOrDefaultAsync(ct);
            return (await earlier.CountAsync(r => r.PurchaseOrderId == poId, ct) + 1, count ?? 1);
        }
        if (dto.OrderId is not { } orderId) return (null, null);
        var planned = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().Where(o => o.OrderId == orderId)
            .Select(o => o.CustomerDeliveryCount).FirstOrDefaultAsync(ct);
        return (await earlier.CountAsync(r => r.OrderId == orderId && r.PurchaseOrderId == null, ct) + 1, planned ?? 1);
    }

    /// <summary>Phiếu bổ sung: số cần bù của từng dòng = ReplenishQty Quản đốc đã chọn trên dòng đơn thiếu NVL cùng nguồn.</summary>
    private async Task<MaterialReceiptDto> WithReplenishQtyAsync(MaterialReceiptDto dto, CancellationToken ct)
    {
        if (dto.ShortageRequestId is not { } shortageId) return dto;
        var requestLines = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(q => q.RequestId == shortageId && q.ReplenishQty != null).ToListAsync(ct);
        return dto with
        {
            Lines = dto.Lines.Select(l =>
                requestLines.FirstOrDefault(q => ShortageTracker.SameSource(q, l.PurchaseOrderLineId, l.OrderMaterialId)) is { } q
                    ? l with { ToReplenishQty = q.ReplenishQty } : l).ToList(),
        };
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
            await NotifyPendingAsync(receipt, ct);
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
        await NotifyPendingAsync(receipt, ct);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(receiptId, ct);
    }

    /// <summary>Phiếu bổ sung theo đơn thiếu NVL đã chọn "giao bù": nhận bù ≤ số còn phải bù của từng dòng.</summary>
    public async Task<Result<MaterialReceiptDto>> CreateSupplementaryAsync(SaveSupplementaryReceiptDto dto,
        CancellationToken ct)
    {
        var request = await _uow.Repository<Request>().Query().Include(q => q.RequestLines)
            .FirstOrDefaultAsync(q => q.RequestId == dto.ShortageRequestId && q.RequestTypeCode == RequestTypes.ShortDelivery, ct);
        var notResolvable = CheckReplenishable(request);
        if (notResolvable is not null) return notResolvable;

        var original = await Receipts.AsNoTracking().FirstAsync(r => r.MaterialReceiptId == request!.MaterialReceiptId, ct);
        var supplementaryDate = await CheckReceiptDateAsync(dto.ReceiptDate, original.OrderId, null, ct);
        if (supplementaryDate is not null)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.WarehouseInvalidReference, supplementaryDate, ResultKind.Validation);
        var invalid = await CheckSupplementaryLinesAsync(dto, request!, ct);
        if (invalid is not null) return invalid;
        var countReached = await _deliveries.CheckReplenishAsync(request!.RequestId, ct);
        if (countReached is not null)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ReceiptDeliveryCountReached, countReached);

        var created = await _uow.InTransactionAsync(() => AddSupplementaryReceiptAsync(dto, request!, original, ct));
        return await GetByIdAsync(created.Data, ct);
    }

    /// <summary>Đơn thiếu NVL phải tồn tại, gắn phiếu nhập gốc, đang chờ xử lý và đã chọn hướng giao bù.</summary>
    private static Result<MaterialReceiptDto>? CheckReplenishable(Request? request)
    {
        if (request is null || request.MaterialReceiptId is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();
        if (request.Status != RequestStatuses.PendingProcessing)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotResolvable);
        if (request.ShortageResolution != ShortageResolutions.Replenish)
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotReplenish);
        return null;
    }

    /// <summary>Mỗi dòng phải thuộc đơn thiếu NVL và nhận bù không vượt số còn phải bù (đã tính cả phiếu đang chờ).</summary>
    private async Task<Result<MaterialReceiptDto>?> CheckSupplementaryLinesAsync(SaveSupplementaryReceiptDto dto, Request request,
        CancellationToken ct)
    {
        var requestLines = request.RequestLines.Where(l => l.ReplenishQty > 0).ToDictionary(l => l.RequestLineId);
        if (dto.Lines.Any(l => !requestLines.ContainsKey(l.RequestLineId)))
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReceiptLineTarget);

        var done = await _shortages.ReplenishedAsync(request.RequestId, includePending: true, ct);
        var remaining = (SupplementaryLineDto l) => requestLines[l.RequestLineId].ReplenishQty!.Value - done.GetValueOrDefault(l.RequestLineId);
        var over = dto.Lines.Where(l => l.AcceptedQty > remaining(l))
            .Select(l => $"còn {remaining(l):0.###}, nhận {l.AcceptedQty:0.###}")
            .ToList();
        return over.Count > 0
            ? Result<MaterialReceiptDto>.Failure(ErrorCodes.ShortageExceeded,
                $"{WarehouseMessages.SupplementaryExceedsReplenish}: {string.Join("; ", over)}.")
            : null;
    }

    /// <summary>
    /// Phiếu bổ sung cùng đơn hàng / đơn mua với phiếu gốc; mỗi dòng lấy nguồn từ dòng đơn thiếu NVL (dòng đơn mua hoặc NVL khách
    /// cấp — kể cả NVL chưa về lần nào, khi đó không có dòng phiếu gốc).
    /// </summary>
    private async Task<Result<int>> AddSupplementaryReceiptAsync(SaveSupplementaryReceiptDto dto, Request request,
        MaterialReceipt original, CancellationToken ct)
    {
        var requestLines = request.RequestLines.ToDictionary(l => l.RequestLineId);
        var polIds = request.RequestLines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToArray();
        var purchaseLines = await _uow.Repository<PurchaseOrderLine>().Query().AsNoTracking()
            .Where(p => polIds.Contains(p.PurchaseOrderLineId)).ToDictionaryAsync(p => p.PurchaseOrderLineId, ct);
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
                var src = requestLines[l.RequestLineId];
                var pol = src.PurchaseOrderLineId is { } id ? purchaseLines[id] : null;
                var orderMaterialId = pol is null ? src.OrderMaterialId : pol.OrderMaterialId;
                return new MaterialReceiptLine
                {
                    OrderMaterialId = orderMaterialId,
                    PurchaseOrderLineId = src.PurchaseOrderLineId,
                    OriginalReceiptLineId = src.MaterialReceiptLineId,
                    MaterialId = orderMaterialId is null ? pol?.MaterialId : null,
                    AcceptedQty = l.AcceptedQty,
                };
            }).ToList(),
        };
        await _uow.Repository<MaterialReceipt>().AddAsync(receipt);
        await NotifyPendingAsync(receipt, ct);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(receipt.MaterialReceiptId);
    }

    public Task<Result> ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var receipt = await Receipts.Include(r => r.MaterialReceiptLines).ThenInclude(l => l.OrderMaterial)
                .FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
            if (receipt is null) return WarehouseFailures.NotFound();
            if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();
            var blocked = await CheckConfirmableAsync(receipt, ct);
            if (blocked is not null) return blocked;

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

            var message = await HandleShortageAsync(receipt, ct);
            await _uow.SaveChangesAsync();
            _logger.LogInformation("Material receipt {ReceiptCode} confirmed by employee {EmployeeId}",
                receipt.ReceiptCode, confirmedByEmployeeId);
            return Result.Success(message);
        });

    /// <summary>
    /// Kiểm lại ngay lúc xác nhận (phiếu có thể lập trước khi phiếu khác được xác nhận): tổng đã nhận không vượt số đặt (trừ phần
    /// chốt không bù) hoặc số khách cấp, chưa vượt số lần giao (DEV-ALL-94), và đã có ảnh chứng từ (swimlane III.1.4).
    /// </summary>
    private async Task<Result?> CheckConfirmableAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        var overLimit = await FindOverLimitAsync(receipt, ct);
        if (overLimit is not null) return Result.Failure(ErrorCodes.ReceiptExceedsPurchase, overLimit);
        var countReached = await _deliveries.CheckReceiptAsync(receipt, ct);
        if (countReached is not null) return Result.Failure(ErrorCodes.ReceiptDeliveryCountReached, countReached);
        var hasPhoto = await _uow.Repository<Attachment>().Query().AnyAsync(a =>
            a.OwnerType == AttachmentOwnerTypes.MaterialReceipt && a.OwnerId == receipt.MaterialReceiptId && a.IsCurrent, ct);
        return hasPhoto ? null : Result.Failure(ErrorCodes.ReceiptPhotoRequired, WarehouseMessages.ReceiptPhotoRequired);
    }

    /// <summary>
    /// Sau khi phiếu đã xác nhận: phiếu bổ sung thì đóng / trả về đơn thiếu NVL; phiếu thường thì sinh đơn thiếu NVL cho mọi NVL còn
    /// thiếu của đơn nếu đơn đã đủ số lần giao (DEV-ALL-95). Trả về thông báo kết quả.
    /// </summary>
    private async Task<string> HandleShortageAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        var message = WarehouseMessages.ReceiptConfirmed;
        if (receipt.ShortageRequestId is { } requestId)
            return await SettleReplenishAsync(requestId, ct) ? $"{message} {WarehouseMessages.ReplenishRoundReopened}" : message;

        var shortages = await _shortages.FindAsync(receipt, ct);
        if (shortages.Count == 0) return message;
        await _shortages.StageAsync(receipt, shortages, ct);
        return $"{message} {WarehouseMessages.ShortDeliveryCreated}";   // MSG31
    }

    public async Task<Result> ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct)
    {
        var receipt = await Receipts.FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
        if (receipt is null) return WarehouseFailures.NotFound();
        if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();

        receipt.Status = ReceiptStatuses.Returned;
        receipt.ReturnReason = dto.Reason.Trim();
        // Thủ kho phải sửa lại phiếu theo lý do trả.
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu nhập bị trả lại", $"Quản đốc trả lại phiếu nhập {receipt.ReceiptCode}: {receipt.ReturnReason}", ReceiptLink), ct);
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

    // ---------- Giao bù: đóng / trả về đơn thiếu NVL ----------

    /// <summary>
    /// Nhận đủ hàng bù thì hoàn thành đơn thiếu NVL. Đã nhận đủ số lần giao bù dự kiến mà còn thiếu thì trả đơn về chờ Quản đốc
    /// quyết định lại (giao bù tiếp hoặc chốt không bù phần còn lại) — DEV-ALL-94. Trả true khi đơn bị trả về.
    /// </summary>
    private async Task<bool> SettleReplenishAsync(int requestId, CancellationToken ct)
    {
        var lines = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(l => l.RequestId == requestId && l.ReplenishQty > 0).ToListAsync(ct);
        var done = await _shortages.ReplenishedAsync(requestId, includePending: false, ct);
        if (lines.Count > 0 && lines.All(l => done.GetValueOrDefault(l.RequestLineId) >= l.ReplenishQty))
        {
            await _requests.MarkCompletedAsync(requestId, WarehouseMessages.ReplenishCompletedNote, ct);
            return false;
        }
        if (await _deliveries.CheckReplenishAsync(requestId, ct) is null) return false;

        var request = await _uow.Repository<Request>().Query().FirstAsync(q => q.RequestId == requestId, ct);
        request.ShortageResolution = null;
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
            WarehouseMessages.ReplenishRoundShortTitle,
            string.Format(WarehouseMessages.ReplenishRoundShort, request.ExpectedDeliveryCount, request.RequestCode),
            null, nameof(Request), request.RequestId), ct);
        return true;
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

    /// <summary>
    /// NVL khách cấp còn nhận phiếu thường được không: không có phiếu chờ trùng, Quản đốc chưa chốt không bù (hủy giao phần thiếu)
    /// và chưa nhận đủ số lần khách dự kiến giao (DEV-ALL-94).
    /// </summary>
    private async Task<Result<ResolvedReceipt>?> CheckCustomerReceivableAsync(int orderId, IReadOnlyCollection<int> ids,
        int? exceptReceiptId, CancellationToken ct)
    {
        var duplicate = await CheckNoPendingDuplicateAsync(ids, [], exceptReceiptId, ct);
        if (duplicate is not null) return duplicate;
        if ((await _supply.ClosedWithoutReplenishAsync(ids, ct)).Count > 0)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptShortageClosed, WarehouseMessages.ReceiptShortageClosed);
        var countReached = await _deliveries.CheckCustomerAsync(orderId, exceptReceiptId, ct);
        return countReached is null ? null : Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptDeliveryCountReached, countReached);
    }

    private async Task<Result<ResolvedReceipt>> ResolveCustomerAsync(SaveMaterialReceiptDto dto, int? exceptReceiptId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<ResolvedReceipt>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
        var dateError = DocumentDateRules.CheckDocumentDate(dto.ReceiptDate, _clock.Today, order.ReceivedDate, WarehouseMessages.LabelReceiptDate);
        if (dateError is not null)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, dateError, ResultKind.Validation);

        var ids = dto.Lines.Select(l => l.OrderMaterialId ?? 0).ToList();
        if (ids.Any(i => i <= 0))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReceiptLineTarget);
        var materials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => ids.Contains(om.OrderMaterialId) && om.OrderId == dto.OrderId).ToListAsync(ct);
        if (materials.Count != ids.Count)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderMaterialNotInOrder);

        var blocked = await CheckCustomerReceivableAsync(order.OrderId, ids, exceptReceiptId, ct);
        if (blocked is not null) return blocked;

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

    /// <summary>Ngày nhập của phiếu theo đơn mua hoặc phiếu bổ sung: quy tắc ngày chứng từ + không trước ngày đặt đơn mua.</summary>
    private async Task<string?> CheckReceiptDateAsync(DateOnly receiptDate, int? orderId, DateOnly? purchaseOrderDate, CancellationToken ct)
    {
        DateOnly? received = orderId is { } id
            ? await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
                .Where(o => o.OrderId == id).Select(o => (DateOnly?)o.ReceivedDate).FirstOrDefaultAsync(ct)
            : null;
        var error = DocumentDateRules.CheckDocumentDate(receiptDate, _clock.Today, received, WarehouseMessages.LabelReceiptDate);
        if (error is not null) return error;
        return purchaseOrderDate is { } placed && receiptDate < placed
            ? string.Format(WarehouseMessages.ReceiptBeforePurchaseDate, placed)
            : null;
    }

    /// <summary>Đơn mua phải đang đặt / nhận một phần và đơn hàng gắn với nó chưa đóng.</summary>
    private async Task<Result<ResolvedReceipt>?> CheckReceivableAsync(PurchaseOrder po, CancellationToken ct)
    {
        if (po.GoodsStatus is not (PurchaseGoodsStatuses.Ordered or PurchaseGoodsStatuses.PartiallyReceived))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.PurchaseOrderNotReceivable);
        if (po.OrderId is { } orderId)
        {
            var status = await _uow.Repository<CustomerOrder>().Query().Where(o => o.OrderId == orderId)
                .Select(o => o.Status).FirstAsync(ct);
            if (OrderStatuses.Closed.Contains(status))
                return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
        }
        return null;
    }

    private async Task<Result<ResolvedReceipt>> ResolveSupplierAsync(SaveMaterialReceiptDto dto, int? exceptReceiptId, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking().Include(p => p.PurchaseOrderLines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == dto.PurchaseOrderId, ct);
        if (po is null) return WarehouseFailures.NotFound<ResolvedReceipt>();
        var notReceivable = await CheckReceivableAsync(po, ct);
        if (notReceivable is not null) return notReceivable;
        var dateCheck = await CheckReceiptDateAsync(dto.ReceiptDate, po.OrderId, po.OrderDate, ct);
        if (dateCheck is not null) return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, dateCheck, ResultKind.Validation);

        var byId = po.PurchaseOrderLines.ToDictionary(l => l.PurchaseOrderLineId);
        if (dto.Lines.Any(l => l.PurchaseOrderLineId is null || !byId.ContainsKey(l.PurchaseOrderLineId.Value)))
            return Result<ResolvedReceipt>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.PurchaseLineNotInOrder);

        var duplicate = await CheckNoPendingDuplicateAsync([], dto.Lines.Select(l => l.PurchaseOrderLineId!.Value).ToList(), exceptReceiptId, ct);
        if (duplicate is not null) return duplicate;
        var countReached = await _deliveries.CheckPurchaseOrderAsync(po.PurchaseOrderId, exceptReceiptId, ct);
        if (countReached is not null)
            return Result<ResolvedReceipt>.Failure(ErrorCodes.ReceiptDeliveryCountReached, countReached);

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
