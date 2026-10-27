using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// KI-04 + KI-11 (UC55, UC22): sản lượng cuối ca của tổ may theo công nhân × công đoạn × dòng đơn.
/// <list type="bullet">
/// <item>Đơn giá khóa lúc nhập: lấy đơn giá APPROVED hiệu lực tại ngày làm việc; chưa có thì chặn (MSG18, GBR-11).</item>
/// <item>Trần (MSG16, database 25/10): sản lượng của công nhân ở một công đoạn × dòng đơn ≤ số lần tổ trưởng đã giao và
/// công nhân đã xác nhận (<c>vw_WorkerOutputCap</c>); chưa được giao thì trần bằng 0. Database cũng chặn cứng (50052).</item>
/// <item>Hạn sửa (GBR-09): hết ngày làm việc kế tiếp; sau hạn chỉ Quản đốc sửa, bắt buộc lý do, ghi AuditLog (MSG20).</item>
/// <item>Công nhân chưa có giờ vào hôm đó: cảnh báo MSG17, không chặn.</item>
/// </list>
/// </summary>
public sealed class OutputService : IOutputService
{
    private readonly IUnitOfWork _uow;
    private readonly ProductionGuard _guard;
    private readonly IPieceRateService _rates;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public OutputService(IUnitOfWork uow, ProductionGuard guard, IPieceRateService rates, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _guard = guard; _rates = rates; _audit = audit; _clock = clock;
    }

    private IQueryable<OutputRecord> Records => _uow.Repository<OutputRecord>().Query();

    // ---------- Lưới nhập ----------

    public async Task<Result<OutputGridDto>> GetGridAsync(
        DateOnly workDate, string shiftCode, int orderLineId, int? sewingTeamId, CancellationToken ct)
    {
        var shift = CodeText.ToCode(shiftCode);
        if (!ShiftCodes.All.Contains(shift))
            return Result<OutputGridDto>.Failure(ErrorCodes.OutputInvalid, "Ca làm việc không hợp lệ.", ResultKind.Validation);
        var team = _guard.ResolveSewingTeam(sewingTeamId);
        if (!team.IsSuccess) return Fail.As<OutputGridDto>(team);

        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrderLineId == orderLineId, ct);
        if (line is null) return Fail.NotFound<OutputGridDto>();

        var assignments = await _uow.Repository<OperationAssignment>().Query().AsNoTracking()
            .Where(a => a.SewingTeamId == team.Data && a.OrderId == line.OrderId && a.ValidTo == null
                        && a.Operation.ProductId == line.ProductId && a.Operation.Status == OperationStatuses.Active)
            .Select(a => new { a.OperationId, a.WorkerId })
            .ToListAsync(ct);
        var records = await Records.AsNoTracking().Include(r => r.PieceRate)
            .Where(r => r.WorkDate == workDate && r.ShiftCode == shift && r.SewingTeamId == team.Data
                        && r.OrderLineId == orderLineId)
            .ToListAsync(ct);

