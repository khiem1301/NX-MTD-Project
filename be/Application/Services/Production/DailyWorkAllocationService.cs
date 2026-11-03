using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Giao sản lượng công đoạn theo ngày cho công nhân (DEV-ALL-63). Mỗi ngày tổ trưởng nhập số lần giao của từng công nhân × công đoạn
/// (lấy sẵn các công đoạn đã phân công trước khi may, thêm được công đoạn ngoài phân công); số của ngày là trần sản lượng cuối ca của ngày.
/// Phiếu do tổ trưởng ghi là CONFIRMED ngay (ConfirmMode = LEADER). Cuối ngày phần chưa làm hết được thu hồi kèm lý do, bởi tổ trưởng
/// hoặc hệ thống (ConfirmMode = SYSTEM). Trần cộng dồn của <c>vw_WorkerOutputCap</c> và trigger giữ nguyên: nhờ thu hồi mỗi ngày
/// nên trần cộng dồn bằng phần giao của ngày còn lại.
/// </summary>
public sealed class DailyWorkAllocationService : IDailyWorkAllocationService
{
    /// <summary>Số ngày chờ sau ngày làm việc trước khi hệ thống tự thu hồi: sản lượng của ngày D sửa được đến hết ngày D+1.</summary>
    private const int AutoRetrieveGraceDays = 1;

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly IAppLock _locks;
    private readonly IClock _clock;

    public DailyWorkAllocationService(IUnitOfWork uow, INotificationService notifier, IAppLock locks, IClock clock)
    {
        _uow = uow; _notifier = notifier; _locks = locks; _clock = clock;
    }

    private IQueryable<WorkAllocation> Allocations => _uow.Repository<WorkAllocation>().Query();

    private sealed record Key(int WorkerId, int OrderLineId, int OperationId);

    /// <summary>Số liệu của một công nhân × dòng đơn × công đoạn: phần giao ròng và sản lượng của ngày, phần giữ ròng và sản lượng cộng dồn.</summary>
    private sealed record Position(int DayNet, int DayOutput, int Held, int Output)
    {
        /// <summary>Phần chưa làm hết của ngày có thể thu hồi: không vượt số công nhân còn giữ sau khi trừ sản lượng đã báo.</summary>
        public int Remaining => Math.Max(0, Math.Min(DayNet - DayOutput, Held - Output));
    }

    public async Task<Result<DailyAllocationBoardDto>> GetBoardAsync(int teamId, DateOnly workDate, int orderId, int? orderLineId,
        CancellationToken ct)
    {
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId && (orderLineId == null || l.OrderLineId == orderLineId))
            .Select(l => new { l.OrderLineId, l.ProductId, Order = l.Order.OrderCode, Product = l.ProductVariant.Product.ProductCode,
                l.ProductVariant.Color, l.ProductVariant.SizeCode }).ToListAsync(ct);
        var lineIds = lines.Select(l => l.OrderLineId).ToList();

