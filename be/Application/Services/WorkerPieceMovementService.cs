using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC53 / UC54 / UC60: tổ trưởng may giao hoặc thu hồi BTP với công nhân; công nhân tự xác nhận (UC60) hoặc từ chối,
/// tổ trưởng xác nhận hộ khi công nhân không tự xác nhận được (UC54, bắt buộc lý do). Số xác nhận cập nhật
/// <c>vw_TeamPieceHolding</c> và <c>vw_WorkerOutputCap</c> (trần sản lượng MSG16 của luồng may).
/// </summary>
public sealed class WorkerPieceMovementService : IWorkerPieceMovementService
{
    private readonly IUnitOfWork _uow;
    private readonly IInventoryService _inventory;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IAppLock _locks;
    private readonly IClock _clock;

    public WorkerPieceMovementService(IUnitOfWork uow, IInventoryService inventory, INotificationService notifier,
        IAuditLogService audit, IAppLock locks, IClock clock)
    {
        _uow = uow; _inventory = inventory; _notifier = notifier; _audit = audit; _locks = locks; _clock = clock;
    }

    private IQueryable<WorkerPieceMovement> Movements => _uow.Repository<WorkerPieceMovement>().Query();

    public async Task<Result<PagedResult<WorkerPieceMovementDto>>> GetPagedAsync(WorkerMovementQuery query, int? teamId,
        int? workerId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Project(Movements.AsNoTracking()
                .Where(m => teamId == null || m.SewingTeamId == teamId)
                .Where(m => workerId == null || m.WorkerId == workerId)
                .Where(m => query.WorkerId == null || m.WorkerId == query.WorkerId)
                .Where(m => string.IsNullOrEmpty(query.Status) || m.Status == query.Status)
                .Where(m => string.IsNullOrEmpty(query.MovementType) || m.MovementType == query.MovementType)
                .Where(m => query.OrderId == null || m.OrderLine.OrderId == query.OrderId)
                .Where(m => string.IsNullOrEmpty(keyword) || m.Worker.FullName.Contains(keyword)
                            || m.Worker.EmployeeCode.Contains(keyword) || m.OrderLine.Order.OrderCode.Contains(keyword))
                .OrderByDescending(m => m.WorkerPieceMovementId))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<WorkerPieceMovementDto>>.Success(page);
    }

