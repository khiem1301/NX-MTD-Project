using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC43: tổ cắt bàn giao BTP của các bàn đã cắt; Thủ kho đếm, xác nhận (tăng tồn BTP) hoặc từ chối.</summary>
public sealed class CutHandoverService : ICutHandoverService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly IRequestService _requests;
    private readonly ILogger<CutHandoverService> _logger;

    public CutHandoverService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes, IAppLock locks,
        IClock clock, IRequestService requests, ILogger<CutHandoverService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _locks = locks; _clock = clock; _requests = requests; _logger = logger;
    }

    private IQueryable<CutHandover> Handovers => _uow.Repository<CutHandover>().Query();

    public async Task<Result<PagedResult<CutHandoverListItemDto>>> GetPagedAsync(CutHandoverQuery query,
        int? cuttingTeamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Handovers.AsNoTracking()
            .Where(h => cuttingTeamId == null || h.WorkOrder.CuttingTeamId == cuttingTeamId)
            .Where(h => string.IsNullOrEmpty(query.Status) || h.Status == query.Status)
            .Where(h => query.WorkOrderId == null || h.WorkOrderId == query.WorkOrderId)
            .Where(h => string.IsNullOrEmpty(keyword) || h.HandoverCode.Contains(keyword)
                        || h.WorkOrder.WorkOrderCode.Contains(keyword) || h.WorkOrder.Order.OrderCode.Contains(keyword))
            .OrderByDescending(h => h.CutHandoverId)
            .Select(h => new CutHandoverListItemDto(h.CutHandoverId, h.HandoverCode, h.WorkOrderId,
                h.WorkOrder.WorkOrderCode, h.WorkOrder.Order.OrderCode, h.WorkOrder.CuttingTeamId, h.WorkOrder.CuttingTeam.TeamName,
                h.HandedAt, h.Status, h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName,
                h.ConfirmedAt, h.CutHandoverLines.Sum(l => l.HandedQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<CutHandoverListItemDto>>.Success(page);
    }

    public async Task<Result<CutHandoverDto>> GetByIdAsync(int handoverId, int? cuttingTeamId, CancellationToken ct)
    {
        var dto = await Handovers.AsNoTracking().Where(h => h.CutHandoverId == handoverId)
            .Select(h => new CutHandoverDto(h.CutHandoverId, h.HandoverCode, h.WorkOrderId, h.WorkOrder.WorkOrderCode,
                h.WorkOrder.Order.OrderCode, h.WorkOrder.CuttingTeamId, h.WorkOrder.CuttingTeam.TeamName, h.HandedAt, h.Status,
                h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName, h.ConfirmedAt, h.RejectReason,
                h.CutHandoverLines.OrderBy(l => l.CutHandoverLineId).Select(l => new CutHandoverLineDto(
                    l.CutHandoverLineId, l.OrderLineId, l.ProductComponentMaterialId,
                    l.ProductComponentMaterial == null ? null : l.ProductComponentMaterial.ProductComponent.ComponentName + " – " + l.ProductComponentMaterial.Material.MaterialName + (l.ProductComponentMaterial.MaterialColor == "" ? "" : " " + l.ProductComponentMaterial.MaterialColor),
                    l.OrderLine.ProductVariant.Product.ProductCode, l.OrderLine.ProductVariant.Color,
                    l.OrderLine.ProductVariant.SizeCode, l.HandedQty, l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<CutHandoverDto>();
        return cuttingTeamId is not null && dto.CuttingTeamId != cuttingTeamId
            ? WarehouseFailures.TeamMismatch<CutHandoverDto>()
            : Result<CutHandoverDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<HandoverableLineDto>>> GetHandoverableAsync(int workOrderId,
        int cuttingTeamId, CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.ProductComponent)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.Material)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<IReadOnlyList<HandoverableLineDto>>();
        if (wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<IReadOnlyList<HandoverableLineDto>>();
        return Result<IReadOnlyList<HandoverableLineDto>>.Success(await HandoverableAsync(wo, ct));
    }

    public async Task<Result<CutHandoverDto>> CreateAsync(CreateCutHandoverDto dto, int cuttingTeamId,
        CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var wo = await _uow.Repository<WorkOrder>().Query()
                .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant)
                .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.ProductComponent)
                .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.Material).Include(w => w.CuttingLays)
                .FirstOrDefaultAsync(w => w.WorkOrderId == dto.WorkOrderId, ct);
            if (wo is null) return WarehouseFailures.NotFound<int>();
            if (wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<int>();
            if (wo.Status is not (WorkOrderStatuses.Open or WorkOrderStatuses.Cutting))
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotCuttable);

            await _locks.AcquireAsync($"cut-handover:{wo.WorkOrderId}", ct);
            var check = CheckRemaining(dto, await HandoverableAsync(wo, ct));
            if (check is not null) return check;

            var handover = new CutHandover
            {
                HandoverCode = await _codes.NextAsync<CutHandover>(DocumentPrefixes.CutHandover, h => h.HandoverCode, ct),
                WorkOrderId = wo.WorkOrderId,
                HandedAt = _clock.Now,
                Status = HandoverStatuses.Pending,
                CutHandoverLines = dto.Lines.Select(l => new CutHandoverLine { OrderLineId = l.OrderLineId, ProductComponentMaterialId = l.ProductComponentMaterialId, HandedQty = l.HandedQty }).ToList(),
            };
            // Số đã đưa vào kho thì không sửa nữa: khóa các bàn cắt đang RECORDED của lệnh.
            foreach (var lay in wo.CuttingLays.Where(l => l.Status == CuttingLayStatuses.Recorded))
                lay.Status = CuttingLayStatuses.Locked;

            await _uow.Repository<CutHandover>().AddAsync(handover);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(handover.CutHandoverId);
        });
        return created.IsSuccess
            ? await GetByIdAsync(created.Data, cuttingTeamId, ct)
            : WarehouseFailures.From<CutHandoverDto>(created);
    }

    public Task<Result> ConfirmAsync(int handoverId, ConfirmCutHandoverDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var handover = await Handovers.Include(h => h.CutHandoverLines).FirstOrDefaultAsync(h => h.CutHandoverId == handoverId, ct);
            if (handover is null) return WarehouseFailures.NotFound();
            if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var byId = dto.Lines.ToDictionary(l => l.CutHandoverLineId);
            if (byId.Count != handover.CutHandoverLines.Count || handover.CutHandoverLines.Any(l => !byId.ContainsKey(l.CutHandoverLineId)))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
            foreach (var line in handover.CutHandoverLines)
            {
                var input = byId[line.CutHandoverLineId];
                if (input.ConfirmedQty > line.HandedQty)
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.HandoverConfirmedExceedsHanded);
                if (input.ConfirmedQty != line.HandedQty && string.IsNullOrWhiteSpace(input.DiscrepancyReason))
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);
                line.ConfirmedQty = input.ConfirmedQty;
                line.DiscrepancyReason = input.ConfirmedQty == line.HandedQty ? null : input.DiscrepancyReason!.Trim();
            }

            var today = _clock.Today;
            var movements = handover.CutHandoverLines.Where(l => l.ConfirmedQty > 0)
                .Select(l => new StockMovement(StockKey.SemiFinished(l.OrderLineId, l.ProductComponentMaterialId), l.ConfirmedQty!.Value,
                    StockDocTypes.CutHandover, handover.CutHandoverId, l.CutHandoverLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            handover.Status = HandoverStatuses.Confirmed;
            handover.ConfirmedBy = employeeId;
            handover.ConfirmedAt = _clock.Now;
            await _uow.SaveChangesAsync();
            await CompleteReplacementRequestAsync(handover.WorkOrderId, ct);
            await _uow.SaveChangesAsync();
            _logger.LogInformation("Cut handover {HandoverCode} confirmed by employee {EmployeeId}", handover.HandoverCode, employeeId);
            return Result.Success(WarehouseMessages.HandoverConfirmed);
        });

    /// <summary>
    /// Lệnh cắt bù (SourceRequestId): khi BTP đã vào kho đủ số kế hoạch thì đơn cắt bù tự hoàn thành (UC19, TH-10).
    /// Chạy sau khi phiếu đã lưu CONFIRMED để tổng đã nhận đọc từ DB đã gồm phiếu này.
    /// </summary>
    private async Task CompleteReplacementRequestAsync(int workOrderId, CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Where(w => w.WorkOrderId == workOrderId).Select(w => new { w.SourceRequestId }).FirstAsync(ct);
        if (wo.SourceRequestId is not { } requestId) return;

        var planned = await _uow.Repository<WorkOrderLine>().Query().Where(l => l.WorkOrderId == workOrderId).SumAsync(l => l.PlannedQty, ct);
        var received = await _uow.Repository<CutHandoverLine>().Query()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId && l.CutHandover.Status == HandoverStatuses.Confirmed)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        if (received >= planned) await _requests.MarkCompletedAsync(requestId, "BTP cắt bù đã vào kho.", ct);
    }

    public async Task<Result> RejectAsync(int handoverId, ReasonDto dto, int employeeId, CancellationToken ct)
    {
        var handover = await Handovers.FirstOrDefaultAsync(h => h.CutHandoverId == handoverId, ct);
        if (handover is null) return WarehouseFailures.NotFound();
        if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        handover.Status = HandoverStatuses.Rejected;
        handover.RejectReason = dto.Reason.Trim();
        handover.ConfirmedBy = employeeId;
        handover.ConfirmedAt = _clock.Now;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.HandoverRejected);
    }

    public async Task<Result> CancelAsync(int handoverId, int cuttingTeamId, CancellationToken ct)
    {
        var handover = await Handovers.Include(h => h.WorkOrder)
            .FirstOrDefaultAsync(h => h.CutHandoverId == handoverId && h.WorkOrder.CuttingTeamId == cuttingTeamId, ct);
        if (handover is null) return WarehouseFailures.NotFound();
        if (handover.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        handover.Status = HandoverStatuses.Cancelled;
        // Phiếu này đã khóa các bàn cắt của lệnh khi lập; còn phiếu chờ / đã nhận khác thì giữ khóa, hết thì mở lại để sửa bàn cắt.
        var workOrderId = handover.WorkOrderId;
        var stillHanded = await Handovers.AnyAsync(h => h.WorkOrderId == workOrderId && h.CutHandoverId != handoverId
            && (h.Status == HandoverStatuses.Pending || h.Status == HandoverStatuses.Confirmed), ct);
        if (!stillHanded)
        {
            var locked = await _uow.Repository<CuttingLay>().Query()
                .Where(l => l.WorkOrderId == workOrderId && l.Status == CuttingLayStatuses.Locked).ToListAsync(ct);
            foreach (var lay in locked) lay.Status = CuttingLayStatuses.Recorded;
        }
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.CutHandoverCancelled);
    }

    /// <summary>Đã cắt (Σ số thực tế các bàn) − đã bàn giao (phiếu chờ nhận + đã nhận, tính theo số tổ giao).</summary>
    private async Task<IReadOnlyList<HandoverableLineDto>> HandoverableAsync(WorkOrder wo, CancellationToken ct)
    {
        var cut = await _uow.Repository<CuttingLayLine>().Query().Where(l => l.CuttingLay.WorkOrderId == wo.WorkOrderId)
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId }).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);
        var handed = await _uow.Repository<CutHandoverLine>().Query()
            .Where(l => l.CutHandover.WorkOrderId == wo.WorkOrderId
                        && (l.CutHandover.Status == HandoverStatuses.Pending || l.CutHandover.Status == HandoverStatuses.Confirmed))
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId }).Select(g => new { g.Key, Qty = g.Sum(l => l.HandedQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);

        // Mỗi (dòng đơn, chi tiết) của lệnh một dòng: lệnh thường chỉ có bộ đủ (chi tiết null), lệnh cắt bù có từng chi tiết.
        return wo.WorkOrderLines.DistinctBy(l => (l.OrderLineId, l.ProductComponentMaterialId))
            .OrderBy(l => l.OrderLine.ProductVariant.Color).ThenBy(l => l.OrderLine.ProductVariant.SizeCode)
            .ThenBy(l => l.ProductComponentMaterial == null ? 0 : l.ProductComponentMaterial.ProductComponent.SortOrder)
            .Select(l =>
            {
                var c = cut.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId));
                var h = handed.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId));
                return new HandoverableLineDto(l.OrderLineId, l.ProductComponentMaterialId, ComponentLabels.Of(l.ProductComponentMaterial),
                    l.OrderLine.ProductVariant.Color, l.OrderLine.ProductVariant.SizeCode, c, h, Math.Max(0, c - h));
            }).ToList();
    }

    private static Result<int>? CheckRemaining(CreateCutHandoverDto dto, IReadOnlyList<HandoverableLineDto> remaining)
    {
        var byLine = remaining.ToDictionary(r => (r.OrderLineId, r.ProductComponentMaterialId));
        if (dto.Lines.Any(l => !byLine.ContainsKey((l.OrderLineId, l.ProductComponentMaterialId))))
            return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayOrderLineNotInWorkOrder);
        var over = dto.Lines.Where(l => l.HandedQty > byLine[(l.OrderLineId, l.ProductComponentMaterialId)].RemainingQty)
            .Select(l =>
            {
                var r = byLine[(l.OrderLineId, l.ProductComponentMaterialId)];
                return $"{r.Color}-{r.SizeCode}{(r.ComponentName is null ? string.Empty : " (" + r.ComponentName + ")")}: còn {r.RemainingQty}, giao {l.HandedQty}";
            })
            .ToList();
        return over.Count == 0
            ? null
            : Result<int>.Failure(ErrorCodes.CuttingHandoverExceedsCut, $"{WarehouseMessages.HandoverExceedsCut}: {string.Join("; ", over)}.");
    }
}