        var progress = await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
            .Where(v => v.SewingTeamId == teamId && lineIds.Contains(v.OrderLineId!.Value))
            .Select(v => new { LineId = v.OrderLineId!.Value, v.OperationId, Unallocated = v.UnallocatedQty ?? 0 }).ToListAsync(ct);
        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => o.TeamType == TeamTypes.Sewing && o.Status == RecordStatus.Active)
            .Where(o => lines.Select(l => l.ProductId).Contains(o.ProductId))
            .Select(o => new { o.OperationId, o.ProductId, o.OperationCode, o.OperationName, o.StationSequence,
                Group = o.OperationGroup == null ? null : o.OperationGroup.GroupName }).ToListAsync(ct);
        var assigned = await _uow.Repository<OperationAssignment>().Query().AsNoTracking()
            .Where(a => a.SewingTeamId == teamId && a.OrderId == orderId && a.ValidTo == null)
            .Select(a => new Key(a.WorkerId, 0, a.OperationId)).ToListAsync(ct);

        var positions = await LoadPositionsAsync(teamId, lineIds, workDate, null, ct);
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.TeamId == teamId).Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName }).ToListAsync(ct);
        var workerById = workers.ToDictionary(w => w.EmployeeId);
        var unallocatedByKey = progress.ToDictionary(p => (p.LineId, p.OperationId), p => p.Unallocated);

        var assignedKeys = new HashSet<(int WorkerId, int OperationId)>(assigned.Select(a => (a.WorkerId, a.OperationId)));
        var rows = new List<DailyAllocationRowDto>();
        foreach (var line in lines)
        {
            var lineOps = operations.Where(o => o.ProductId == line.ProductId).ToDictionary(o => o.OperationId);
            var keys = assigned.Where(a => lineOps.ContainsKey(a.OperationId)).Select(a => new Key(a.WorkerId, line.OrderLineId, a.OperationId))
                .Union(positions.Keys.Where(k => k.OrderLineId == line.OrderLineId && lineOps.ContainsKey(k.OperationId)))
                .Where(k => workerById.ContainsKey(k.WorkerId));
            foreach (var key in keys)
            {
                var op = lineOps[key.OperationId];
                var w = workerById[key.WorkerId];
                var pos = positions.GetValueOrDefault(key) ?? new Position(0, 0, 0, 0);
                rows.Add(new DailyAllocationRowDto(w.EmployeeId, w.EmployeeCode, w.FullName, line.OrderLineId, line.Order, line.Product,
                    line.Color, line.SizeCode, op.OperationId, op.OperationCode, op.OperationName, op.Group,
                    assignedKeys.Contains((key.WorkerId, key.OperationId)), pos.DayNet, pos.DayOutput, pos.Remaining,
                    unallocatedByKey.GetValueOrDefault((line.OrderLineId, op.OperationId))));
            }
        }
        var ordered = rows.OrderBy(r => r.WorkerName).ThenBy(r => r.OrderLineId)
            .ThenBy(r => operations.First(o => o.OperationId == r.OperationId).StationSequence).ToList();
        return Result<DailyAllocationBoardDto>.Success(new DailyAllocationBoardDto(workDate, ordered));
    }

    public async Task<Result<DailyAllocationResultDto>> SaveAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        if (dto.WorkDate > _clock.Today)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DailyDateInFuture);
        return await _uow.InTransactionAsync(() => SaveInTransactionAsync(dto, teamId, ct));
    }

    private async Task<Result<DailyAllocationResultDto>> SaveInTransactionAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        var invalid = await ValidateAsync(dto, teamId, ct);
        if (invalid is not null) return invalid;

        var lineIds = dto.Items.Select(i => i.OrderLineId).Distinct().OrderBy(i => i).ToList();
        foreach (var id in lineIds) await _locks.AcquireAsync($"work-allocation:{teamId}:{id}", ct);
        var positions = await LoadPositionsAsync(teamId, lineIds, dto.WorkDate, dto.Items.Select(i => i.WorkerId).Distinct().ToList(), ct);
        var unallocated = (await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
                .Where(v => v.SewingTeamId == teamId && lineIds.Contains(v.OrderLineId!.Value))
                .Select(v => new { LineId = v.OrderLineId!.Value, v.OperationId, Qty = v.UnallocatedQty ?? 0 }).ToListAsync(ct))
            .ToDictionary(v => (v.LineId, v.OperationId), v => v.Qty);

        var (added, adjusted) = (0, 0);
        foreach (var item in dto.Items)
        {
            var pos = positions.GetValueOrDefault(new Key(item.WorkerId, item.OrderLineId, item.OperationId)) ?? new Position(0, 0, 0, 0);
            var delta = item.Quantity - pos.DayNet;
            if (delta == 0) continue;
            var label = await OperationLabelAsync(item.OperationId, ct);
            if (delta > 0)
            {
                var free = unallocated.GetValueOrDefault((item.OrderLineId, item.OperationId));
                if (delta > free)
                    return Failure(ErrorCodes.AllocationExceedsRequired, $"{WarehouseMessages.DailyExceedsUnallocated} {label}: còn {free}, cần thêm {delta}.");
                unallocated[(item.OrderLineId, item.OperationId)] = free - delta;
                await _uow.Repository<WorkAllocation>().AddAsync(NewRow(teamId, dto.WorkDate, item, PieceMovementTypes.Handover, delta, null, null, ConfirmModes.Leader));
                added++;
            }
            else
            {
                if (item.Quantity < pos.DayOutput)
                    return Failure(ErrorCodes.PieceRetrievalExceedsHolding, $"{WarehouseMessages.DailyBelowOutput} ({label}: đã báo {pos.DayOutput}).");
                if (-delta > pos.Remaining)
                    return Failure(ErrorCodes.PieceRetrievalExceedsHolding, $"{WarehouseMessages.PieceRetrievalExceedsHolding} ({label}: còn thu hồi được {pos.Remaining}).");
                unallocated[(item.OrderLineId, item.OperationId)] = unallocated.GetValueOrDefault((item.OrderLineId, item.OperationId)) - delta;
                await _uow.Repository<WorkAllocation>().AddAsync(NewRow(teamId, dto.WorkDate, item, PieceMovementTypes.Retrieval, -delta,
                    RetrievalReasons.Adjusted, dto.Reason!.Trim(), ConfirmModes.Leader));
                adjusted++;
            }
        }
        if (added + adjusted > 0)
        {
            NotifyWorkers(dto.Items.Where(i => positions.GetValueOrDefault(new Key(i.WorkerId, i.OrderLineId, i.OperationId))?.DayNet != i.Quantity)
                .Select(i => i.WorkerId).Distinct().ToList(), dto.WorkDate);
            await _uow.SaveChangesAsync();
        }
        return Result<DailyAllocationResultDto>.Success(new DailyAllocationResultDto(added, adjusted, 0), WarehouseMessages.PieceMovementCreated);
    }

    public async Task<Result<DailyAllocationResultDto>> RetrieveRemainingAsync(RetrieveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        if (dto.WorkDate > _clock.Today)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DailyDateInFuture);
        return await _uow.InTransactionAsync(async () =>
        {
            var count = await RetrieveAsync(teamId, dto.WorkDate, dto.WorkerIds, dto.Reason.Trim(), ConfirmModes.Leader, ct);
            if (count == 0)
                return Result<DailyAllocationResultDto>.Failure(ErrorCodes.PieceRetrievalExceedsHolding, WarehouseMessages.DailyNothingToRetrieve);
            await _uow.SaveChangesAsync();
            return Result<DailyAllocationResultDto>.Success(new DailyAllocationResultDto(0, 0, count), WarehouseMessages.PieceMovementCreated);
        });
    }

    public async Task<int> AutoRetrieveAsync(CancellationToken ct)
    {
        var lastDay = _clock.Today.AddDays(-1 - AutoRetrieveGraceDays);
        // Mỗi (tổ, ngày) một giao dịch: tổ này lỗi không chặn tổ khác.
        var pending = await Allocations.AsNoTracking()
            .Where(a => a.Status == HandoverStatuses.Confirmed && a.WorkDate <= lastDay)
            .Select(a => new { a.SewingTeamId, a.WorkDate }).Distinct().ToListAsync(ct);
        var total = 0;
        foreach (var slot in pending)
        {
            var count = 0;
            await _uow.InTransactionAsync(async () =>
            {
                count = await RetrieveAsync(slot.SewingTeamId, slot.WorkDate, null, WarehouseMessages.DailyAutoRetrieveNote, ConfirmModes.System, ct);
                if (count > 0) await _uow.SaveChangesAsync();
                return Result.Success();
            });
            total += count;
        }
        return total;
    }

    // ---------- Nội bộ ----------

    /// <summary>Thu hồi phần chưa làm hết của ngày. Chưa gọi SaveChanges. Trả số phiếu thu hồi đã thêm.</summary>
    private async Task<int> RetrieveAsync(int teamId, DateOnly workDate, IReadOnlyList<int>? workerIds, string note, string mode,
        CancellationToken ct)
    {
        var lineIds = await Allocations.AsNoTracking()
            .Where(a => a.SewingTeamId == teamId && a.WorkDate == workDate && a.Status == HandoverStatuses.Confirmed)
            .Select(a => a.OrderLineId).Distinct().OrderBy(i => i).ToListAsync(ct);
        foreach (var id in lineIds) await _locks.AcquireAsync($"work-allocation:{teamId}:{id}", ct);
        var positions = await LoadPositionsAsync(teamId, lineIds, workDate, workerIds is { Count: > 0 } ? workerIds : null, ct);
        var count = 0;
        foreach (var (key, pos) in positions.Where(p => p.Value.Remaining > 0))
        {
            var item = new SaveDailyAllocationItem(key.WorkerId, key.OrderLineId, key.OperationId, 0);
            await _uow.Repository<WorkAllocation>().AddAsync(NewRow(teamId, workDate, item, PieceMovementTypes.Retrieval, pos.Remaining,
                RetrievalReasons.EndOfDay, note, mode));
            count++;
        }
        if (count > 0 && mode == ConfirmModes.Leader)
            NotifyWorkers(positions.Where(p => p.Value.Remaining > 0).Select(p => p.Key.WorkerId).Distinct().ToList(), workDate);
        return count;
    }

    private WorkAllocation NewRow(int teamId, DateOnly workDate, SaveDailyAllocationItem item, string type, int qty,
        string? reason, string? note, string mode) => new()
    {
        WorkDate = workDate,
        MovementType = type,
        SewingTeamId = teamId,
        WorkerId = item.WorkerId,
        OrderLineId = item.OrderLineId,
        OperationId = item.OperationId,
        Quantity = qty,
        ConfirmedQty = qty,
        RetrievalReason = reason,
        RetrievalNote = note,
        ConfirmMode = mode,
        Status = HandoverStatuses.Confirmed,
        CreatedAt = _clock.Now,
        ConfirmedAt = _clock.Now,
    };

    /// <summary>Phần giao ròng / sản lượng của ngày và cộng dồn, theo công nhân × dòng đơn × công đoạn của các dòng đơn đã cho.</summary>
    private async Task<Dictionary<Key, Position>> LoadPositionsAsync(int teamId, List<int> lineIds, DateOnly workDate,
        IReadOnlyList<int>? workerIds, CancellationToken ct)
    {
        var moves = await Allocations.AsNoTracking()
            .Where(a => a.SewingTeamId == teamId && a.Status == HandoverStatuses.Confirmed && lineIds.Contains(a.OrderLineId))
            .Where(a => workerIds == null || workerIds.Contains(a.WorkerId))
            .GroupBy(a => new { a.WorkerId, a.OrderLineId, a.OperationId })
            .Select(g => new
            {
                g.Key.WorkerId, g.Key.OrderLineId, g.Key.OperationId,
                Held = g.Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? 0 : -(a.ConfirmedQty ?? 0)),
                Day = g.Where(a => a.WorkDate == workDate)
                    .Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? 0 : -(a.ConfirmedQty ?? 0)),
            }).ToListAsync(ct);
        var outputs = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(r => r.SewingTeamId == teamId && lineIds.Contains(r.OrderLineId))
            .Where(r => workerIds == null || workerIds.Contains(r.WorkerId))
            .GroupBy(r => new { r.WorkerId, r.OrderLineId, r.OperationId })
            .Select(g => new { g.Key.WorkerId, g.Key.OrderLineId, g.Key.OperationId, All = g.Sum(r => r.Quantity),
                Day = g.Where(r => r.WorkDate == workDate).Sum(r => r.Quantity) }).ToListAsync(ct);
        var outputByKey = outputs.ToDictionary(o => new Key(o.WorkerId, o.OrderLineId, o.OperationId));
        return moves.Where(m => m.Held != 0 || m.Day != 0).ToDictionary(m => new Key(m.WorkerId, m.OrderLineId, m.OperationId), m =>
        {
            var o = outputByKey.GetValueOrDefault(new Key(m.WorkerId, m.OrderLineId, m.OperationId));
            return new Position(m.Day, o?.Day ?? 0, m.Held, o?.All ?? 0);
        });
    }

    /// <summary>Công nhân đúng tổ và còn làm, dòng đơn chưa đóng, công đoạn may đang dùng thuộc mã hàng của dòng đơn; hạ số giao phải có lý do.</summary>
    private async Task<Result<DailyAllocationResultDto>?> ValidateAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct);
        if (team is null || team.TeamType != TeamTypes.Sewing) return WarehouseFailures.TeamMismatch<DailyAllocationResultDto>();

        var workerIds = dto.Items.Select(i => i.WorkerId).Distinct().ToList();
        var okWorkers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => workerIds.Contains(e.EmployeeId) && e.TeamId == teamId && e.Status == RecordStatus.Active)
            .Select(e => e.EmployeeId).ToListAsync(ct);
        if (okWorkers.Count != workerIds.Count)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseTeamMismatch, WarehouseMessages.WorkerNotInTeam, ResultKind.Forbidden);

        var lineIds = dto.Items.Select(i => i.OrderLineId).Distinct().ToList();
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => lineIds.Contains(l.OrderLineId)).Select(l => new { l.OrderLineId, l.ProductId, l.Order.Status }).ToListAsync(ct);
        if (lines.Count != lineIds.Count) return WarehouseFailures.NotFound<DailyAllocationResultDto>();
        if (lines.Any(l => OrderStatuses.Closed.Contains(l.Status)))
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var operationIds = dto.Items.Select(i => i.OperationId).Distinct().ToList();
        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => operationIds.Contains(o.OperationId)).Select(o => new { o.OperationId, o.ProductId, o.Status, o.TeamType }).ToListAsync(ct);
        var productByLine = lines.ToDictionary(l => l.OrderLineId, l => l.ProductId);
        foreach (var item in dto.Items)
        {
            var op = operations.FirstOrDefault(o => o.OperationId == item.OperationId);
            if (op is null || op.ProductId != productByLine[item.OrderLineId])
                return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OperationNotOfProduct);
            if (item.Quantity > 0 && (op.Status != RecordStatus.Active || op.TeamType != TeamTypes.Sewing))
                return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OperationNotAllocatable);
        }
        // Chỉ bắt lý do khi thật sự hạ số: biết được sau khi đọc số hiện tại, nên kiểm ở đây theo số đã lưu.
        if (string.IsNullOrWhiteSpace(dto.Reason) && await AnyDecreaseAsync(dto, teamId, ct))
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DailyReasonRequired);
        return null;
    }

    private async Task<bool> AnyDecreaseAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        var positions = await LoadPositionsAsync(teamId, dto.Items.Select(i => i.OrderLineId).Distinct().ToList(), dto.WorkDate,
            dto.Items.Select(i => i.WorkerId).Distinct().ToList(), ct);
        return dto.Items.Any(i => i.Quantity < (positions.GetValueOrDefault(new Key(i.WorkerId, i.OrderLineId, i.OperationId))?.DayNet ?? 0));
    }

    private async Task<string> OperationLabelAsync(int operationId, CancellationToken ct) =>
        await _uow.Repository<Operation>().Query().AsNoTracking().Where(o => o.OperationId == operationId)
            .Select(o => o.OperationCode).FirstOrDefaultAsync(ct) ?? string.Empty;

    private void NotifyWorkers(List<int> workerIds, DateOnly workDate)
    {
        if (workerIds.Count == 0) return;
        _notifier.NotifyEmployees(workerIds, new NotificationMessage(NotificationSources.Production, "Việc được giao đã thay đổi",
            $"Tổ trưởng vừa cập nhật sản lượng công đoạn được giao ngày {workDate:dd/MM/yyyy}, vui lòng kiểm tra.", null));
    }

    private static Result<DailyAllocationResultDto> Failure(string code, string message) =>
        Result<DailyAllocationResultDto>.Failure(code, message);
}