        // Số lần được giao (trần) và đã báo của từng công nhân × công đoạn ở dòng đơn này.
        var caps = await _uow.Repository<VwWorkerOutputCap>().Query().AsNoTracking()
            .Where(v => v.OrderLineId == orderLineId).ToListAsync(ct);
        var capByKey = caps.ToDictionary(v => (v.OperationId, v.WorkerId));
        var teamWorkerIds = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.TeamId == team.Data).Select(e => e.EmployeeId).ToListAsync(ct);

        // Công nhân được giao việc nhưng chưa có phân công công đoạn vẫn có dòng để báo sản lượng.
        var keys = assignments.Select(a => (a.OperationId, a.WorkerId))
            .Union(records.Select(r => (r.OperationId, r.WorkerId)))
            .Union(caps.Where(c => (c.CapQty ?? 0) > 0 && teamWorkerIds.Contains(c.WorkerId)).Select(c => (c.OperationId, c.WorkerId))).ToList();
        var operationIds = keys.Select(k => k.OperationId).Distinct().ToList();
        var workerIds = keys.Select(k => k.WorkerId).Distinct().ToList();
        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => operationIds.Contains(o.OperationId)).ToDictionaryAsync(o => o.OperationId, ct);
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => workerIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, ct);

        var assigned = assignments.Select(a => (a.OperationId, a.WorkerId)).ToHashSet();
        var recordByKey = records.ToDictionary(r => (r.OperationId, r.WorkerId));
        var newRowOpen = _clock.Now <= ProductionGuard.NextWorkingDayEnd(workDate) || _guard.IsManager;

        var rows = new List<OutputGridRowDto>();
        foreach (var (operationId, workerId) in keys.OrderBy(k => operations[k.OperationId].StationSequence)
                     .ThenBy(k => workers[k.WorkerId].EmployeeCode))
        {
            var operation = operations[operationId];
            var worker = workers[workerId];
            recordByKey.TryGetValue((operationId, workerId), out var record);
            var rate = record is null ? await _rates.FindEffectiveAsync(operationId, workDate, ct) : null;
            var canEdit = record is null ? newRowOpen : _clock.Now <= record.EditDeadline || _guard.IsManager;

            rows.Add(new OutputGridRowDto(operationId, operation.OperationCode, operation.OperationName, workerId,
                worker.EmployeeCode, worker.FullName, record?.OutputRecordId, record?.Quantity,
                record?.PieceRate.Rate ?? rate?.Rate, record is not null || rate is not null,
                record?.IsOutsideAssignment ?? !assigned.Contains((operationId, workerId)),
                record?.NoAttendanceWarning ?? false, canEdit, record?.EditDeadline, record?.RowVer,
                capByKey.TryGetValue((operationId, workerId), out var cap) ? cap.CapQty ?? 0 : 0,
                cap?.OutputQty ?? 0));
        }

        var received = await ReceivedAsync(team.Data, orderLineId, ct);
        return Result<OutputGridDto>.Success(new OutputGridDto(workDate, shift, orderLineId, team.Data, received, rows));
    }

    // ---------- Lưu ----------

    public async Task<Result<OutputSaveResultDto>> SaveAsync(SaveOutputDto dto, CancellationToken ct)
    {
        var shift = CodeText.ToCode(dto.ShiftCode);
        if (!ShiftCodes.All.Contains(shift)) return Invalid("Ca làm việc không hợp lệ.");
        if (dto.Rows.Count == 0) return Invalid(Messages.OutputRowsRequired);
        if (dto.Rows.Any(r => r.Quantity < 0)) return Invalid(Messages.OutputQtyInvalid);
        if (dto.Rows.Select(r => (r.OperationId, r.WorkerId)).Distinct().Count() != dto.Rows.Count)
            return Invalid(Messages.OutputDuplicateRow);
        if (dto.WorkDate > _clock.Today) return Invalid(Messages.OutputDateFuture);

        var team = _guard.ResolveSewingTeam(dto.SewingTeamId);
        if (!team.IsSuccess) return Fail.As<OutputSaveResultDto>(team);
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<OutputSaveResultDto>(_guard.PeriodLockedFailure(dto.WorkDate));

        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return Result<OutputSaveResultDto>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        var existing = await Records.Where(r => r.WorkDate == dto.WorkDate && r.ShiftCode == shift
            && r.SewingTeamId == team.Data && r.OrderLineId == dto.OrderLineId).ToListAsync(ct);
        var existingByKey = existing.ToDictionary(r => (r.OperationId, r.WorkerId));
        var newDeadline = ProductionGuard.NextWorkingDayEnd(dto.WorkDate);
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        // 1) Phân loại từng dòng + kiểm hạn sửa trước khi chạm DB.
        var inserts = new List<OutputLineInput>();
        var updates = new List<(OutputRecord Record, OutputLineInput Input)>();
        var deletes = new List<OutputRecord>();
        foreach (var row in dto.Rows)
        {
            existingByKey.TryGetValue((row.OperationId, row.WorkerId), out var record);
            if (record is null)
            {
                if (row.Quantity == 0) continue;
                var window = _guard.CheckEditWindow(newDeadline, reason);
                if (window is not null) return Fail.As<OutputSaveResultDto>(window);
                inserts.Add(row);
                continue;
            }
            if (row.Quantity == record.Quantity) continue;

            var editWindow = _guard.CheckEditWindow(record.EditDeadline, reason);
            if (editWindow is not null) return Fail.As<OutputSaveResultDto>(editWindow);
            if (row.Quantity == 0) deletes.Add(record);
            else updates.Add((record, row));
        }
        if (inserts.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            return Result<OutputSaveResultDto>.Success(new OutputSaveResultDto(0, 0, 0, []), Messages.OutputSaved);

        // 2) Công đoạn / công nhân / đơn giá / trần cho các dòng thêm hoặc đổi số lượng.
        var changing = inserts.Select(i => (i.OperationId, i.WorkerId, i.Quantity, Record: (OutputRecord?)null))
            .Concat(updates.Select(u => (u.Input.OperationId, u.Input.WorkerId, u.Input.Quantity, Record: (OutputRecord?)u.Record)))
            .ToList();
        var operationIds = changing.Select(c => c.OperationId).Distinct().ToList();
        var workerIds = changing.Select(c => c.WorkerId).Distinct().ToList();

        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => operationIds.Contains(o.OperationId)).ToDictionaryAsync(o => o.OperationId, ct);
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => workerIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, ct);

        foreach (var c in changing)
        {
            if (!operations.TryGetValue(c.OperationId, out var operation)) return Fail.As<OutputSaveResultDto>(Fail.NotFound());
            if (operation.Status != OperationStatuses.Active)
                return Result<OutputSaveResultDto>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive);
            if (operation.ProductId != line.ProductId)
                return Result<OutputSaveResultDto>.Failure(ErrorCodes.OperationProductMismatch, Messages.OperationProductMismatch);
            if (!workers.TryGetValue(c.WorkerId, out var worker) || worker.TeamId != team.Data || worker.Status != RecordStatus.Active)
                return Result<OutputSaveResultDto>.Failure(ErrorCodes.AssignmentWorkerInvalid, Messages.AssignmentWorkerInvalid);
        }

        var rates = new Dictionary<int, EffectivePieceRate>();
        foreach (var operationId in inserts.Select(i => i.OperationId).Distinct())
        {
            var rate = await _rates.FindEffectiveAsync(operationId, dto.WorkDate, ct);
            if (rate is null)   // MSG18: chưa có đơn giá duyệt thì không nhập sản lượng
                return Result<OutputSaveResultDto>.Failure(ErrorCodes.PieceRateNotApproved,
                    string.Format(Messages.PieceRateNotApproved, operations[operationId].OperationCode,
                        dto.WorkDate.ToString("dd/MM/yyyy")));
            rates[operationId] = rate;
        }

        var capFailure = await CheckCapsAsync(team.Data, dto.OrderLineId, changing, operations, ct);
        if (capFailure is not null) return Fail.As<OutputSaveResultDto>(capFailure);

        // 3) Cảnh báo MSG17 + cờ ngoài phân công cho các dòng mới.
        var attendedWorkerIds = await _uow.Repository<DailyTimesheet>().Query().AsNoTracking()
            .Where(t => t.WorkDate == dto.WorkDate && t.CheckIn != null && workerIds.Contains(t.EmployeeId))
            .Select(t => t.EmployeeId).Distinct().ToListAsync(ct);
        var assigned = (await _uow.Repository<OperationAssignment>().Query().AsNoTracking()
                .Where(a => a.SewingTeamId == team.Data && a.OrderId == line.OrderId && a.ValidTo == null)
                .Select(a => new { a.OperationId, a.WorkerId }).ToListAsync(ct))
            .Select(a => (a.OperationId, a.WorkerId)).ToHashSet();

        var warnings = new List<string>();
        foreach (var workerId in inserts.Select(i => i.WorkerId).Distinct().Where(id => !attendedWorkerIds.Contains(id)))
            warnings.Add(string.Format(Messages.OutputNoAttendanceWarning, workers[workerId].FullName,
                dto.WorkDate.ToString("dd/MM/yyyy")));

        // 4) Ghi cùng AuditLog trong một transaction.
        var created = new List<OutputRecord>();
        var result = await _uow.InTransactionAsync(async () =>
        {
            foreach (var row in inserts)
            {
                var record = new OutputRecord
                {
                    WorkDate = dto.WorkDate, ShiftCode = shift, SewingTeamId = team.Data, WorkerId = row.WorkerId,
                    OperationId = row.OperationId, OrderLineId = dto.OrderLineId, Quantity = row.Quantity,
                    PieceRateId = rates[row.OperationId].PieceRateId,   // đơn giá khóa theo ngày nhập
                    IsOutsideAssignment = !assigned.Contains((row.OperationId, row.WorkerId)),
                    NoAttendanceWarning = !attendedWorkerIds.Contains(row.WorkerId),
                    EditDeadline = newDeadline, Status = OutputStatuses.Saved, EnteredAt = _clock.Now,
                };
                await _uow.Repository<OutputRecord>().AddAsync(record);
                created.Add(record);
            }
            foreach (var (record, input) in updates)
            {
                if (input.RowVer is { Length: > 0 }) _uow.Repository<OutputRecord>().SetOriginalRowVersion(record, input.RowVer);
                _audit.Add(new AuditEntry(nameof(OutputRecord), record.OutputRecordId, AuditActions.Update,
                    new { record.Quantity }, new { input.Quantity }, reason));
                record.Quantity = input.Quantity;   // đơn giá đã khóa lúc nhập, không đổi theo giá mới
                record.UpdatedAt = _clock.Now;
            }
            foreach (var record in deletes)
            {
                _audit.Add(new AuditEntry(nameof(OutputRecord), record.OutputRecordId, AuditActions.Delete,
                    new { record.WorkDate, record.WorkerId, record.OperationId, record.Quantity, record.PieceRateId }, null, reason));
                _uow.Repository<OutputRecord>().Remove(record);
            }
            await _uow.SaveChangesAsync();   // lấy OutputRecordId của dòng mới trước khi ghi log Insert

            foreach (var record in created)
                _audit.Add(new AuditEntry(nameof(OutputRecord), record.OutputRecordId, AuditActions.Insert, null,
                    new { record.WorkDate, record.ShiftCode, record.WorkerId, record.OperationId, record.OrderLineId,
                          record.Quantity, record.PieceRateId }, reason));
            await _uow.SaveChangesAsync();
            return Result.Success();
        });
        if (!result.IsSuccess) return Fail.As<OutputSaveResultDto>(result);

        return Result<OutputSaveResultDto>.Success(
            new OutputSaveResultDto(inserts.Count, updates.Count, deletes.Count, warnings), Messages.OutputSaved);
    }

    // ---------- Trần (MSG16) ----------

    /// <summary>Trần theo công nhân × công đoạn: Σ sản lượng ≤ số lần đã giao và được công nhân xác nhận (WorkAllocation).
    /// Chưa được giao thì trần bằng 0 (trigger trg_OutputRecord_Check chặn cứng, THROW 50052).</summary>
    private async Task<Result?> CheckCapsAsync(int teamId, int orderLineId,
        List<(int OperationId, int WorkerId, int Quantity, OutputRecord? Record)> changing,
        Dictionary<int, Operation> operations, CancellationToken ct)
    {
        foreach (var row in changing)
        {
            var cap = await _uow.Repository<VwWorkerOutputCap>().Query().AsNoTracking()
                .Where(v => v.WorkerId == row.WorkerId && v.OrderLineId == orderLineId && v.OperationId == row.OperationId)
                .Select(v => (int?)v.CapQty).FirstOrDefaultAsync(ct) ?? 0;

            var recordId = row.Record?.OutputRecordId;
            var others = await Records.AsNoTracking()
                .Where(r => r.WorkerId == row.WorkerId && r.OrderLineId == orderLineId && r.OperationId == row.OperationId
                            && (recordId == null || r.OutputRecordId != recordId))
                .SumAsync(r => (int?)r.Quantity, ct) ?? 0;
            if (others + row.Quantity > cap)
                return Result.Failure(ErrorCodes.OutputExceedsCap, string.Format(Messages.OutputExceedsCap,
                    operations[row.OperationId].OperationCode, cap, Math.Max(0, cap - others)));
        }
        return null;
    }

    private async Task<int> ReceivedAsync(int teamId, int orderLineId, CancellationToken ct) =>
        (int)await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking().ReceivedSets(teamId, orderLineId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);

    private static Result<OutputSaveResultDto> Invalid(string message) =>
        Result<OutputSaveResultDto>.Failure(ErrorCodes.OutputInvalid, message, ResultKind.Validation);
}
