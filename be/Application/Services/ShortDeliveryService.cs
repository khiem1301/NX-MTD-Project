using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC08 / UC39 (TH-13): xử lý đơn SHORT_DELIVERY do hệ thống tạo khi phiếu nhập có hàng thiếu / lỗi.
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

    public ShortDeliveryService(IUnitOfWork uow, IRequestService requests, MaterialSupplyUpdater supply,
        INotificationService notifier, IAuditLogService audit)
    {
        _uow = uow; _requests = requests; _supply = supply; _notifier = notifier; _audit = audit;
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
            .Where(q => string.IsNullOrEmpty(keyword) || q.RequestCode.Contains(keyword)
                        || q.MaterialReceipt!.ReceiptCode.Contains(keyword))
            .OrderByDescending(q => q.RequestId)
            .Select(q => new ShortDeliveryListItemDto(q.RequestId, q.RequestCode, q.Status, q.ShortageResolution,
                q.MaterialReceiptId!.Value, q.MaterialReceipt!.ReceiptCode,
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
                ReceiptCode = q.MaterialReceipt!.ReceiptCode, q.MaterialReceipt.Source,
                PoCode = q.MaterialReceipt.PurchaseOrder == null ? null : q.MaterialReceipt.PurchaseOrder.PoCode,
                OrderCode = q.Order == null ? null : q.Order.OrderCode, q.CreatedAt,
                Lines = q.RequestLines.OrderBy(l => l.RequestLineId).Select(l => new
                {
                    l.RequestLineId, l.MaterialReceiptLineId, MaterialId = l.MaterialId!.Value,
                    l.Material!.MaterialCode, l.Material.MaterialName, l.Material.Unit,
                    Color = l.MaterialColor ?? string.Empty, l.Quantity, l.ReplenishQty,
                    ShortQty = l.MaterialReceiptLine!.ShortQty, DefectiveQty = l.MaterialReceiptLine.DefectiveQty,
                }).ToList(),
            }).FirstOrDefaultAsync(ct);
        if (head is null) return WarehouseFailures.NotFound<ShortDeliveryDto>();

        var done = await ReplenishedAsync(requestId, ct);
        return Result<ShortDeliveryDto>.Success(new ShortDeliveryDto(head.RequestId, head.RequestCode, head.Status,
            head.ShortageResolution, head.ResultNote, head.ReceiptId!.Value, head.ReceiptCode, head.Source, head.PoCode,
            head.OrderCode, head.CreatedAt,
            head.Lines.Select(l => new ShortDeliveryLineDto(l.RequestLineId, l.MaterialReceiptLineId!.Value, l.MaterialId,
                l.MaterialCode, l.MaterialName, l.Unit, l.Color, l.ShortQty, l.DefectiveQty, l.Quantity, l.ReplenishQty,
                done.GetValueOrDefault(l.MaterialReceiptLineId!.Value))).ToList()));
    }

    public Task<Result> ResolveAsync(int requestId, ResolveShortDeliveryDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var request = await ShortDeliveries.Include(q => q.RequestLines).ThenInclude(l => l.MaterialReceiptLine)
                .Include(q => q.MaterialReceipt)
                .FirstOrDefaultAsync(q => q.RequestId == requestId, ct);
            if (request is null) return WarehouseFailures.NotFound();
            if (request.Status != RequestStatuses.PendingProcessing || request.ShortageResolution is not null)
                return Result.Failure(ErrorCodes.ShortageNotResolvable, WarehouseMessages.ShortageNotResolvable);

            return dto.Resolution == ShortageResolutions.Replenish
                ? await ReplenishAsync(request, dto, employeeId, ct)
                : await CloseWithoutReplenishAsync(request, dto, employeeId, ct);
        });

    private async Task<Result> ReplenishAsync(Request request, ResolveShortDeliveryDto dto, int employeeId,
        CancellationToken ct)
    {
        var input = (dto.Lines ?? Array.Empty<ReplenishInputDto>()).ToDictionary(l => l.RequestLineId);
        if (input.Keys.Any(id => request.RequestLines.All(l => l.RequestLineId != id)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ShortageResolutionInvalid);

        // Không nhập số thì mặc định bù đủ phần thiếu / lỗi của từng dòng.
        foreach (var line in request.RequestLines)
        {
            var qty = input.TryGetValue(line.RequestLineId, out var i) ? i.ReplenishQty : line.Quantity;
            if (qty > line.Quantity)
                return Result.Failure(ErrorCodes.ShortageExceeded,
                    $"{WarehouseMessages.ReplenishExceedsShortage}: tối đa {line.Quantity:0.###}.");
            line.ReplenishQty = qty;
        }
        if (request.RequestLines.All(l => l.ReplenishQty is null or 0))
            return Result.Failure(ErrorCodes.ShortageExceeded, WarehouseMessages.ShortageResolutionInvalid);

        request.ShortageResolution = ShortageResolutions.Replenish;
        _audit.Add(new AuditEntry(nameof(Request), request.RequestId, AuditActions.Update, null,
            new { request.ShortageResolution }, dto.Note?.Trim()));
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Chờ nhận hàng giao bù", $"Quản đốc chọn giao bù cho {request.RequestCode}, lập phiếu bổ sung khi hàng về.",
            null, nameof(Request), request.RequestId), ct);
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ShortageResolved);
    }

    private async Task<Result> CloseWithoutReplenishAsync(Request request, ResolveShortDeliveryDto dto, int employeeId,
        CancellationToken ct)
    {
        var purchaseLineIds = request.RequestLines.Select(l => l.MaterialReceiptLine?.PurchaseOrderLineId)
            .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var purchaseLines = await _uow.Repository<PurchaseOrderLine>().Query()
            .Where(l => purchaseLineIds.Contains(l.PurchaseOrderLineId)).ToDictionaryAsync(l => l.PurchaseOrderLineId, ct);
        var received = await _supply.ReceivedByPurchaseLineAsync(purchaseLineIds, ct);

        foreach (var line in request.RequestLines)
        {
            line.ReplenishQty = 0;
            if (line.MaterialReceiptLine?.PurchaseOrderLineId is not { } polId || !purchaseLines.TryGetValue(polId, out var pol))
                continue;   // phiếu khách cấp: không có đơn mua để chốt, chỉ ghi nhận không bù
            // Đã nhận + không bù không vượt số đặt (CK_POL_Qty chặn NotReplenishedQty > OrderedQty).
            var room = pol.OrderedQty - received.GetValueOrDefault(polId) - pol.NotReplenishedQty;
            pol.NotReplenishedQty += Math.Max(0, Math.Min(line.Quantity, room));
        }

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

    private async Task<Dictionary<int, decimal>> ReplenishedAsync(int requestId, CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query()
            .Where(l => l.OriginalReceiptLineId != null && l.MaterialReceipt.ShortageRequestId == requestId
                        && l.MaterialReceipt.ReceiptType == ReceiptTypes.Supplementary
                        && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OriginalReceiptLineId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
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
