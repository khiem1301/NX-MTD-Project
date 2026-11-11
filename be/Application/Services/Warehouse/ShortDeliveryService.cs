using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC08 / UC39 (TH-13): xử lý đơn SHORT_DELIVERY do hệ thống tạo khi nhận đủ số lần giao dự kiến mà còn thiếu hàng.
/// Quản đốc chọn <c>REPLENISH</c> (bên giao bù, Thủ kho lập phiếu bổ sung) hoặc <c>NO_REPLENISH</c> (chốt không bù:
/// <c>PurchaseOrderLine.NotReplenishedQty</c> tăng, đơn mua có thể thành CLOSED_SHORT).
/// </summary>
public sealed class ShortDeliveryService : IShortDeliveryService
{
    private readonly IUnitOfWork _uow;
    private readonly IRequestService _requests;
    private readonly MaterialSupplyUpdater _supply;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly DeliveryCountGuard _deliveries;
    private readonly ShortageTracker _shortages;
    private readonly IClock _clock;

    public ShortDeliveryService(IUnitOfWork uow, IRequestService requests, MaterialSupplyUpdater supply,
        INotificationService notifier, IAuditLogService audit, DeliveryCountGuard deliveries, ShortageTracker shortages, IClock clock)
    {
        _uow = uow; _requests = requests; _supply = supply; _notifier = notifier; _audit = audit;
        _deliveries = deliveries; _shortages = shortages; _clock = clock;
    }

    private IQueryable<Request> ShortDeliveries =>
        _uow.Repository<Request>().Query().Where(q => q.RequestTypeCode == RequestTypes.ShortDelivery);

    public async Task<Result<PagedResult<ShortDeliveryListItemDto>>> GetPagedAsync(ShortDeliveryQuery query,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await ShortDeliveries.AsNoTracking()
            .Where(q => query.AwaitingReplenish != true
                        || (q.Status == RequestStatuses.PendingProcessing && q.ShortageResolution == ShortageResolutions.Replenish))
            .Where(q => query.ResolvedOnly != true || q.ShortageResolution != null)
            .Where(q => string.IsNullOrEmpty(keyword) || q.RequestCode.Contains(keyword)
                        || q.MaterialReceipt!.ReceiptCode.Contains(keyword))
            .OrderByDescending(q => q.RequestId)
            .Select(q => new ShortDeliveryListItemDto(q.RequestId, q.RequestCode, q.Status, q.ShortageResolution,
                q.MaterialReceiptId!.Value, q.MaterialReceipt!.ReceiptCode, q.MaterialReceipt.Source!,
                q.MaterialReceipt.PurchaseOrder == null ? null : q.MaterialReceipt.PurchaseOrder.PoCode,
                q.Order == null ? null : q.Order.OrderCode, q.CreatedAt))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<ShortDeliveryListItemDto>>.Success(page);
    }

    public async Task<Result<ShortDeliveryDto>> GetByIdAsync(int requestId, CancellationToken ct)
    {
        var head = await ShortDeliveries.AsNoTracking().Where(q => q.RequestId == requestId)
            .Select(q => new
            {
                q.RequestId, q.RequestCode, q.Status, q.ShortageResolution, q.ResultNote, ReceiptId = q.MaterialReceiptId,
                q.ExpectedDeliveryCount, q.TargetDate, q.TargetDateTo,
                ReceiptCode = q.MaterialReceipt!.ReceiptCode, Source = q.MaterialReceipt.Source!,
                PoCode = q.MaterialReceipt.PurchaseOrder == null ? null : q.MaterialReceipt.PurchaseOrder.PoCode,
                OrderCode = q.Order == null ? null : q.Order.OrderCode, q.CreatedAt,
                Lines = q.RequestLines.OrderBy(l => l.RequestLineId).Select(l => new
                {
                    l.RequestLineId, l.MaterialReceiptLineId, MaterialId = l.MaterialId!.Value,
                    l.Material!.MaterialCode, l.Material.MaterialName, l.Material.Unit,
                    Color = l.Material.Color, l.Quantity, l.ReplenishQty,
                }).ToList(),
            }).FirstOrDefaultAsync(ct);
        if (head is null) return WarehouseFailures.NotFound<ShortDeliveryDto>();

        var done = await _shortages.ReplenishedAsync(requestId, includePending: false, ct);
        var deliveredCount = await _deliveries.ReplenishCountAsync(requestId, ct);
        return Result<ShortDeliveryDto>.Success(new ShortDeliveryDto(head.RequestId, head.RequestCode, head.Status,
            head.ShortageResolution, head.ResultNote, head.ReceiptId!.Value, head.ReceiptCode, head.Source, head.PoCode,
            head.OrderCode, head.CreatedAt,
            head.Lines.Select(l => new ShortDeliveryLineDto(l.RequestLineId, l.MaterialReceiptLineId, l.MaterialId,
                l.MaterialCode, l.MaterialName, l.Unit, l.Color, l.Quantity, l.ReplenishQty,
                done.GetValueOrDefault(l.RequestLineId))).ToList(),
            head.ExpectedDeliveryCount, deliveredCount, head.TargetDate, head.TargetDateTo));
    }