    public async Task<Result<PieceMovementHoldingsDto>> GetHoldingsAsync(int teamId, int? orderId, CancellationToken ct)
    {
        var teamLines = (await _inventory.GetTeamHoldingsAsync(teamId, orderId, ct)).Data ?? Array.Empty<TeamPieceHoldingDto>();
        var pending = await Movements.AsNoTracking()
            .Where(m => m.SewingTeamId == teamId && m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Handover)
            .GroupBy(m => m.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var team = teamLines.Where(l => l.HeldByTeam > 0 || pending.ContainsKey(l.OrderLineId))
            .Select(l =>
            {
                var p = pending.GetValueOrDefault(l.OrderLineId);
                return new TeamHoldingLineDto(l.OrderLineId, l.OrderCode, l.ProductCode, l.Color, l.SizeCode, l.HeldByTeam, p,
                    Math.Max(0, l.HeldByTeam - p));
            }).ToList();

        var rows = await Movements.AsNoTracking()
            .Where(m => m.SewingTeamId == teamId && (orderId == null || m.OrderLine.OrderId == orderId))
            .GroupBy(m => new
            {
                m.WorkerId, m.Worker.EmployeeCode, m.Worker.FullName, m.OrderLineId, m.OrderLine.Order.OrderCode,
                m.OrderLine.Product.ProductCode, m.OrderLine.Color, m.OrderLine.SizeCode,
            })
            .Select(g => new
            {
                g.Key.WorkerId, g.Key.EmployeeCode, g.Key.FullName, g.Key.OrderLineId, g.Key.OrderCode, g.Key.ProductCode,
                g.Key.Color, g.Key.SizeCode,
                Held = g.Where(m => m.Status == HandoverStatuses.Confirmed)
                    .Sum(m => m.MovementType == PieceMovementTypes.Handover ? m.ConfirmedQty ?? 0 : -(m.ConfirmedQty ?? 0)),
                PendingRetrieval = g.Where(m => m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Retrieval)
                    .Sum(m => m.Quantity),
            }).ToListAsync(ct);
        var workers = rows.Where(r => r.Held > 0)
            .OrderBy(r => r.FullName).ThenBy(r => r.OrderCode).ThenBy(r => r.Color).ThenBy(r => r.SizeCode)
            .Select(r => new WorkerHoldingLineDto(r.WorkerId, r.EmployeeCode, r.FullName, r.OrderLineId, r.OrderCode,
                r.ProductCode, r.Color, r.SizeCode, r.Held, r.PendingRetrieval, Math.Max(0, r.Held - r.PendingRetrieval)))
            .ToList();
        return Result<PieceMovementHoldingsDto>.Success(new PieceMovementHoldingsDto(team, workers));
    }

    public async Task<Result<WorkerPieceMovementDto>> CreateAsync(CreateWorkerMovementDto dto, int teamId,
        CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct);
            if (team is null || team.TeamType != TeamTypes.Sewing) return WarehouseFailures.TeamMismatch<int>();

            var worker = await _uow.Repository<Employee>().Query().AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == dto.WorkerId, ct);
            if (worker is null || worker.TeamId != teamId || worker.Status != RecordStatus.Active)
                return Result<int>.Failure(ErrorCodes.WarehouseTeamMismatch, WarehouseMessages.WorkerNotInTeam, ResultKind.Forbidden);

