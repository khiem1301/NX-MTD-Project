using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC53 / UC54 / UC60: tổ trưởng may chia việc cho công nhân (database 26/10): mỗi lần giao là số lần làm một công đoạn
/// × màu × size (không còn đếm BTP giao kèm). Tổng số lần giao của một công đoạn không vượt số bộ BTP tổ đã nhận (50050);
/// số lần đã giao là trần sản lượng của công nhân ở đúng công đoạn đó (<c>vw_WorkerOutputCap</c>, 50052).
/// Service kiểm trước để báo lỗi rõ; trigger <c>trg_WorkAllocation_Check</c> là chốt chặn cuối.
/// </summary>
public sealed class WorkAllocationService : IWorkAllocationService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IAppLock _locks;
    private readonly IClock _clock;

    public WorkAllocationService(IUnitOfWork uow, INotificationService notifier,
        IAuditLogService audit, IAppLock locks, IClock clock)
    {
        _uow = uow; _notifier = notifier; _audit = audit; _locks = locks; _clock = clock;
    }

    private IQueryable<WorkAllocation> Allocations => _uow.Repository<WorkAllocation>().Query();

    public async Task<Result<PagedResult<WorkAllocationDto>>> GetPagedAsync(WorkAllocationQuery query, int? teamId,
        int? workerId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Project(Allocations.AsNoTracking()
                .Where(m => teamId == null || m.SewingTeamId == teamId)
                .Where(m => workerId == null || m.WorkerId == workerId)
                .Where(m => query.WorkerId == null || m.WorkerId == query.WorkerId)
                .Where(m => string.IsNullOrEmpty(query.Status) || m.Status == query.Status)
                .Where(m => string.IsNullOrEmpty(query.MovementType) || m.MovementType == query.MovementType)
                .Where(m => query.OrderId == null || m.OrderLine.OrderId == query.OrderId)
                .Where(m => string.IsNullOrEmpty(keyword) || m.Worker.FullName.Contains(keyword)
                            || m.Worker.EmployeeCode.Contains(keyword) || m.OrderLine.Order.OrderCode.Contains(keyword)
                            || m.Operation.OperationName.Contains(keyword))
                .OrderByDescending(m => m.WorkAllocationId))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<WorkAllocationDto>>.Success(page);
    }

    public async Task<Result<WorkAllocationBoardDto>> GetBoardAsync(int teamId, int? orderId, CancellationToken ct)
    {
        var operations = await (
            from v in _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
            join l in _uow.Repository<OrderLine>().Query() on v.OrderLineId equals l.OrderLineId
            join o in _uow.Repository<Operation>().Query() on v.OperationId equals o.OperationId
            where v.SewingTeamId == teamId && (orderId == null || l.OrderId == orderId)
            orderby l.Order.OrderCode, l.ProductVariant.Color, l.ProductVariant.SizeCode, o.StationSequence
            select new OperationProgressDto(l.OrderLineId, l.Order.OrderCode, l.ProductVariant.Product.ProductCode,
                l.ProductVariant.Color, l.ProductVariant.SizeCode, o.OperationId, o.OperationCode, o.OperationName,
                o.OperationGroup == null ? null : o.OperationGroup.GroupName, v.UsesSemiFinished, v.RequiredQty ?? 0,
                v.AllocatedQty, v.PendingQty, v.UnallocatedQty ?? 0, v.OutputQty)).ToListAsync(ct);

        var rows = await Allocations.AsNoTracking()
            .Where(m => m.SewingTeamId == teamId && (orderId == null || m.OrderLine.OrderId == orderId))
            .GroupBy(m => new { m.WorkerId, m.Worker.EmployeeCode, m.Worker.FullName, m.OrderLineId, m.OperationId })
            .Select(g => new
            {
                g.Key.WorkerId, g.Key.EmployeeCode, g.Key.FullName, g.Key.OrderLineId, g.Key.OperationId,
                Held = g.Where(m => m.Status == HandoverStatuses.Confirmed)
                    .Sum(m => m.MovementType == PieceMovementTypes.Handover ? m.ConfirmedQty ?? 0 : -(m.ConfirmedQty ?? 0)),
                PendingRetrieval = g.Where(m => m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Retrieval)
                    .Sum(m => m.Quantity),
            }).ToListAsync(ct);
        var outputs = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(r => r.SewingTeamId == teamId && (orderId == null || r.OrderLine.OrderId == orderId))
            .GroupBy(r => new { r.WorkerId, r.OrderLineId, r.OperationId })
            .Select(g => new { g.Key.WorkerId, g.Key.OrderLineId, g.Key.OperationId, Qty = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => (x.WorkerId, x.OrderLineId, x.OperationId), x => x.Qty, ct);
        var workers = rows.Where(r => r.Held > 0 || r.PendingRetrieval > 0)
            .OrderBy(r => r.FullName).ThenBy(r => r.OrderLineId).ThenBy(r => r.OperationId)
            .Select(r =>
            {
                var output = outputs.GetValueOrDefault((r.WorkerId, r.OrderLineId, r.OperationId));
                return new WorkerAllocationLineDto(r.WorkerId, r.EmployeeCode, r.FullName, r.OrderLineId, r.OperationId, r.Held,
                    r.PendingRetrieval, output, Math.Max(0, r.Held - r.PendingRetrieval - output));
            }).ToList();
        return Result<WorkAllocationBoardDto>.Success(new WorkAllocationBoardDto(operations, workers));
    }

    public async Task<Result<WorkAllocationDto>> CreateAsync(CreateWorkAllocationDto dto, int teamId, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct);
            if (team is null || team.TeamType != TeamTypes.Sewing) return WarehouseFailures.TeamMismatch<int>();

            var worker = await _uow.Repository<Employee>().Query().AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == dto.WorkerId, ct);
            if (worker is null || worker.TeamId != teamId || worker.Status != RecordStatus.Active)
                return Result<int>.Failure(ErrorCodes.WarehouseTeamMismatch, WarehouseMessages.WorkerNotInTeam, ResultKind.Forbidden);

            var line = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.Order).Include(l => l.ProductVariant)
                .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId, ct);
            if (line is null) return WarehouseFailures.NotFound<int>();
            if (OrderStatuses.Closed.Contains(line.Order.Status))
                return Result<int>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

            // THROW 50044: công đoạn phải thuộc mã hàng của dòng đơn; chỉ chia việc công đoạn may đang dùng (như vw_OperationAllocationProgress).
            var operation = await _uow.Repository<Operation>().Query().AsNoTracking()
                .FirstOrDefaultAsync(o => o.OperationId == dto.OperationId && o.ProductId == line.ProductId, ct);
            if (operation is null) return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OperationNotOfProduct);
            if (dto.MovementType == PieceMovementTypes.Handover
                && (operation.Status != RecordStatus.Active || operation.TeamType != TeamTypes.Sewing))
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OperationNotAllocatable);

            await _locks.AcquireAsync($"work-allocation:{teamId}:{dto.OrderLineId}", ct);
            if (dto.MovementType == PieceMovementTypes.Handover)
            {
                var received = await TeamReceivedAsync(teamId, dto.OrderLineId, ct);
                if (received == 0)
                    return Result<int>.Failure(ErrorCodes.AllocationExceedsRequired, WarehouseMessages.AllocationNoPiecesReceived);
                var unallocated = await UnallocatedAsync(teamId, dto.OrderLineId, dto.OperationId, ct);
                if (dto.Quantity > unallocated)
                    return Result<int>.Failure(ErrorCodes.AllocationExceedsRequired,
                        $"{WarehouseMessages.AllocationExceedsRequired}: tổ đã nhận {received} bộ, còn giao được {unallocated}, yêu cầu {dto.Quantity}.");
            }
            else
            {
                var retrievable = await WorkerRetrievableAsync(dto.WorkerId, dto.OrderLineId, dto.OperationId, ct);
                if (dto.Quantity > retrievable)
                    return Result<int>.Failure(ErrorCodes.PieceRetrievalExceedsHolding,
                        $"{WarehouseMessages.PieceRetrievalExceedsHolding}: còn {retrievable}, yêu cầu {dto.Quantity}.");
            }

            var allocation = new WorkAllocation
            {
                MovementType = dto.MovementType,
                SewingTeamId = teamId,
                WorkerId = dto.WorkerId,
                OrderLineId = dto.OrderLineId,
                OperationId = dto.OperationId,
                Quantity = dto.Quantity,
                RetrievalReason = dto.MovementType == PieceMovementTypes.Retrieval ? dto.RetrievalReason : null,
                ConfirmMode = ConfirmModes.Worker,
                Status = HandoverStatuses.Pending,
                CreatedAt = _clock.Now,
            };
            await _uow.Repository<WorkAllocation>().AddAsync(allocation);
            var verb = dto.MovementType == PieceMovementTypes.Handover ? "giao" : "thu hồi";
            _notifier.NotifyEmployees(new[] { dto.WorkerId }, new NotificationMessage(NotificationSources.Warehouse,
                dto.MovementType == PieceMovementTypes.Handover ? "Bạn được giao việc" : "Việc được thu hồi từ bạn",
                $"Tổ trưởng ghi {verb} {dto.Quantity} lần \"{operation.OperationName}\" ({line.ProductVariant.Color} - {line.ProductVariant.SizeCode})"
                + ", vui lòng kiểm tra và xác nhận.",
                null));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(allocation.WorkAllocationId, WarehouseMessages.PieceMovementCreated);
        });
        return created.IsSuccess ? await GetOneAsync(created.Data, ct) : WarehouseFailures.From<WorkAllocationDto>(created);
    }

    public Task<Result> ConfirmByWorkerAsync(int allocationId, ConfirmWorkAllocationDto dto, int workerId, CancellationToken ct) =>
        DecideAsync(allocationId, ct, m => m.WorkerId == workerId, m => ApplyConfirmAsync(m, dto.ConfirmedQty,
            dto.DiscrepancyReason, ConfirmModes.Worker, null, ct), notifyLeaders: true);

    public Task<Result> ConfirmOnBehalfAsync(int allocationId, ConfirmOnBehalfDto dto, int teamId, CancellationToken ct) =>
        DecideAsync(allocationId, ct, m => m.SewingTeamId == teamId, m => ApplyConfirmAsync(m, dto.ConfirmedQty,
            dto.DiscrepancyReason, ConfirmModes.OnBehalf, dto.OnBehalfReason.Trim(), ct), notifyLeaders: false);

    public Task<Result> RejectByWorkerAsync(int allocationId, ReasonDto dto, int workerId, CancellationToken ct) =>
        DecideAsync(allocationId, ct, m => m.WorkerId == workerId, m =>
        {
            m.Status = HandoverStatuses.Rejected;
            m.RejectReason = dto.Reason.Trim();
            m.ConfirmedAt = _clock.Now;
            return Task.FromResult(Result.Success(WarehouseMessages.PieceMovementRejected));
        }, notifyLeaders: true);

    // ---------- Nội bộ ----------

    private Task<Result> DecideAsync(int allocationId, CancellationToken ct, Func<WorkAllocation, bool> allowed,
        Func<WorkAllocation, Task<Result>> apply, bool notifyLeaders) =>
        _uow.InTransactionAsync(async () =>
        {
            var allocation = await Allocations.Include(m => m.OrderLine).ThenInclude(l => l.ProductVariant)
                .Include(m => m.Operation).FirstOrDefaultAsync(m => m.WorkAllocationId == allocationId, ct);
            if (allocation is null || !allowed(allocation)) return WarehouseFailures.NotFound();
            if (allocation.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            await _locks.AcquireAsync($"work-allocation:{allocation.SewingTeamId}:{allocation.OrderLineId}", ct);
            var before = new { allocation.Status };
            var result = await apply(allocation);
            if (!result.IsSuccess) return result;

            _audit.Add(new AuditEntry(nameof(WorkAllocation), allocation.WorkAllocationId, AuditActions.Update, before,
                new { allocation.Status, allocation.ConfirmedQty, allocation.ConfirmMode },
                allocation.OnBehalfReason ?? allocation.RejectReason));
            if (notifyLeaders)
            {
                var leaders = await _uow.Repository<Employee>().Query().AsNoTracking()
                    .Where(e => e.TeamId == allocation.SewingTeamId && e.Status == RecordStatus.Active
                                && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
                    .Select(e => e.EmployeeId).ToListAsync(ct);
                _notifier.NotifyEmployees(leaders, new NotificationMessage(NotificationSources.Warehouse,
                    allocation.Status == HandoverStatuses.Confirmed ? "Công nhân đã xác nhận việc được giao" : "Công nhân từ chối phiếu giao việc",
                    $"{allocation.Operation.OperationName} ({allocation.OrderLine.ProductVariant.Color} - {allocation.OrderLine.ProductVariant.SizeCode}): {(allocation.Status == HandoverStatuses.Confirmed ? "đã xác nhận" : "bị từ chối")}.",
                    null));
            }
            await _uow.SaveChangesAsync();
            return result;
        });

    private async Task<Result> ApplyConfirmAsync(WorkAllocation m, int confirmedQty, string? discrepancy,
        string mode, string? onBehalfReason, CancellationToken ct)
    {
        if (confirmedQty < 1 || confirmedQty > m.Quantity)
            return Result.Failure(ErrorCodes.WarehouseInvalidReference,
                string.Format(WarehouseMessages.AllocationConfirmRange, m.Quantity));
        var same = confirmedQty == m.Quantity;
        if (!same && string.IsNullOrWhiteSpace(discrepancy))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);

        // Thu hồi: kiểm lại đến lúc này vì công nhân có thể đã báo thêm sản lượng (50050 / 50052).
        if (m.MovementType == PieceMovementTypes.Retrieval)
        {
            var retrievable = await WorkerRetrievableAsync(m.WorkerId, m.OrderLineId, m.OperationId, ct, excludeId: m.WorkAllocationId);
            if (confirmedQty > retrievable)
                return Result.Failure(ErrorCodes.PieceRetrievalExceedsHolding, WarehouseMessages.PieceRetrievalExceedsHolding);
        }

        m.ConfirmedQty = confirmedQty;
        m.DiscrepancyReason = same ? null : discrepancy!.Trim();
        m.ConfirmMode = mode;
        m.OnBehalfReason = onBehalfReason;
        m.Status = HandoverStatuses.Confirmed;
        m.ConfirmedAt = _clock.Now;
        return Result.Success(WarehouseMessages.PieceMovementConfirmed);
    }

    /// <summary>Số lần còn giao được của công đoạn = cần làm − đã giao − đang chờ xác nhận (vw_OperationAllocationProgress).</summary>
    private async Task<int> UnallocatedAsync(int teamId, int orderLineId, int operationId, CancellationToken ct)
    {
        var row = await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
            .Where(v => v.SewingTeamId == teamId && v.OrderLineId == orderLineId && v.OperationId == operationId)
            .Select(v => new { v.UnallocatedQty }).FirstOrDefaultAsync(ct);
        return Math.Max(0, row?.UnallocatedQty ?? 0);
    }

    /// <summary>Số bộ BTP tổ đã nhận của dòng đơn (trần giao việc của trigger 50050).</summary>
    private async Task<int> TeamReceivedAsync(int teamId, int orderLineId, CancellationToken ct) =>
        (int)await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking().ReceivedSets(teamId, orderLineId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);

    /// <summary>Số lần công nhân đang giữ ở công đoạn (đã nhận − đã thu hồi) − chờ thu hồi − đã báo sản lượng.</summary>
    private async Task<int> WorkerRetrievableAsync(int workerId, int orderLineId, int operationId, CancellationToken ct,
        int? excludeId = null)
    {
        var rows = await Allocations.Where(m => m.WorkerId == workerId && m.OrderLineId == orderLineId && m.OperationId == operationId)
            .ToListAsync(ct);
        var held = rows.Where(m => m.Status == HandoverStatuses.Confirmed)
            .Sum(m => m.MovementType == PieceMovementTypes.Handover ? m.ConfirmedQty ?? 0 : -(m.ConfirmedQty ?? 0));
        var pending = rows.Where(m => m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Retrieval
                                      && m.WorkAllocationId != excludeId).Sum(m => m.Quantity);
        var output = await _uow.Repository<OutputRecord>().Query()
            .Where(r => r.WorkerId == workerId && r.OrderLineId == orderLineId && r.OperationId == operationId)
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;
        return Math.Max(0, held - pending - output);
    }

    private async Task<Result<WorkAllocationDto>> GetOneAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Allocations.AsNoTracking().Where(m => m.WorkAllocationId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? WarehouseFailures.NotFound<WorkAllocationDto>() : Result<WorkAllocationDto>.Success(dto);
    }

    private static IQueryable<WorkAllocationDto> Project(IQueryable<WorkAllocation> q) =>
        q.Select(m => new WorkAllocationDto(m.WorkAllocationId, m.MovementType, m.SewingTeamId,
            m.SewingTeam.TeamName, m.WorkerId, m.Worker.EmployeeCode, m.Worker.FullName, m.OrderLineId,
            m.OrderLine.Order.OrderCode, m.OrderLine.ProductVariant.Product.ProductCode, m.OrderLine.ProductVariant.Color,
            m.OrderLine.ProductVariant.SizeCode, m.OperationId, m.Operation.OperationCode, m.Operation.OperationName,
            m.Operation.UsesSemiFinished, m.Quantity, m.ConfirmedQty,
            m.DiscrepancyReason, m.RetrievalReason, m.ConfirmMode, m.OnBehalfReason, m.Status, m.RejectReason,
            m.CreatedAt, m.ConfirmedAt));
}