    public Task<Result> ResolveAsync(int requestId, ResolveShortDeliveryDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var request = await ShortDeliveries.Include(q => q.RequestLines)
                .Include(q => q.MaterialReceipt)
                .FirstOrDefaultAsync(q => q.RequestId == requestId, ct);
            if (request is null) return WarehouseFailures.NotFound();
            if (request.Status != RequestStatuses.PendingProcessing || request.ShortageResolution is not null)
                return Result.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotResolvable);

            return dto.Resolution == ShortageResolutions.Replenish
                ? await ReplenishAsync(request, dto, employeeId, ct)
                : await CloseWithoutReplenishAsync(request, dto, employeeId, ct);
        });

    /// <summary>
    /// Giao bù: lưu số bù từng dòng, số lần và ngày / khoảng ngày dự kiến giao bù (DEV-ALL-94). Đơn có thể được chọn giao bù lại
    /// khi đã nhận đủ số lần giao bù mà còn thiếu: số lần giao bù cộng dồn (đã giao + số lần đợt này) để giới hạn phiếu bổ sung.
    /// </summary>
    private async Task<Result> ReplenishAsync(Request request, ResolveShortDeliveryDto dto, int employeeId,
        CancellationToken ct)
    {
        var dateError = new[] { dto.ExpectedDeliveryDate, ReplenishDateTo(dto) }.OfType<DateOnly>()
            .Select(d => DocumentDateRules.CheckExpectedDelivery(d, _clock.Today, null, null)).FirstOrDefault(e => e is not null);
        if (dateError is not null) return Result.Failure(ErrorCodes.WarehouseInvalidReference, dateError, ResultKind.Validation);
        var linesError = await ApplyReplenishQtyAsync(request, dto, ct);
        if (linesError is not null) return linesError;

        var delivered = await _deliveries.ReplenishCountAsync(request.RequestId, ct);
        request.ExpectedDeliveryCount = (short)(delivered + dto.ExpectedDeliveryCount!.Value);
        request.TargetDate = dto.ExpectedDeliveryDate;
        request.TargetDateTo = ReplenishDateTo(dto);
        request.ShortageResolution = ShortageResolutions.Replenish;
        _audit.Add(new AuditEntry(nameof(Request), request.RequestId, AuditActions.Update, null,
            new { request.ShortageResolution, request.ExpectedDeliveryCount, request.TargetDate, request.TargetDateTo },
            dto.Note?.Trim()));
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            WarehouseMessages.NotifyReplenishTitle,
            string.Format(WarehouseMessages.NotifyReplenishBody, request.RequestCode, dto.ExpectedDeliveryCount,
                DeliveryWindow(request.TargetDate, request.TargetDateTo)),
            null, nameof(Request), request.RequestId), ct);
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ShortageResolved);
    }

    private static DateOnly? ReplenishDateTo(ResolveShortDeliveryDto dto) =>
        DocumentDateRules.IsRangeDelivery(dto.ExpectedDeliveryCount) ? dto.ExpectedDeliveryDateTo : null;

    private static string DeliveryWindow(DateOnly? from, DateOnly? to) =>
        to is { } end
            ? string.Format(WarehouseMessages.DeliveryWindowRange, from, end)
            : string.Format(WarehouseMessages.DeliveryWindowSingle, from);

    /// <summary>
    /// Số bù đợt này của từng dòng không vượt phần thiếu chưa nhận bù (không nhập số thì mặc định bù đủ phần đó);
    /// lưu <c>ReplenishQty</c> = đã nhận bù + số bù đợt này để phiếu bổ sung và việc hoàn thành đơn tính đúng.
    /// </summary>
    private async Task<Result?> ApplyReplenishQtyAsync(Request request, ResolveShortDeliveryDto dto, CancellationToken ct)
    {
        var input = (dto.Lines ?? Array.Empty<ReplenishInputDto>()).ToDictionary(l => l.RequestLineId);
        if (input.Keys.Any(id => request.RequestLines.All(l => l.RequestLineId != id)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ShortageResolutionInvalid);

        var done = await _shortages.ReplenishedAsync(request.RequestId, includePending: false, ct);
        var anyQty = false;
        foreach (var line in request.RequestLines)
        {
            var received = done.GetValueOrDefault(line.RequestLineId);
            var remaining = Math.Max(0m, line.Quantity - received);
            var qty = input.TryGetValue(line.RequestLineId, out var i) ? i.ReplenishQty : remaining;
            if (qty > remaining)
                return Result.Failure(ErrorCodes.ShortageExceeded,
                    $"{WarehouseMessages.ReplenishExceedsShortage}: tối đa {remaining:0.###}.");
            line.ReplenishQty = received + qty;
            anyQty |= qty > 0;
        }
        return anyQty ? null : Result.Failure(ErrorCodes.ShortageExceeded, WarehouseMessages.ShortageResolutionInvalid);
    }

    private async Task<Result> CloseWithoutReplenishAsync(Request request, ResolveShortDeliveryDto dto, int employeeId,
        CancellationToken ct)
    {
        var purchaseLineIds = request.RequestLines.Where(l => l.PurchaseOrderLineId != null)
            .Select(l => l.PurchaseOrderLineId!.Value).Distinct().ToList();
        var purchaseLines = await _uow.Repository<PurchaseOrderLine>().Query()
            .Where(l => purchaseLineIds.Contains(l.PurchaseOrderLineId)).ToDictionaryAsync(l => l.PurchaseOrderLineId, ct);
        var received = await _supply.ReceivedByPurchaseLineAsync(purchaseLineIds, ct);

        foreach (var line in request.RequestLines)
        {
            line.ReplenishQty = 0;
            if (line.PurchaseOrderLineId is not { } polId || !purchaseLines.TryGetValue(polId, out var pol))
                continue;   // phiếu khách cấp: không có đơn mua để chốt, chỉ ghi nhận không bù
            // Đã nhận + không bù không vượt số đặt (CK_POL_Qty chặn NotReplenishedQty > OrderedQty).
            var room = pol.OrderedQty - received.GetValueOrDefault(polId) - pol.NotReplenishedQty;
            pol.NotReplenishedQty += Math.Max(0, Math.Min(line.Quantity, room));
        }

        await ReduceCustomerSupplyAsync(request, dto.Note?.Trim(), ct);
        request.ShortageResolution = ShortageResolutions.NoReplenish;
        await _uow.SaveChangesAsync();   // lưu số "không bù" trước khi tính lại trạng thái đơn mua / NVL của đơn
        if (request.MaterialReceipt?.PurchaseOrderId is { } poId) await _supply.RecalculatePurchaseOrderAsync(poId, ct);
        if (request.OrderId is { } orderId) await _supply.RecalculateMaterialStatusAsync(orderId, ct);

        _audit.Add(new AuditEntry(nameof(Request), request.RequestId, AuditActions.Update, null,
            new { request.ShortageResolution }, dto.Note?.Trim()));
        var done = await _requests.MarkCompletedAsync(request.RequestId, dto.Note?.Trim() ?? "Chốt không bù.", ct);
        if (!done.IsSuccess) return done;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ShortageResolved);
    }

    /// <summary>
    /// Phiếu khách cấp chốt không bù: khách không cấp phần còn thiếu nữa nên số khách cấp giảm xuống bằng số đã nhận thật, phần thiếu
    /// tự thành số phải mua (cột tính PurchaseQty). Lịch khách giao khai ở cả đơn nên không đổi ở đây (DEV-ALL-95).
    /// </summary>
    private async Task ReduceCustomerSupplyAsync(Request request, string? note, CancellationToken ct)
    {
        var ids = request.RequestLines.Where(l => l.PurchaseOrderLineId == null && l.OrderMaterialId != null)
            .Select(l => l.OrderMaterialId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var materials = await _uow.Repository<OrderMaterial>().Query().Where(m => ids.Contains(m.OrderMaterialId)).ToListAsync(ct);
        var received = await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.OrderMaterialId != null && ids.Contains(l.OrderMaterialId.Value) && l.PurchaseOrderLineId == null
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OrderMaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        foreach (var material in materials)
        {
            var actual = received.GetValueOrDefault(material.OrderMaterialId);
            if (actual >= material.CustomerQty) continue;
            var before = new { material.CustomerQty };
            material.CustomerQty = actual;
            _audit.Add(new AuditEntry(nameof(OrderMaterial), material.OrderMaterialId, AuditActions.Update, before,
                new { material.CustomerQty }, note));
        }
    }
}

/// <summary>SHORT_DELIVERY chỉ do hệ thống tạo và chỉ đóng bằng "giao bù đủ" hoặc "chốt không bù" (không bấm hoàn thành tay).</summary>
public sealed class ShortDeliveryRequestHandler : IRequestTypeHandler
{
    public string RequestType => RequestTypes.ShortDelivery;
    public bool AllowManualComplete => false;
    public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct) => Task.FromResult(Result.Success());
    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) =>
        Task.FromResult(Result.Success());
}
