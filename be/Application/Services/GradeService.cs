using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC15 / UC16 / UC50 (KH-14): bậc thợ (danh mục GradeLevelCode: A, B, C, D... Admin thêm / sửa / ngừng dùng). Admin đặt tiền bậc (đổi tiền = thêm dòng mới theo ngày hiệu lực); tổ trưởng / tổ phó
/// đề xuất bậc cho công nhân tổ mình (số lỗi 30 ngày gần nhất do hệ thống tính); Admin / Quản đốc quyết định: áp dụng
/// (cập nhật <c>Employee.CurrentGradeCode</c>) hoặc không áp dụng có lý do. Dòng APPLIED là lịch sử bậc (GBR-22).
/// </summary>
public sealed class GradeService : IGradeService
{
    private const int DefectWindowDays = 30;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public GradeService(IUnitOfWork uow, ICurrentUser user, INotificationService notifier, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _user = user; _notifier = notifier; _audit = audit; _clock = clock;
    }

    private IQueryable<GradeEvaluation> Evaluations => _uow.Repository<GradeEvaluation>().Query();

    // ---------- Mức bậc ----------

    public async Task<Result<IReadOnlyList<GradeLevelDto>>> GetLevelsAsync(CancellationToken ct)
    {
        var rows = await _uow.Repository<GradeLevel>().Query().AsNoTracking().OrderBy(g => g.GradeCode).ThenByDescending(g => g.EffectiveDate).ToListAsync(ct);
        var today = _clock.Today;
        var currentIds = rows.Where(r => r.EffectiveDate <= today).GroupBy(r => r.GradeCode).Select(g => g.First().GradeLevelId).ToHashSet();
        return Result<IReadOnlyList<GradeLevelDto>>.Success(rows
            .Select(r => new GradeLevelDto(r.GradeLevelId, r.GradeCode, r.MonthlyAmount, r.EffectiveDate, currentIds.Contains(r.GradeLevelId)))
            .ToList());
    }

    // ---------- Danh mục bậc ----------

    private IQueryable<GradeLevelCode> Codes => _uow.Repository<GradeLevelCode>().Query();

    /// <summary>Bậc phải có trong danh mục và đang dùng mới gán được cho nhân viên / đề xuất.</summary>
    private Task<bool> IsAssignableAsync(string code, CancellationToken ct) =>
        Codes.AnyAsync(c => c.GradeCode == code && c.Status == GradeCodes.Active, ct);

    private async Task<bool> IsInUseAsync(string code, CancellationToken ct) =>
        await _uow.Repository<GradeLevel>().Query().AnyAsync(g => g.GradeCode == code, ct)
        || await _uow.Repository<Employee>().Query().AnyAsync(e => e.CurrentGradeCode == code, ct)
        || await Evaluations.AnyAsync(e => e.ProposedGradeCode == code || e.CurrentGradeCode == code, ct);

    public async Task<Result<IReadOnlyList<GradeCodeDto>>> GetCodesAsync(bool activeOnly, CancellationToken ct)
    {
        var rows = await Codes.AsNoTracking().Where(c => !activeOnly || c.Status == GradeCodes.Active)
            .OrderBy(c => c.GradeCode).ToListAsync(ct);
        var list = new List<GradeCodeDto>(rows.Count);
        foreach (var c in rows) list.Add(ToDto(c, activeOnly ? false : await IsInUseAsync(c.GradeCode, ct)));
        return Result<IReadOnlyList<GradeCodeDto>>.Success(list);
    }

