using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC44: QA/QC gom số hàng đạt QC để bàn giao thành phẩm vào kho FINISHED (được giao một phần, tổng giao ≤ số QC đạt);
/// Thủ kho đếm, xác nhận (tăng tồn thành phẩm, lệch phải có lý do) hoặc từ chối có lý do (R11).
/// </summary>
public sealed class FgHandoverService : IFgHandoverService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IOutputQueryService _output;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly ILogger<FgHandoverService> _logger;

    public FgHandoverService(IUnitOfWork uow, IStockService stock, IOutputQueryService output,
        IDocumentCodeGenerator codes, INotificationService notifier, IAppLock locks, IClock clock,
        ILogger<FgHandoverService> logger)
    {
        _uow = uow; _stock = stock; _output = output; _codes = codes; _notifier = notifier; _locks = locks;
        _clock = clock; _logger = logger;
    }

    private IQueryable<FgHandover> Handovers => _uow.Repository<FgHandover>().Query();

    public async Task<Result<PagedResult<FgHandoverListItemDto>>> GetPagedAsync(FgHandoverQuery query,
        int? inspectorId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Handovers.AsNoTracking()
            .Where(h => inspectorId == null || h.InspectorId == inspectorId)
            .Where(h => string.IsNullOrEmpty(query.Status) || h.Status == query.Status)
            .Where(h => query.OrderId == null || h.OrderId == query.OrderId)
            .Where(h => string.IsNullOrEmpty(keyword) || h.HandoverCode.Contains(keyword)
                        || h.Order.OrderCode.Contains(keyword) || h.Product.ProductCode.Contains(keyword))
            .OrderByDescending(h => h.FgHandoverId)
            .Select(h => new FgHandoverListItemDto(h.FgHandoverId, h.HandoverCode, h.OrderId, h.Order.OrderCode,
                h.ProductId, h.Product.ProductCode, h.InspectorId, h.Inspector.FullName, h.HandedAt, h.Status,
                h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName, h.ConfirmedAt,
                h.FgHandoverLines.Sum(l => l.HandedQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<FgHandoverListItemDto>>.Success(page);
    }

    public async Task<Result<FgHandoverDto>> GetByIdAsync(int handoverId, int? inspectorId, CancellationToken ct)
    {
        var dto = await Handovers.AsNoTracking().Where(h => h.FgHandoverId == handoverId)
            .Select(h => new FgHandoverDto(h.FgHandoverId, h.HandoverCode, h.OrderId, h.Order.OrderCode, h.ProductId,
                h.Product.ProductCode, h.InspectorId, h.Inspector.FullName, h.HandedAt, h.Status,
                h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName, h.ConfirmedAt, h.RejectReason,
                h.FgHandoverLines.OrderBy(l => l.FgHandoverLineId).Select(l => new FgHandoverLineDto(
                    l.FgHandoverLineId, l.OrderLineId, l.OrderLine.ProductVariant.Color, l.OrderLine.ProductVariant.SizeCode, l.HandedQty,
                    l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<FgHandoverDto>();
        // QA/QC chỉ xem phiếu của mình: phiếu người khác trả "không tìm thấy" như các luồng khác (R1).
        return inspectorId is not null && dto.InspectorId != inspectorId
            ? WarehouseFailures.NotFound<FgHandoverDto>()
            : Result<FgHandoverDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<FgHandoverableLineDto>>> GetHandoverableAsync(int orderId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return WarehouseFailures.NotFound<IReadOnlyList<FgHandoverableLineDto>>();
        return Result<IReadOnlyList<FgHandoverableLineDto>>.Success(await HandoverableAsync(orderId, null, ct));
    }

    public async Task<Result<FgHandoverDto>> CreateAsync(CreateFgHandoverDto dto, int inspectorId, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var order = await _uow.Repository<CustomerOrder>().Query().FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
            if (order is null) return WarehouseFailures.NotFound<int>();
            if (OrderStatuses.Closed.Contains(order.Status))
                return Result<int>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

            await _locks.AcquireAsync($"fg-handover:{order.OrderId}", ct);
            var remaining = (await HandoverableAsync(order.OrderId, dto.ProductId, ct)).ToDictionary(r => r.OrderLineId);
            if (dto.Lines.Any(l => !remaining.ContainsKey(l.OrderLineId)))
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.FgLineNotInOrder);
            var over = dto.Lines.Where(l => l.HandedQty > remaining[l.OrderLineId].RemainingQty)
                .Select(l => $"{remaining[l.OrderLineId].Color}-{remaining[l.OrderLineId].SizeCode}: còn {remaining[l.OrderLineId].RemainingQty}, giao {l.HandedQty}")
                .ToList();
            if (over.Count > 0)
                return Result<int>.Failure(ErrorCodes.FgHandoverExceedsQc,
                    $"{WarehouseMessages.FgHandoverExceedsQc}: {string.Join("; ", over)}.");

            var handover = new FgHandover
            {
                HandoverCode = await _codes.NextAsync<FgHandover>(DocumentPrefixes.FgHandover, h => h.HandoverCode, ct),
                OrderId = order.OrderId,
                ProductId = dto.ProductId,
                InspectorId = inspectorId,
                HandedAt = _clock.Now,
                Status = HandoverStatuses.Pending,
                FgHandoverLines = dto.Lines.Select(l => new FgHandoverLine { OrderLineId = l.OrderLineId, HandedQty = l.HandedQty }).ToList(),
            };
            await _uow.Repository<FgHandover>().AddAsync(handover);
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                "Chờ nhận thành phẩm", $"QA/QC đã lập phiếu {handover.HandoverCode} cho đơn {order.OrderCode}, vui lòng đếm và nhận vào kho.",
                null), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(handover.FgHandoverId, WarehouseMessages.FgHandoverCreated);
        });
        return created.IsSuccess
            ? await GetByIdAsync(created.Data, inspectorId, ct)
            : WarehouseFailures.From<FgHandoverDto>(created);
    }

    public Task<Result> ConfirmAsync(int handoverId, ConfirmFgHandoverDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var handover = await Handovers.Include(h => h.FgHandoverLines).FirstOrDefaultAsync(h => h.FgHandoverId == handoverId, ct);
            if (handover is null) return WarehouseFailures.NotFound();
            if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var byId = dto.Lines.ToDictionary(l => l.FgHandoverLineId);
            if (byId.Count != handover.FgHandoverLines.Count || handover.FgHandoverLines.Any(l => !byId.ContainsKey(l.FgHandoverLineId)))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
            foreach (var line in handover.FgHandoverLines)
            {
                var input = byId[line.FgHandoverLineId];
                if (input.ConfirmedQty > line.HandedQty)
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.HandoverConfirmedExceedsHanded);
                if (input.ConfirmedQty != line.HandedQty && string.IsNullOrWhiteSpace(input.DiscrepancyReason))
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);
                line.ConfirmedQty = input.ConfirmedQty;
                line.DiscrepancyReason = input.ConfirmedQty == line.HandedQty ? null : input.DiscrepancyReason!.Trim();
            }

            var today = _clock.Today;
            var movements = handover.FgHandoverLines.Where(l => l.ConfirmedQty > 0)
                .Select(l => new StockMovement(StockKey.Finished(l.OrderLineId), l.ConfirmedQty!.Value,
                    StockDocTypes.FgHandover, handover.FgHandoverId, l.FgHandoverLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            handover.Status = HandoverStatuses.Confirmed;
            handover.ConfirmedBy = employeeId;
            handover.ConfirmedAt = _clock.Now;
            _notifier.NotifyEmployees(new[] { handover.InspectorId }, new NotificationMessage(NotificationSources.Warehouse,
                "Đã nhận thành phẩm", $"Thủ kho đã nhận phiếu bàn giao {handover.HandoverCode} vào kho.", null,
                nameof(FgHandover), handover.FgHandoverId));
            await _uow.SaveChangesAsync();
            await CompareWithKpiAsync(handover, ct);
            await _uow.SaveChangesAsync();
            _logger.LogInformation("FG handover {HandoverCode} confirmed by employee {EmployeeId}", handover.HandoverCode, employeeId);
            return Result.Success(WarehouseMessages.FgHandoverConfirmed);
        });

    /// <summary>
    /// Swimlane "Compare & Notify KPI result": mỗi lần kho nhận thành phẩm, so số thành phẩm kho đã nhận của đơn + mã hàng với
    /// chỉ tiêu KPI đang hoạt động và báo Quản đốc. Chỉ báo; chốt và đóng KPI vẫn qua "Kết thúc đơn" của Quản đốc.
    /// </summary>
    private async Task CompareWithKpiAsync(FgHandover handover, CancellationToken ct)
    {
        var pairs = (await _uow.Repository<FgHandoverLine>().Query().AsNoTracking()
                .Where(l => l.FgHandoverId == handover.FgHandoverId)
                .Select(l => new { l.OrderLine.OrderId, l.OrderLine.ProductId }).ToListAsync(ct))
            .Distinct().ToList();
        foreach (var pair in pairs)
        {
            var targets = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
                .Where(k => k.OrderId == pair.OrderId && k.ProductId == pair.ProductId && k.Status == KpiStatuses.Active)
                .Select(k => new { k.TargetFinishedQty, OrderCode = k.Order.OrderCode, ProductCode = k.Product.ProductCode, Team = k.SewingTeam.TeamName })
                .ToListAsync(ct);
            if (targets.Count == 0) continue;

            var received = await _uow.Repository<FgHandoverLine>().Query().AsNoTracking()
                .Where(l => l.FgHandover.Status == HandoverStatuses.Confirmed && l.OrderLine.OrderId == pair.OrderId
                            && l.OrderLine.ProductId == pair.ProductId)
                .SumAsync(l => l.ConfirmedQty ?? 0, ct);
            foreach (var t in targets)
            {
                var percent = t.TargetFinishedQty > 0 ? Math.Round(100m * received / t.TargetFinishedQty, 1) : 0m;
                var verdict = received >= t.TargetFinishedQty
                    ? "đã đạt chỉ tiêu, có thể bấm Kết thúc đơn để chốt KPI"
                    : $"chưa đạt, còn thiếu {t.TargetFinishedQty - received}";
                await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Production,
                    "Kết quả KPI sau khi kho nhận thành phẩm",
                    $"Đơn {t.OrderCode} mã hàng {t.ProductCode}, {t.Team}: kho đã nhận {received}/{t.TargetFinishedQty} ({percent:0.#}%), {verdict}.",
                    "/kpi/targets"), ct);
            }
        }
    }

    public async Task<Result> RejectAsync(int handoverId, ReasonDto dto, int employeeId, CancellationToken ct)
    {
        var handover = await Handovers.FirstOrDefaultAsync(h => h.FgHandoverId == handoverId, ct);
        if (handover is null) return WarehouseFailures.NotFound();
        if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        handover.Status = HandoverStatuses.Rejected;
        handover.RejectReason = dto.Reason.Trim();
        handover.ConfirmedBy = employeeId;
        handover.ConfirmedAt = _clock.Now;
        _notifier.NotifyEmployees(new[] { handover.InspectorId }, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu bàn giao bị từ chối", $"Thủ kho từ chối phiếu {handover.HandoverCode}: {handover.RejectReason}", null,
            nameof(FgHandover), handover.FgHandoverId));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.FgHandoverRejected);
    }

    public async Task<Result> CancelAsync(int handoverId, int inspectorId, CancellationToken ct)
    {
        var handover = await Handovers.FirstOrDefaultAsync(h => h.FgHandoverId == handoverId && h.InspectorId == inspectorId, ct);
        if (handover is null) return WarehouseFailures.NotFound();
        if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        handover.Status = HandoverStatuses.Cancelled;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.FgHandoverCancelled);
    }

    /// <summary>Số QC đạt − đã bàn giao (phiếu chờ nhận + đã nhận, tính theo số QA/QC giao) của từng dòng đơn.</summary>
    private async Task<IReadOnlyList<FgHandoverableLineDto>> HandoverableAsync(int orderId, int? productId, CancellationToken ct)
    {
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId && (productId == null || l.ProductId == productId))
            .OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new { l.OrderLineId, l.ProductId, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode })
            .ToListAsync(ct);
        var ids = lines.Select(l => l.OrderLineId).ToList();
        var qc = await _output.GetQcFinishedByOrderLineAsync(ids, ct);
        var handed = await _uow.Repository<FgHandoverLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId)
                        && (l.FgHandover.Status == HandoverStatuses.Pending || l.FgHandover.Status == HandoverStatuses.Confirmed))
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.HandedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        return lines.Select(l =>
        {
            var q = qc.GetValueOrDefault(l.OrderLineId);
            var h = handed.GetValueOrDefault(l.OrderLineId);
            return new FgHandoverableLineDto(l.OrderLineId, l.ProductId, l.ProductCode, l.Color, l.SizeCode, q, h, Math.Max(0, q - h));
        }).ToList();
    }
}
