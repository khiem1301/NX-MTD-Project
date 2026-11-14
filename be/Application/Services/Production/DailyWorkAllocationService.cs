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
/// DEV-ALL-100: phần giao thêm là phiếu PENDING chờ công nhân xác nhận (UC60) hoặc tổ trưởng xác nhận hộ (UC54), chỉ khi đã xác nhận
/// mới vào trần sản lượng; phần hạ số xuống trừ trước vào phiếu đang chờ, phần còn lại ghi thu hồi ADJUSTED (CONFIRMED ngay).
/// DEV-ALL-64: cuối ngày không thu hồi, phần chưa làm hết chuyển sang hôm sau cho chính công nhân đó (tồn từ hôm trước); trần cộng dồn
/// của <c>vw_WorkerOutputCap</c> và trigger giữ nguyên.
/// </summary>
public sealed class DailyWorkAllocationService : IDailyWorkAllocationService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly IPieceRateService _rates;

    public DailyWorkAllocationService(IUnitOfWork uow, INotificationService notifier, IAppLock locks, IClock clock,
        IPieceRateService rates)
    {
        _uow = uow; _notifier = notifier; _locks = locks; _clock = clock; _rates = rates;
    }

    private IQueryable<WorkAllocation> Allocations => _uow.Repository<WorkAllocation>().Query();

    private sealed record Key(int WorkerId, int OrderLineId, int OperationId);

    /// <summary>
    /// Số liệu của một công nhân × dòng đơn × công đoạn: phần tồn từ các ngày trước (giao − sản lượng trước ngày), phần giao ròng và
    /// sản lượng của ngày, phần giữ ròng và sản lượng cộng dồn (chỉ phiếu đã xác nhận), phần giao của ngày đang chờ công nhân xác nhận.
    /// </summary>
    private sealed record Position(int Carry, int DayNet, int DayOutput, int Held, int Output, int DayPending)
    {
        public static readonly Position Empty = new(0, 0, 0, 0, 0, 0);

        /// <summary>Số giao của ngày tổ trưởng đã nhập: đã xác nhận cộng đang chờ xác nhận.</summary>
        public int DayGiven => DayNet + DayPending;

        /// <summary>Phần chưa làm hết tính cả tồn từ hôm trước: không vượt số công nhân còn giữ sau khi trừ sản lượng đã báo.</summary>
        public int Remaining => Math.Max(0, Math.Min(Carry + DayNet - DayOutput, Held - Output));
    }

    public async Task<Result<DailyAllocationBoardDto>> GetBoardAsync(int teamId, DateOnly workDate, int orderId, int? orderLineId,
        CancellationToken ct)
    {
        var allotted = Allotted(teamId);
        // Chỉ các dòng đơn (mã hàng × màu × size) đã chia cho tổ (DEV-ALL-49, DEV-ALL-68).
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId && (orderLineId == null || l.OrderLineId == orderLineId))
            .Where(l => allotted.Any(a => a.OrderLineId == l.OrderLineId))
            .Select(l => new
            {
                l.OrderLineId,
                l.ProductId,
                Order = l.Order.OrderCode,
                Product = l.ProductVariant.Product.ProductCode,
                l.ProductVariant.ProductColor.Color,
                l.ProductVariant.SizeCode
            }).ToListAsync(ct);
        var lineIds = lines.Select(l => l.OrderLineId).ToList();

        var progress = await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
            .Where(v => v.SewingTeamId == teamId && lineIds.Contains(v.OrderLineId!.Value))
            .Select(v => new { LineId = v.OrderLineId!.Value, v.OperationId, Unallocated = v.UnallocatedQty ?? 0 }).ToListAsync(ct);
        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => o.TeamType == TeamTypes.Sewing && o.Status == RecordStatus.Active)
            .Where(o => lines.Select(l => l.ProductId).Contains(o.ProductId))
            .Select(o => new
            {
                o.OperationId,
                o.ProductId,
                o.OperationCode,
                o.OperationName,
                o.StationSequence,
                Group = o.OperationGroup == null ? null : o.OperationGroup.GroupName
            }).ToListAsync(ct);
        var assigned = await _uow.Repository<OperationAssignment>().Query().AsNoTracking()
            .Where(a => a.SewingTeamId == teamId && a.OrderId == orderId && a.ValidTo == null)
            .Select(a => new Key(a.WorkerId, 0, a.OperationId)).ToListAsync(ct);

        var positions = await LoadPositionsAsync(teamId, lineIds, workDate, null, ct);
        var previous = await LoadPreviousAsync(teamId, lineIds, workDate, ct);
        var components = await LoadComponentsAsync(operations.Select(o => o.OperationId).ToList(), ct);
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
                var pos = positions.GetValueOrDefault(key) ?? Position.Empty;
                rows.Add(new DailyAllocationRowDto(w.EmployeeId, w.EmployeeCode, w.FullName, line.OrderLineId, line.Order, line.Product,
                    line.Color, line.SizeCode, op.OperationId, op.OperationCode, op.OperationName, op.Group,
                    assignedKeys.Contains((key.WorkerId, key.OperationId)), pos.Carry, pos.DayGiven, pos.DayOutput, pos.Remaining,
                    unallocatedByKey.GetValueOrDefault((line.OrderLineId, op.OperationId)), previous.GetValueOrDefault(key),
                    PendingQty: pos.DayPending, Components: components.GetValueOrDefault(op.OperationId) ?? []));
            }
        }
        var sequence = operations.ToDictionary(o => o.OperationId, o => o.StationSequence);
        var rates = await _rates.FindEffectiveManyAsync(rows.Select(r => r.OperationId).Distinct().ToList(), workDate, ct);
        var ordered = rows
            .Select(r =>
            {
                rates.TryGetValue(r.OperationId, out var rate);
                return r with { UnitRate = rate?.Rate, RateApproved = rate is not null };
            })
            .OrderBy(r => r.WorkerName).ThenBy(r => r.OrderLineId).ThenBy(r => sequence[r.OperationId]).ToList();
        return Result<DailyAllocationBoardDto>.Success(new DailyAllocationBoardDto(workDate, ordered));
    }

    public async Task<Result<DailyAllocationResultDto>> SaveAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        if (dto.WorkDate != _clock.Today)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DailyDateMustBeToday);
        return await _uow.InTransactionAsync(() => SaveInTransactionAsync(dto, teamId, ct));
    }

    private async Task<Result<DailyAllocationResultDto>> SaveInTransactionAsync(SaveDailyAllocationDto dto, int teamId, CancellationToken ct)
    {
        var invalid = await ValidateAsync(dto, teamId, ct);
        if (invalid is not null) return invalid;

        var lineIds = dto.Items.Select(i => i.OrderLineId).Distinct().OrderBy(i => i).ToList();
        foreach (var id in lineIds) await _locks.AcquireAsync($"work-allocation:{teamId}:{id}", ct);
        // Đọc số đã giao một lần (sau khi khóa) cho cả kiểm lý do lẫn ghi phần chênh.
        var workerIds = dto.Items.Select(i => i.WorkerId).Distinct().ToList();
        var positions = await LoadPositionsAsync(teamId, lineIds, dto.WorkDate, workerIds, ct);
        Position PositionOf(SaveDailyAllocationItem i) =>
            positions.GetValueOrDefault(new Key(i.WorkerId, i.OrderLineId, i.OperationId)) ?? Position.Empty;
        // Chỉ bắt lý do khi thật sự hạ số so với số đã lưu.
        if (string.IsNullOrWhiteSpace(dto.Reason) && dto.Items.Any(i => i.Quantity < PositionOf(i).DayGiven))
            return Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DailyReasonRequired);

        var unallocated = (await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
                .Where(v => v.SewingTeamId == teamId && lineIds.Contains(v.OrderLineId!.Value))
                .Select(v => new { LineId = v.OrderLineId!.Value, v.OperationId, Qty = v.UnallocatedQty ?? 0 }).ToListAsync(ct))
            .ToDictionary(v => (v.LineId, v.OperationId), v => v.Qty);
        var operationIds = dto.Items.Select(i => i.OperationId).Distinct().ToList();
        var labels = await _uow.Repository<Operation>().Query().AsNoTracking().Where(o => operationIds.Contains(o.OperationId))
            .ToDictionaryAsync(o => o.OperationId, o => o.OperationCode, ct);
        var pending = await LoadPendingRowsAsync(teamId, lineIds, dto.WorkDate, workerIds, ct);

        var (added, adjusted) = (0, 0);
        foreach (var item in dto.Items)
        {
            var pos = PositionOf(item);
            var delta = item.Quantity - pos.DayGiven;
            if (delta == 0) continue;
            var label = labels.GetValueOrDefault(item.OperationId) ?? string.Empty;
            var error = delta > 0
                ? await GiveMoreAsync(dto, teamId, item, delta, label, unallocated)
                : await LowerAsync(dto, teamId, item, pos, -delta, label, unallocated, pending);
            if (error is not null) return error;
            if (delta > 0) added++; else adjusted++;
        }
        if (added > 0)
            NotifyWorkers(dto.Items.Where(i => i.Quantity > PositionOf(i).DayGiven).Select(i => i.WorkerId).Distinct().ToList(), dto.WorkDate);
        if (added + adjusted > 0) await _uow.SaveChangesAsync();
        return Result<DailyAllocationResultDto>.Success(new DailyAllocationResultDto(added, adjusted),
            added > 0 ? WarehouseMessages.DailySavedPendingConfirm : WarehouseMessages.PieceMovementCreated);
    }

    /// <summary>Giao thêm (không vượt số tổ còn giao được của công đoạn): phiếu PENDING chờ công nhân xác nhận (DEV-ALL-100).</summary>
    private async Task<Result<DailyAllocationResultDto>?> GiveMoreAsync(SaveDailyAllocationDto dto, int teamId, SaveDailyAllocationItem item,
        int qty, string label, Dictionary<(int LineId, int OperationId), int> unallocated)
    {
        var key = (item.OrderLineId, item.OperationId);
        var free = unallocated.GetValueOrDefault(key);
        if (qty > free)
            return Failure(ErrorCodes.AllocationExceedsRequired, $"{WarehouseMessages.DailyExceedsUnallocated} {label}: còn {free}, cần thêm {qty}.");
        unallocated[key] = free - qty;
        await _uow.Repository<WorkAllocation>().AddAsync(NewRow(teamId, dto.WorkDate, item, PieceMovementTypes.Handover, qty));
        return null;
    }

    /// <summary>
    /// Hạ số giao của ngày: trừ trước vào phiếu giao đang chờ xác nhận, phần còn lại ghi thu hồi ADJUSTED từ phần đã xác nhận
    /// (tồn + phần đã xác nhận còn lại không dưới sản lượng đã báo của ngày, không thu quá phần chưa làm hết).
    /// </summary>
    private async Task<Result<DailyAllocationResultDto>?> LowerAsync(SaveDailyAllocationDto dto, int teamId, SaveDailyAllocationItem item,
        Position pos, int cut, string label, Dictionary<(int LineId, int OperationId), int> unallocated,
        Dictionary<Key, List<WorkAllocation>> pending)
    {
        var fromPending = Math.Min(cut, pos.DayPending);
        var fromConfirmed = cut - fromPending;
        if (fromConfirmed > 0 && pos.Carry + pos.DayNet - fromConfirmed < pos.DayOutput)
            return Failure(ErrorCodes.PieceRetrievalExceedsHolding,
                $"{WarehouseMessages.DailyBelowOutput} ({label}: tồn từ hôm trước {pos.Carry}, đã báo {pos.DayOutput}).");
        if (fromConfirmed > pos.Remaining)
            return Failure(ErrorCodes.PieceRetrievalExceedsHolding, $"{WarehouseMessages.PieceRetrievalExceedsHolding} ({label}: còn thu hồi được {pos.Remaining}).");

        var reason = dto.Reason!.Trim();
        WithdrawPending(pending.GetValueOrDefault(new Key(item.WorkerId, item.OrderLineId, item.OperationId)) ?? [], fromPending, reason);
        var key = (item.OrderLineId, item.OperationId);
        unallocated[key] = unallocated.GetValueOrDefault(key) + cut;
        if (fromConfirmed > 0)
        {
            var row = NewRow(teamId, dto.WorkDate, item, PieceMovementTypes.Retrieval, fromConfirmed);
            (row.RetrievalReason, row.RetrievalNote) = (RetrievalReasons.Adjusted, reason);
            await _uow.Repository<WorkAllocation>().AddAsync(row);
        }
        return null;
    }

    /// <summary>Bớt số trên các phiếu giao đang chờ (mới nhất trước); phiếu bớt hết thì đóng (REJECTED) kèm lý do của tổ trưởng.</summary>
    private void WithdrawPending(List<WorkAllocation> rows, int qty, string reason)
    {
        foreach (var row in rows.OrderByDescending(r => r.WorkAllocationId))
        {
            if (qty == 0) return;
            var take = Math.Min(qty, row.Quantity);
            qty -= take;
            if (take < row.Quantity)
            {
                row.Quantity -= take;
                continue;
            }
            row.Status = HandoverStatuses.Rejected;
            row.RejectReason = string.Format(WarehouseMessages.DailyPendingWithdrawn, reason);
            row.ConfirmedAt = _clock.Now;
        }
    }

    // ---------- Nội bộ ----------

    /// <summary>Giao: phiếu PENDING chờ công nhân xác nhận. Thu hồi do tổ trưởng hạ số: CONFIRMED ngay (ConfirmMode = LEADER).</summary>
    private WorkAllocation NewRow(int teamId, DateOnly workDate, SaveDailyAllocationItem item, string type, int qty)
    {
        var handover = type == PieceMovementTypes.Handover;
        return new WorkAllocation
        {
            WorkDate = workDate,
            MovementType = type,
            SewingTeamId = teamId,
            WorkerId = item.WorkerId,
            OrderLineId = item.OrderLineId,
            OperationId = item.OperationId,
            Quantity = qty,
            ConfirmedQty = handover ? null : qty,
            ConfirmMode = handover ? ConfirmModes.Worker : ConfirmModes.Leader,
            Status = handover ? HandoverStatuses.Pending : HandoverStatuses.Confirmed,
            CreatedAt = _clock.Now,
            ConfirmedAt = handover ? null : _clock.Now,
        };
    }

    /// <summary>
    /// Phần tồn trước ngày, phần giao ròng / sản lượng của ngày và cộng dồn (phiếu đã xác nhận), phần chờ xác nhận của ngày,
    /// theo công nhân × dòng đơn × công đoạn.
    /// </summary>
    private async Task<Dictionary<Key, Position>> LoadPositionsAsync(int teamId, List<int> lineIds, DateOnly workDate,
        IReadOnlyList<int>? workerIds, CancellationToken ct)
    {
        var moves = (await ConfirmedOf(teamId, lineIds)
            .Where(a => workerIds == null || workerIds.Contains(a.WorkerId))
            .GroupBy(a => new { a.WorkerId, a.OrderLineId, a.OperationId })
            .Select(g => new
            {
                g.Key.WorkerId,
                g.Key.OrderLineId,
                g.Key.OperationId,
                Held = g.Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? 0 : -(a.ConfirmedQty ?? 0)),
                Before = g.Where(a => a.WorkDate < workDate)
                    .Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? 0 : -(a.ConfirmedQty ?? 0)),
                Day = g.Where(a => a.WorkDate == workDate)
                    .Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? 0 : -(a.ConfirmedQty ?? 0)),
            }).ToListAsync(ct))
            .Where(m => m.Held != 0 || m.Day != 0).ToDictionary(m => new Key(m.WorkerId, m.OrderLineId, m.OperationId));
        var pending = (await PendingOf(teamId, lineIds, workDate, workerIds).AsNoTracking()
                .GroupBy(a => new { a.WorkerId, a.OrderLineId, a.OperationId })
                .Select(g => new { g.Key.WorkerId, g.Key.OrderLineId, g.Key.OperationId, Qty = g.Sum(a => a.Quantity) }).ToListAsync(ct))
            .ToDictionary(p => new Key(p.WorkerId, p.OrderLineId, p.OperationId), p => p.Qty);
        var outputs = await LoadOutputsAsync(teamId, lineIds, workDate, workerIds, ct);
        return moves.Keys.Union(pending.Keys).ToDictionary(k => k, k =>
        {
            var m = moves.GetValueOrDefault(k);
            var o = outputs.GetValueOrDefault(k) ?? OutputSums.Empty;
            return new Position(Math.Max(0, (m?.Before ?? 0) - o.Before), m?.Day ?? 0, o.Day, m?.Held ?? 0, o.All,
                pending.GetValueOrDefault(k));
        });
    }

    private sealed record OutputSums(int All, int Before, int Day)
    {
        public static readonly OutputSums Empty = new(0, 0, 0);
    }

    /// <summary>Sản lượng đã báo cộng dồn, trước ngày và của ngày, theo công nhân × dòng đơn × công đoạn.</summary>
    private async Task<Dictionary<Key, OutputSums>> LoadOutputsAsync(int teamId, List<int> lineIds, DateOnly workDate,
        IReadOnlyList<int>? workerIds, CancellationToken ct)
    {
        var outputs = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(r => r.SewingTeamId == teamId && lineIds.Contains(r.OrderLineId))
            .Where(r => workerIds == null || workerIds.Contains(r.WorkerId))
            .GroupBy(r => new { r.WorkerId, r.OrderLineId, r.OperationId })
            .Select(g => new
            {
                g.Key.WorkerId,
                g.Key.OrderLineId,
                g.Key.OperationId,
                All = g.Sum(r => r.Quantity),
                Before = g.Where(r => r.WorkDate < workDate).Sum(r => r.Quantity),
                Day = g.Where(r => r.WorkDate == workDate).Sum(r => r.Quantity)
            }).ToListAsync(ct);
        return outputs.ToDictionary(o => new Key(o.WorkerId, o.OrderLineId, o.OperationId), o => new OutputSums(o.All, o.Before, o.Day));
    }

    /// <summary>Phiếu giao của ngày đang chờ xác nhận (có theo dõi, để bớt số khi tổ trưởng hạ số giao).</summary>
    private async Task<Dictionary<Key, List<WorkAllocation>>> LoadPendingRowsAsync(int teamId, List<int> lineIds, DateOnly workDate,
        IReadOnlyList<int> workerIds, CancellationToken ct) =>
        (await PendingOf(teamId, lineIds, workDate, workerIds).ToListAsync(ct))
            .GroupBy(a => new Key(a.WorkerId, a.OrderLineId, a.OperationId)).ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>Thành phần (chi tiết vải) của từng công đoạn, theo tên.</summary>
    private async Task<Dictionary<int, IReadOnlyList<OperationComponentRefDto>>> LoadComponentsAsync(List<int> operationIds,
        CancellationToken ct)
    {
        var links = await _uow.Repository<OperationComponent>().Query().AsNoTracking()
            .Where(c => operationIds.Contains(c.OperationId))
            .Select(c => new
            {
                c.OperationId,
                c.ProductComponentId,
                c.ProductComponent.ComponentName
            }).ToListAsync(ct);
        return links.GroupBy(l => l.OperationId).ToDictionary(g => g.Key, g => (IReadOnlyList<OperationComponentRefDto>)g
            .OrderBy(l => l.ComponentName).Select(l => new OperationComponentRefDto(l.ProductComponentId, l.ComponentName)).ToList());
    }

    /// <summary>
    /// Số giao ròng của lần giao gần nhất trước ngày (ngày gần nhất có giao ròng &gt; 0), cho nút "Giao như lần trước". Gồm cả phiếu giao
    /// công nhân chưa xác nhận: "lần trước" là số tổ trưởng đã giao.
    /// </summary>
    private async Task<Dictionary<Key, int>> LoadPreviousAsync(int teamId, List<int> lineIds, DateOnly workDate, CancellationToken ct)
    {
        var days = await Allocations.AsNoTracking()
            .Where(a => a.SewingTeamId == teamId && lineIds.Contains(a.OrderLineId) && a.WorkDate < workDate)
            .Where(a => a.Status == HandoverStatuses.Confirmed
                        || (a.Status == HandoverStatuses.Pending && a.MovementType == PieceMovementTypes.Handover))
            .GroupBy(a => new { a.WorkerId, a.OrderLineId, a.OperationId, a.WorkDate })
            .Select(g => new
            {
                g.Key.WorkerId,
                g.Key.OrderLineId,
                g.Key.OperationId,
                g.Key.WorkDate,
                Net = g.Sum(a => a.MovementType == PieceMovementTypes.Handover ? a.ConfirmedQty ?? a.Quantity : -(a.ConfirmedQty ?? 0)),
            }).ToListAsync(ct);
        return days.Where(d => d.Net > 0).GroupBy(d => new Key(d.WorkerId, d.OrderLineId, d.OperationId))
            .ToDictionary(g => g.Key, g => g.MaxBy(d => d.WorkDate)!.Net);
    }

    private IQueryable<OrderLineTeam> Allotted(int teamId) =>
        _uow.Repository<OrderLineTeam>().Query().AsNoTracking().Where(a => a.SewingTeamId == teamId);

    private IQueryable<WorkAllocation> ConfirmedOf(int teamId, List<int> lineIds) => Allocations.AsNoTracking()
        .Where(a => a.SewingTeamId == teamId && a.Status == HandoverStatuses.Confirmed && lineIds.Contains(a.OrderLineId));

    /// <summary>Phiếu giao của ngày đang chờ công nhân xác nhận.</summary>
    private IQueryable<WorkAllocation> PendingOf(int teamId, List<int> lineIds, DateOnly workDate, IReadOnlyList<int>? workerIds) =>
        Allocations.Where(a => a.SewingTeamId == teamId && lineIds.Contains(a.OrderLineId) && a.WorkDate == workDate
                               && a.Status == HandoverStatuses.Pending && a.MovementType == PieceMovementTypes.Handover)
            .Where(a => workerIds == null || workerIds.Contains(a.WorkerId));

    /// <summary>Công nhân đúng tổ và còn làm, dòng đơn đã chia cho tổ và chưa đóng, công đoạn may đang dùng thuộc mã hàng của dòng đơn.</summary>
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
            .Where(l => lineIds.Contains(l.OrderLineId)).Select(l => new { l.OrderLineId, l.ProductId, l.Order.Status, l.Order.ReceivedDate }).ToListAsync(ct);
        if (lines.Count != lineIds.Count) return WarehouseFailures.NotFound<DailyAllocationResultDto>();
        var beforeStart = lines.Select(l => ProductionGuard.BeforeOrderStart(dto.WorkDate, l.ReceivedDate)).FirstOrDefault(m => m is not null);
        if (beforeStart is not null)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.WarehouseInvalidReference, beforeStart);
        if (await Allotted(teamId).CountAsync(a => lineIds.Contains(a.OrderLineId), ct) != lineIds.Count)
            return Result<DailyAllocationResultDto>.Failure(ErrorCodes.AssignmentProductNotAllotted, Messages.OrderLineNotAllotted);
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

        var giving = dto.Items.Where(i => i.Quantity > 0).Select(i => i.OperationId).Distinct().ToList();
        var noRate = await AllocationRateCheck.FindMissingAsync(_uow, giving, dto.WorkDate, ct);
        if (noRate is not null) return Result<DailyAllocationResultDto>.Failure(ErrorCodes.PieceRateNotApproved, noRate);
        return null;
    }

    private void NotifyWorkers(List<int> workerIds, DateOnly workDate)
    {
        if (workerIds.Count == 0) return;
        _notifier.NotifyEmployees(workerIds, new NotificationMessage(NotificationSources.Production, WarehouseMessages.DailyNotifyTitle,
            string.Format(WarehouseMessages.DailyNotifyBody, workDate), null));
    }

    private static Result<DailyAllocationResultDto> Failure(string code, string message) =>
        Result<DailyAllocationResultDto>.Failure(code, message);
}