    public async Task<Result<GradeCodeDto>> CreateCodeAsync(SaveGradeCodeDto dto, CancellationToken ct)
    {
        var code = dto.GradeCode.Trim().ToUpperInvariant();
        if (!GradeCodes.IsWellFormed(code))
            return Result<GradeCodeDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeMalformed, ResultKind.Validation);
        if (await Codes.AnyAsync(c => c.GradeCode == code, ct))
            return Result<GradeCodeDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeDuplicate);

        var row = new GradeLevelCode { GradeCode = code, GradeName = dto.GradeName.Trim(), Status = GradeCodes.Active, CreatedAt = _clock.Now };
        await _uow.Repository<GradeLevelCode>().AddAsync(row);
        _audit.Add(new AuditEntry(nameof(GradeLevelCode), 0, AuditActions.Insert, null, new { row.GradeCode, row.GradeName }));
        await _uow.SaveChangesAsync();
        return Result<GradeCodeDto>.Success(ToDto(row, false), Messages.Saved);
    }

    public async Task<Result<GradeCodeDto>> UpdateCodeAsync(string gradeCode, UpdateGradeCodeDto dto, CancellationToken ct)
    {
        var code = gradeCode.Trim().ToUpperInvariant();
        var row = await Codes.FirstOrDefaultAsync(c => c.GradeCode == code, ct);
        if (row is null) return Fail.NotFound<GradeCodeDto>();

        var before = new { row.GradeName, row.Status };
        row.GradeName = dto.GradeName.Trim();
        row.Status = dto.Status == "Inactive" ? GradeCodes.Inactive : GradeCodes.Active;
        _audit.Add(new AuditEntry(nameof(GradeLevelCode), 0, AuditActions.Update, before, new { row.GradeName, row.Status }, code));
        await _uow.SaveChangesAsync();
        return Result<GradeCodeDto>.Success(ToDto(row, await IsInUseAsync(code, ct)), Messages.Saved);
    }

    public async Task<Result> DeleteCodeAsync(string gradeCode, CancellationToken ct)
    {
        var code = gradeCode.Trim().ToUpperInvariant();
        var row = await Codes.FirstOrDefaultAsync(c => c.GradeCode == code, ct);
        if (row is null) return Fail.NotFound();
        if (await IsInUseAsync(code, ct)) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInUse);

        _uow.Repository<GradeLevelCode>().Remove(row);
        _audit.Add(new AuditEntry(nameof(GradeLevelCode), 0, AuditActions.Delete, new { row.GradeCode, row.GradeName }, null, code));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    private static GradeCodeDto ToDto(GradeLevelCode c, bool inUse) =>
        new(c.GradeCode, c.GradeName, CodeText.ToPascal(c.Status), inUse);

    public async Task<Result<GradeLevelDto>> UpdateLevelAsync(int gradeLevelId, UpdateGradeLevelDto dto, CancellationToken ct)
    {
        var level = await _uow.Repository<GradeLevel>().Query().FirstOrDefaultAsync(g => g.GradeLevelId == gradeLevelId, ct);
        if (level is null) return Fail.NotFound<GradeLevelDto>();
        if (level.EffectiveDate <= _clock.Today) return Result<GradeLevelDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeLevelLocked);
        if (dto.EffectiveDate <= _clock.Today)
            return Result<GradeLevelDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeLevelEffectiveInPast, ResultKind.Validation);
        if (await _uow.Repository<GradeLevel>().Query().AnyAsync(g => g.GradeCode == level.GradeCode && g.EffectiveDate == dto.EffectiveDate && g.GradeLevelId != gradeLevelId, ct))
            return Result<GradeLevelDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeLevelDuplicate);

        var before = new { level.MonthlyAmount, level.EffectiveDate };
        level.MonthlyAmount = dto.MonthlyAmount;
        level.EffectiveDate = dto.EffectiveDate;
        _audit.Add(new AuditEntry(nameof(GradeLevel), level.GradeLevelId, AuditActions.Update, before, new { level.MonthlyAmount, level.EffectiveDate }));
        await _uow.SaveChangesAsync();
        return Result<GradeLevelDto>.Success(new GradeLevelDto(level.GradeLevelId, level.GradeCode, level.MonthlyAmount, level.EffectiveDate, false), Messages.Saved);
    }

    public async Task<Result> DeleteLevelAsync(int gradeLevelId, CancellationToken ct)
    {
        var level = await _uow.Repository<GradeLevel>().Query().FirstOrDefaultAsync(g => g.GradeLevelId == gradeLevelId, ct);
        if (level is null) return Fail.NotFound();
        if (level.EffectiveDate <= _clock.Today) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeLevelLocked);

        _uow.Repository<GradeLevel>().Remove(level);
        _audit.Add(new AuditEntry(nameof(GradeLevel), level.GradeLevelId, AuditActions.Delete, new { level.GradeCode, level.MonthlyAmount, level.EffectiveDate }, null));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    // ---------- Mức tiền bậc (thêm) ----------

    public async Task<Result<GradeLevelDto>> AddLevelAsync(SaveGradeLevelDto dto, CancellationToken ct)
    {
        var code = dto.GradeCode.Trim().ToUpperInvariant();
        if (!await Codes.AnyAsync(c => c.GradeCode == code, ct))
            return Result<GradeLevelDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInvalid, ResultKind.Validation);
        if (await _uow.Repository<GradeLevel>().Query().AnyAsync(g => g.GradeCode == code && g.EffectiveDate == dto.EffectiveDate, ct))
            return Result<GradeLevelDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeLevelDuplicate);

        var level = new GradeLevel { GradeCode = code, MonthlyAmount = dto.MonthlyAmount, EffectiveDate = dto.EffectiveDate, CreatedAt = _clock.Now };
        await _uow.Repository<GradeLevel>().AddAsync(level);
        await _uow.SaveChangesAsync();
        _audit.Add(new AuditEntry(nameof(GradeLevel), level.GradeLevelId, AuditActions.Insert, null, new { level.GradeCode, level.MonthlyAmount, level.EffectiveDate }));
        await _uow.SaveChangesAsync();
        return Result<GradeLevelDto>.Success(new GradeLevelDto(level.GradeLevelId, code, level.MonthlyAmount, level.EffectiveDate,
            level.EffectiveDate <= _clock.Today), Messages.Saved);
    }

    // ---------- Đề xuất ----------

    public async Task<Result<IReadOnlyList<GradeWorkerDto>>> GetTeamWorkersAsync(int teamId, CancellationToken ct)
    {
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active && e.Role.RoleCode == RoleCodes.Worker)
            .OrderBy(e => e.EmployeeCode).Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName, e.CurrentGradeCode }).ToListAsync(ct);
        var ids = workers.Select(w => w.EmployeeId).ToList();
        var defects = await DefectCountsAsync(ids, ct);
        var pending = await Evaluations.Where(e => ids.Contains(e.WorkerId) && e.Status == GradeEvaluationStatuses.Submitted)
            .Select(e => e.WorkerId).Distinct().ToListAsync(ct);
        return Result<IReadOnlyList<GradeWorkerDto>>.Success(workers
            .Select(w => new GradeWorkerDto(w.EmployeeId, w.EmployeeCode, w.FullName, w.CurrentGradeCode, defects.GetValueOrDefault(w.EmployeeId), pending.Contains(w.EmployeeId)))
            .ToList());
    }

    public async Task<Result<IReadOnlyList<GradeWorkerDto>>> GetAllWorkersAsync(CancellationToken ct)
    {
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.Status == RecordStatus.Active && e.Role.RoleCode == RoleCodes.Worker)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName, e.CurrentGradeCode, e.TeamId, TeamName = e.Team == null ? null : e.Team.TeamName })
            .ToListAsync(ct);
        var ids = workers.Select(w => w.EmployeeId).ToList();
        var defects = await DefectCountsAsync(ids, ct);
        var pending = await Evaluations.Where(e => ids.Contains(e.WorkerId) && e.Status == GradeEvaluationStatuses.Submitted)
            .Select(e => e.WorkerId).Distinct().ToListAsync(ct);
        return Result<IReadOnlyList<GradeWorkerDto>>.Success(workers
            .Select(w => new GradeWorkerDto(w.EmployeeId, w.EmployeeCode, w.FullName, w.CurrentGradeCode, defects.GetValueOrDefault(w.EmployeeId),
                pending.Contains(w.EmployeeId), w.TeamName, w.TeamId))
            .ToList());
    }

    public async Task<Result<PagedResult<GradeEvaluationDto>>> GetPagedAsync(GradeEvaluationQuery query, int? teamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Evaluations.AsNoTracking()
            .Where(e => teamId == null || e.TeamId == teamId)
            .Where(e => string.IsNullOrEmpty(query.Status) || e.Status == query.Status)
            .Where(e => query.WorkerId == null || e.WorkerId == query.WorkerId)
            .Where(e => string.IsNullOrEmpty(keyword) || e.Worker.FullName.Contains(keyword) || e.Worker.EmployeeCode.Contains(keyword))
            .OrderByDescending(e => e.GradeEvaluationId)
            .Select(e => new GradeEvaluationDto(e.GradeEvaluationId, e.WorkerId, e.Worker.EmployeeCode, e.Worker.FullName, e.TeamId,
                e.Team == null ? null : e.Team.TeamName, e.ProposedByNavigation.FullName, e.CurrentGradeCode, e.ProposedGradeCode,
                e.Reason, e.RecentDefectCount, e.EvaluationDate, e.Status, e.EffectiveDate, e.ConsideredAt, e.DecisionNote, e.CreatedAt))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<GradeEvaluationDto>>.Success(page);
    }

    public async Task<Result<GradeEvaluationDto>> ProposeAsync(ProposeGradeDto dto, int teamId, CancellationToken ct)
    {
        var code = dto.ProposedGradeCode.Trim().ToUpperInvariant();
        if (!await IsAssignableAsync(code, ct))
            return Result<GradeEvaluationDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInvalid, ResultKind.Validation);

        var worker = await _uow.Repository<Employee>().Query().AsNoTracking().Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeId == dto.WorkerId, ct);
        if (worker is null || worker.Status != RecordStatus.Active || worker.Role.RoleCode != RoleCodes.Worker || worker.TeamId != teamId)
            return Result<GradeEvaluationDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeWorkerNotInTeam, ResultKind.Forbidden);
        if (worker.CurrentGradeCode == code)
            return Result<GradeEvaluationDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeSameAsCurrent);
        if (await Evaluations.AnyAsync(e => e.WorkerId == worker.EmployeeId && e.Status == GradeEvaluationStatuses.Submitted, ct))
            return Result<GradeEvaluationDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradePendingExists);
        if (await Evaluations.AnyAsync(e => e.WorkerId == worker.EmployeeId && e.ProposedBy == _user.EmployeeId && e.EvaluationDate == _clock.Today, ct))
            return Result<GradeEvaluationDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeProposedToday);

        var defects = (await DefectCountsAsync(new[] { worker.EmployeeId }, ct)).GetValueOrDefault(worker.EmployeeId);
        var evaluation = new GradeEvaluation
        {
            WorkerId = worker.EmployeeId, ProposedBy = _user.EmployeeId, TeamId = teamId, CurrentGradeCode = worker.CurrentGradeCode,
            ProposedGradeCode = code, Reason = dto.Reason.Trim(), RecentDefectCount = defects, EvaluationDate = _clock.Today,
            Status = GradeEvaluationStatuses.Submitted, CreatedAt = _clock.Now,
        };
        await _uow.Repository<GradeEvaluation>().AddAsync(evaluation);
        await _notifier.NotifyRoleAsync(RoleCodes.Admin, new NotificationMessage(NotificationSources.System, "Có đề xuất bậc thợ chờ duyệt",
            $"Tổ trưởng đề xuất bậc {code} cho {worker.FullName}.", null), ct);
        await _uow.SaveChangesAsync();
        return await GetOneAsync(evaluation.GradeEvaluationId, Messages.GradeProposed, ct);
    }

    // ---------- Quyết định ----------

    public Task<Result> ApplyAsync(int evaluationId, ApplyGradeDto dto, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var e = await Evaluations.Include(x => x.Worker).FirstOrDefaultAsync(x => x.GradeEvaluationId == evaluationId, ct);
            if (e is null) return Fail.NotFound();
            if (e.Status != GradeEvaluationStatuses.Submitted) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeAlreadyDecided);

            var effective = dto.EffectiveDate ?? _clock.Today;
            e.Status = GradeEvaluationStatuses.Applied;
            e.EffectiveDate = effective;
            e.ConsideredAt = _clock.Now;
            e.DecisionNote = string.IsNullOrWhiteSpace(dto.DecisionNote) ? null : dto.DecisionNote.Trim();
            e.Worker.CurrentGradeCode = e.ProposedGradeCode;
            _audit.Add(new AuditEntry(nameof(Employee), e.WorkerId, AuditActions.Update, new { CurrentGradeCode = e.CurrentGradeCode },
                new { CurrentGradeCode = e.ProposedGradeCode }, e.DecisionNote ?? "Áp dụng bậc thợ"));
            _notifier.NotifyEmployees(new[] { e.WorkerId, e.ProposedBy }, new NotificationMessage(NotificationSources.System, "Bậc thợ đã được áp dụng",
                $"{e.Worker.FullName}: bậc {e.ProposedGradeCode} có hiệu lực từ {effective:dd/MM/yyyy}.", null));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.GradeApplied);
        });

    public Task<Result> SetGradeAsync(SetGradeDto dto, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var code = dto.GradeCode.Trim().ToUpperInvariant();
            if (!await IsAssignableAsync(code, ct)) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInvalid, ResultKind.Validation);

            var worker = await _uow.Repository<Employee>().Query().Include(e => e.Role).FirstOrDefaultAsync(e => e.EmployeeId == dto.WorkerId, ct);
            if (worker is null || worker.Status != RecordStatus.Active || worker.Role.RoleCode != RoleCodes.Worker)
                return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeWorkerInvalid, ResultKind.Validation);
            if (worker.CurrentGradeCode == code) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeSameAsCurrent);
            // Ràng buộc UQ_GradeEval (công nhân, người đặt, ngày): chặn sớm bằng thông báo rõ thay vì lỗi cơ sở dữ liệu.
            if (await Evaluations.AnyAsync(e => e.WorkerId == worker.EmployeeId && e.ProposedBy == _user.EmployeeId && e.EvaluationDate == _clock.Today, ct))
                return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeSetToday);

            var now = _clock.Now;
            var reason = dto.Reason.Trim();
            var effective = dto.EffectiveDate ?? _clock.Today;

            // Đề xuất đang chờ của công nhân này không còn ý nghĩa: đóng lại có ghi chú (GBR-22: không sửa dòng đã quyết định).
            var pending = await Evaluations.Where(e => e.WorkerId == worker.EmployeeId && e.Status == GradeEvaluationStatuses.Submitted).ToListAsync(ct);
            foreach (var p in pending)
            {
                p.Status = GradeEvaluationStatuses.NotApplied;
                p.ConsideredAt = now;
                p.DecisionNote = Messages.GradeSupersededNote;
            }

            var defects = (await DefectCountsAsync(new[] { worker.EmployeeId }, ct)).GetValueOrDefault(worker.EmployeeId);
            await _uow.Repository<GradeEvaluation>().AddAsync(new GradeEvaluation
            {
                WorkerId = worker.EmployeeId, ProposedBy = _user.EmployeeId, TeamId = worker.TeamId, CurrentGradeCode = worker.CurrentGradeCode,
                ProposedGradeCode = code, Reason = reason, RecentDefectCount = defects, EvaluationDate = _clock.Today,
                Status = GradeEvaluationStatuses.Applied, EffectiveDate = effective, ConsideredAt = now, DecisionNote = reason, CreatedAt = now,
            });
            _audit.Add(new AuditEntry(nameof(Employee), worker.EmployeeId, AuditActions.Update, new { CurrentGradeCode = worker.CurrentGradeCode },
                new { CurrentGradeCode = code }, reason));
            var oldGrade = worker.CurrentGradeCode;
            worker.CurrentGradeCode = code;
            _notifier.NotifyEmployees(pending.Select(p => p.ProposedBy).Append(worker.EmployeeId).Distinct().ToArray(),
                new NotificationMessage(NotificationSources.System, "Bậc thợ đã được cập nhật",
                    $"{worker.FullName}: bậc {oldGrade ?? "—"} → {code}, có hiệu lực từ {effective:dd/MM/yyyy}.", null));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.GradeSet);
        });

    public async Task<Result> RejectAsync(int evaluationId, ReasonDto dto, CancellationToken ct)
    {
        var e = await Evaluations.Include(x => x.Worker).FirstOrDefaultAsync(x => x.GradeEvaluationId == evaluationId, ct);
        if (e is null) return Fail.NotFound();
        if (e.Status != GradeEvaluationStatuses.Submitted) return Result.Failure(ErrorCodes.GradeInvalid, Messages.GradeAlreadyDecided);

        e.Status = GradeEvaluationStatuses.NotApplied;
        e.ConsideredAt = _clock.Now;
        e.DecisionNote = dto.Reason.Trim();
        _notifier.NotifyEmployees(new[] { e.ProposedBy }, new NotificationMessage(NotificationSources.System, "Đề xuất bậc thợ không được áp dụng",
            $"{e.Worker.FullName}: {e.DecisionNote}", null));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.GradeNotApplied);
    }

    // ---------- Nội bộ ----------

    /// <summary>Số phiếu lỗi (Rework + Scrap, không tính đã hủy) do công nhân gây ra trong 30 ngày gần nhất.</summary>
    private async Task<Dictionary<int, int>> DefectCountsAsync(IReadOnlyCollection<int> workerIds, CancellationToken ct)
    {
        var from = _clock.Today.AddDays(-DefectWindowDays);
        return await _uow.Repository<DefectRecord>().Query()
            .Where(d => workerIds.Contains(d.ResponsibleWorkerId) && d.Status != DefectStatuses.Cancelled && d.WorkDate >= from)
            .GroupBy(d => d.ResponsibleWorkerId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private async Task<Result<GradeEvaluationDto>> GetOneAsync(int id, string message, CancellationToken ct)
    {
        var dto = (await Evaluations.AsNoTracking().Where(e => e.GradeEvaluationId == id)
            .Select(e => new GradeEvaluationDto(e.GradeEvaluationId, e.WorkerId, e.Worker.EmployeeCode, e.Worker.FullName, e.TeamId,
                e.Team == null ? null : e.Team.TeamName, e.ProposedByNavigation.FullName, e.CurrentGradeCode, e.ProposedGradeCode,
                e.Reason, e.RecentDefectCount, e.EvaluationDate, e.Status, e.EffectiveDate, e.ConsideredAt, e.DecisionNote, e.CreatedAt))
            .FirstAsync(ct));
        return Result<GradeEvaluationDto>.Success(dto, message);
    }
}
