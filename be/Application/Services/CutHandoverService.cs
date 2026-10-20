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
    private readonly ILogger<CutHandoverService> _logger;

    public CutHandoverService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes, IAppLock locks,
        IClock clock, ILogger<CutHandoverService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _locks = locks; _clock = clock; _logger = logger;
    }

    private IQueryable<CutHandover> Handovers => _uow.Repository<CutHandover>().Query();

    public async Task<Result<PagedResult<CutHandoverListItemDto>>> GetPagedAsync(CutHandoverQuery query,
        int? cuttingTeamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Handovers.AsNoTracking()
            .Where(h => cuttingTeamId == null || h.CuttingTeamId == cuttingTeamId)
            .Where(h => string.IsNullOrEmpty(query.Status) || h.Status == query.Status)
            .Where(h => query.WorkOrderId == null || h.WorkOrderId == query.WorkOrderId)
            .Where(h => string.IsNullOrEmpty(keyword) || h.HandoverCode.Contains(keyword)
                        || h.WorkOrder.WorkOrderCode.Contains(keyword) || h.WorkOrder.Order.OrderCode.Contains(keyword))
            .OrderByDescending(h => h.CutHandoverId)
            .Select(h => new CutHandoverListItemDto(h.CutHandoverId, h.HandoverCode, h.WorkOrderId,
                h.WorkOrder.WorkOrderCode, h.WorkOrder.Order.OrderCode, h.CuttingTeamId, h.CuttingTeam.TeamName,
                h.HandedAt, h.Status, h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName,
                h.ConfirmedAt, h.CutHandoverLines.Sum(l => l.HandedQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<CutHandoverListItemDto>>.Success(page);
    }

    public async Task<Result<CutHandoverDto>> GetByIdAsync(int handoverId, int? cuttingTeamId, CancellationToken ct)
    {
        var dto = await Handovers.AsNoTracking().Where(h => h.CutHandoverId == handoverId)
            .Select(h => new CutHandoverDto(h.CutHandoverId, h.HandoverCode, h.WorkOrderId, h.WorkOrder.WorkOrderCode,
                h.WorkOrder.Order.OrderCode, h.CuttingTeamId, h.CuttingTeam.TeamName, h.HandedAt, h.Status,
                h.ConfirmedByNavigation == null ? null : h.ConfirmedByNavigation.FullName, h.ConfirmedAt, h.RejectReason,
                h.CutHandoverLines.OrderBy(l => l.CutHandoverLineId).Select(l => new CutHandoverLineDto(
                    l.CutHandoverLineId, l.OrderLineId, l.OrderLine.Product.ProductCode, l.OrderLine.Color,
                    l.OrderLine.SizeCode, l.HandedQty, l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
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
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
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
                .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).Include(w => w.CuttingLays)
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
                CuttingTeamId = cuttingTeamId,
                HandedAt = _clock.Now,
                Status = HandoverStatuses.Pending,
                CutHandoverLines = dto.Lines.Select(l => new CutHandoverLine { OrderLineId = l.OrderLineId, HandedQty = l.HandedQty }).ToList(),
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
                .Select(l => new StockMovement(StockKey.SemiFinished(l.OrderLineId), l.ConfirmedQty!.Value,
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
            _logger.LogInformation("Cut handover {HandoverCode} confirmed by employee {EmployeeId}", handover.HandoverCode, employeeId);
            return Result.Success(WarehouseMessages.HandoverConfirmed);
        });

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

    /// <summary>Đã cắt (Σ số thực tế các bàn) − đã bàn giao (phiếu chờ nhận + đã nhận, tính theo số tổ giao).</summary>
    private async Task<IReadOnlyList<HandoverableLineDto>> HandoverableAsync(WorkOrder wo, CancellationToken ct)
    {
        var cut = await _uow.Repository<CuttingLayLine>().Query().Where(l => l.CuttingLay.WorkOrderId == wo.WorkOrderId)
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var handed = await _uow.Repository<CutHandoverLine>().Query()
            .Where(l => l.CutHandover.WorkOrderId == wo.WorkOrderId
                        && (l.CutHandover.Status == HandoverStatuses.Pending || l.CutHandover.Status == HandoverStatuses.Confirmed))
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.HandedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        return wo.WorkOrderLines.Select(l => l.OrderLine).DistinctBy(l => l.OrderLineId)
            .OrderBy(l => l.Color).ThenBy(l => l.SizeCode)
            .Select(l =>
            {
                var c = cut.GetValueOrDefault(l.OrderLineId);
                var h = handed.GetValueOrDefault(l.OrderLineId);
                return new HandoverableLineDto(l.OrderLineId, l.Color, l.SizeCode, c, h, Math.Max(0, c - h));
            }).ToList();
    }

    private static Result<int>? CheckRemaining(CreateCutHandoverDto dto, IReadOnlyList<HandoverableLineDto> remaining)
    {
        var byLine = remaining.ToDictionary(r => r.OrderLineId);
        if (dto.Lines.Any(l => !byLine.ContainsKey(l.OrderLineId)))
            return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayOrderLineNotInWorkOrder);
        var over = dto.Lines.Where(l => l.HandedQty > byLine[l.OrderLineId].RemainingQty)
            .Select(l => $"{byLine[l.OrderLineId].Color}-{byLine[l.OrderLineId].SizeCode}: còn {byLine[l.OrderLineId].RemainingQty}, giao {l.HandedQty}")
            .ToList();
        return over.Count == 0
            ? null
            : Result<int>.Failure(ErrorCodes.CuttingHandoverExceedsCut, $"{WarehouseMessages.HandoverExceedsCut}: {string.Join("; ", over)}.");
    }
}