            var line = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.Order)
                .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId, ct);
            if (line is null) return WarehouseFailures.NotFound<int>();
            if (OrderStatuses.Closed.Contains(line.Order.Status))
                return Result<int>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
            if (dto.OperationId is { } opId)
            {
                var opOk = await _uow.Repository<Operation>().Query().AnyAsync(o => o.OperationId == opId && o.ProductId == line.ProductId, ct);
                if (!opOk) return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OperationNotOfProduct);
            }

            await _locks.AcquireAsync($"piece-move:{teamId}:{dto.OrderLineId}", ct);
            var available = dto.MovementType == PieceMovementTypes.Handover
                ? await TeamAvailableAsync(teamId, dto.OrderLineId, ct)
                : await WorkerRetrievableAsync(dto.WorkerId, dto.OrderLineId, ct);
            if (dto.Quantity > available)
                return Result<int>.Failure(
                    dto.MovementType == PieceMovementTypes.Handover ? ErrorCodes.PieceHandoverExceedsHolding : ErrorCodes.PieceRetrievalExceedsHolding,
                    $"{(dto.MovementType == PieceMovementTypes.Handover ? WarehouseMessages.PieceHandoverExceedsHolding : WarehouseMessages.PieceRetrievalExceedsHolding)}: còn {available}, yêu cầu {dto.Quantity}.");

            var movement = new WorkerPieceMovement
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
            await _uow.Repository<WorkerPieceMovement>().AddAsync(movement);
            _notifier.NotifyEmployees(new[] { dto.WorkerId }, new NotificationMessage(NotificationSources.Warehouse,
                dto.MovementType == PieceMovementTypes.Handover ? "Có BTP được giao cho bạn" : "Có BTP được thu hồi từ bạn",
                $"Tổ trưởng ghi {(dto.MovementType == PieceMovementTypes.Handover ? "giao" : "thu hồi")} {dto.Quantity} BTP ({line.Color} - {line.SizeCode}), vui lòng đếm và xác nhận.",
                null));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(movement.WorkerPieceMovementId, WarehouseMessages.PieceMovementCreated);
        });
        return created.IsSuccess ? await GetOneAsync(created.Data, ct) : WarehouseFailures.From<WorkerPieceMovementDto>(created);
    }

    public Task<Result> ConfirmByWorkerAsync(int movementId, ConfirmWorkerMovementDto dto, int workerId, CancellationToken ct) =>
        DecideAsync(movementId, ct, m => m.WorkerId == workerId, m => ApplyConfirmAsync(m, dto.ConfirmedQty,
            dto.DiscrepancyReason, ConfirmModes.Worker, null, ct), notifyLeaders: true);

    public Task<Result> ConfirmOnBehalfAsync(int movementId, ConfirmOnBehalfDto dto, int teamId, CancellationToken ct) =>
        DecideAsync(movementId, ct, m => m.SewingTeamId == teamId, m => ApplyConfirmAsync(m, dto.ConfirmedQty,
            dto.DiscrepancyReason, ConfirmModes.OnBehalf, dto.OnBehalfReason.Trim(), ct), notifyLeaders: false);

    public Task<Result> RejectByWorkerAsync(int movementId, ReasonDto dto, int workerId, CancellationToken ct) =>
        DecideAsync(movementId, ct, m => m.WorkerId == workerId, m =>
        {
            m.Status = HandoverStatuses.Rejected;
            m.RejectReason = dto.Reason.Trim();
            m.ConfirmedAt = _clock.Now;
            return Task.FromResult(Result.Success(WarehouseMessages.PieceMovementRejected));
        }, notifyLeaders: true);

    // ---------- Nội bộ ----------

    private Task<Result> DecideAsync(int movementId, CancellationToken ct, Func<WorkerPieceMovement, bool> allowed,
        Func<WorkerPieceMovement, Task<Result>> apply, bool notifyLeaders) =>
        _uow.InTransactionAsync(async () =>
        {
            var movement = await Movements.Include(m => m.OrderLine).FirstOrDefaultAsync(m => m.WorkerPieceMovementId == movementId, ct);
            if (movement is null || !allowed(movement)) return WarehouseFailures.NotFound();
            if (movement.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            await _locks.AcquireAsync($"piece-move:{movement.SewingTeamId}:{movement.OrderLineId}", ct);
            var before = new { movement.Status };
            var result = await apply(movement);
            if (!result.IsSuccess) return result;

            _audit.Add(new AuditEntry(nameof(WorkerPieceMovement), movement.WorkerPieceMovementId, AuditActions.Update, before,
                new { movement.Status, movement.ConfirmedQty, movement.ConfirmMode }, movement.OnBehalfReason ?? movement.RejectReason));
            if (notifyLeaders)
            {
                var leaders = await _uow.Repository<Employee>().Query().AsNoTracking()
                    .Where(e => e.TeamId == movement.SewingTeamId && e.Status == RecordStatus.Active
                                && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
                    .Select(e => e.EmployeeId).ToListAsync(ct);
                _notifier.NotifyEmployees(leaders, new NotificationMessage(NotificationSources.Warehouse,
                    movement.Status == HandoverStatuses.Confirmed ? "Công nhân đã xác nhận BTP" : "Công nhân từ chối phiếu BTP",
                    $"Phiếu BTP {movement.OrderLine.Color} - {movement.OrderLine.SizeCode}: {(movement.Status == HandoverStatuses.Confirmed ? "đã xác nhận" : "bị từ chối")}.",
                    null));
            }
            await _uow.SaveChangesAsync();
            return result;
        });

    private async Task<Result> ApplyConfirmAsync(WorkerPieceMovement m, int confirmedQty, string? discrepancy, string mode,
        string? onBehalfReason, CancellationToken ct)
    {
        if (confirmedQty > m.Quantity)
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.PieceConfirmedExceedsQty);
        if (confirmedQty != m.Quantity && string.IsNullOrWhiteSpace(discrepancy))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);

        // Kiểm tra lại với số đã xác nhận đến lúc này: các phiếu khác có thể đã dùng hết số BTP.
        var available = m.MovementType == PieceMovementTypes.Handover
            ? await TeamAvailableAsync(m.SewingTeamId, m.OrderLineId, ct, excludeId: m.WorkerPieceMovementId)
            : await WorkerRetrievableAsync(m.WorkerId, m.OrderLineId, ct, excludeId: m.WorkerPieceMovementId);
        if (confirmedQty > available)
            return Result.Failure(
                m.MovementType == PieceMovementTypes.Handover ? ErrorCodes.PieceHandoverExceedsHolding : ErrorCodes.PieceRetrievalExceedsHolding,
                m.MovementType == PieceMovementTypes.Handover ? WarehouseMessages.PieceHandoverExceedsHolding : WarehouseMessages.PieceRetrievalExceedsHolding);

        m.ConfirmedQty = confirmedQty;
        m.DiscrepancyReason = confirmedQty == m.Quantity ? null : discrepancy!.Trim();
        m.ConfirmMode = mode;
        m.OnBehalfReason = onBehalfReason;
        m.Status = HandoverStatuses.Confirmed;
        m.ConfirmedAt = _clock.Now;
        return Result.Success(WarehouseMessages.PieceMovementConfirmed);
    }

    /// <summary>BTP tổ đang giữ − số đang chờ công nhân nhận (bỏ qua chính phiếu đang xác nhận).</summary>
    private async Task<int> TeamAvailableAsync(int teamId, int orderLineId, CancellationToken ct, int? excludeId = null)
    {
        var held = await _inventory.GetTeamHeldQtyAsync(teamId, orderLineId, ct);
        var pending = await Movements.Where(m => m.SewingTeamId == teamId && m.OrderLineId == orderLineId
                && m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Handover
                && m.WorkerPieceMovementId != excludeId)
            .SumAsync(m => m.Quantity, ct);
        return Math.Max(0, held - pending);
    }

    /// <summary>BTP công nhân đang giữ (đã nhận − đã trả) − số đang chờ xác nhận thu hồi.</summary>
    private async Task<int> WorkerRetrievableAsync(int workerId, int orderLineId, CancellationToken ct, int? excludeId = null)
    {
        var rows = await Movements.Where(m => m.WorkerId == workerId && m.OrderLineId == orderLineId).ToListAsync(ct);
        var held = rows.Where(m => m.Status == HandoverStatuses.Confirmed)
            .Sum(m => m.MovementType == PieceMovementTypes.Handover ? m.ConfirmedQty ?? 0 : -(m.ConfirmedQty ?? 0));
        var pending = rows.Where(m => m.Status == HandoverStatuses.Pending && m.MovementType == PieceMovementTypes.Retrieval
                                      && m.WorkerPieceMovementId != excludeId).Sum(m => m.Quantity);
        return Math.Max(0, held - pending);
    }

    private async Task<Result<WorkerPieceMovementDto>> GetOneAsync(int id, CancellationToken ct)
    {
        var dto = await Project(Movements.AsNoTracking().Where(m => m.WorkerPieceMovementId == id)).FirstOrDefaultAsync(ct);
        return dto is null ? WarehouseFailures.NotFound<WorkerPieceMovementDto>() : Result<WorkerPieceMovementDto>.Success(dto);
    }

    private static IQueryable<WorkerPieceMovementDto> Project(IQueryable<WorkerPieceMovement> q) =>
        q.Select(m => new WorkerPieceMovementDto(m.WorkerPieceMovementId, m.MovementType, m.SewingTeamId,
            m.SewingTeam.TeamName, m.WorkerId, m.Worker.EmployeeCode, m.Worker.FullName, m.OrderLineId,
            m.OrderLine.Order.OrderCode, m.OrderLine.Product.ProductCode, m.OrderLine.Color, m.OrderLine.SizeCode,
            m.OperationId, m.Operation == null ? null : m.Operation.OperationName, m.Quantity, m.ConfirmedQty,
            m.DiscrepancyReason, m.RetrievalReason, m.ConfirmMode, m.OnBehalfReason, m.Status, m.RejectReason,
            m.CreatedAt, m.ConfirmedAt));
}
