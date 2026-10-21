using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// KI-08 (UC56): tổ trưởng / tổ phó may ghi hàng hỏng theo QA/QC báo. REWORK trả lại công nhân sửa, không trừ sản
/// lượng; SCRAP trừ khỏi hàng trên chuyền và sản lượng được trả của công nhân gây lỗi (khóa <c>ScrapUnitRate</c> bằng
/// đơn giá duyệt tại ngày làm việc). Bắt buộc công đoạn, công nhân gây lỗi, lý do (OTHER phải ghi chú); báo cho công
/// nhân; công nhân phản hồi nếu ghi sai. Hạn sửa như sản lượng (GBR-09). Không sinh đơn cắt bù (KI-09 — DEV-KI-08-1).
/// </summary>
public sealed class DefectService : IDefectService
{
    private readonly IUnitOfWork _uow;
    private readonly ProductionGuard _guard;
    private readonly IPieceRateService _rates;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public DefectService(IUnitOfWork uow, ProductionGuard guard, IPieceRateService rates, INotificationService notifier,
        IAuditLogService audit, ICurrentUser user, IClock clock)
    {
        _uow = uow; _guard = guard; _rates = rates; _notifier = notifier; _audit = audit; _user = user; _clock = clock;
    }

    private IQueryable<DefectRecord> Defects => _uow.Repository<DefectRecord>().Query();

    // ---------- Xem ----------

    public async Task<Result<PagedResult<DefectDto>>> ListForTeamAsync(DefectQuery query, CancellationToken ct)
    {
        int? teamId = null;
        if (!_guard.IsManager)
        {
            var team = _guard.ResolveSewingTeam(null);
            if (!team.IsSuccess) return Fail.As<PagedResult<DefectDto>>(team);
            teamId = team.Data;
        }

        var keyword = query.Keyword?.Trim();
        var page = await Project(Defects.AsNoTracking()
                .Where(d => teamId == null || d.SewingTeamId == teamId)
                .Where(d => query.WorkDate == null || d.WorkDate == query.WorkDate)
                .Where(d => query.From == null || d.WorkDate >= query.From)
                .Where(d => query.To == null || d.WorkDate <= query.To)
                .Where(d => string.IsNullOrEmpty(keyword) || d.DefectCode.Contains(keyword)
                            || d.ResponsibleWorker.FullName.Contains(keyword))
                .OrderByDescending(d => d.WorkDate).ThenByDescending(d => d.DefectRecordId))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<DefectDto>>.Success(page with { Items = page.Items.Select(Finish).ToList() });
    }

    public async Task<Result<IReadOnlyList<DefectDto>>> ListMineAsync(int workerId, DefectQuery query, CancellationToken ct)
    {
        var items = await Project(Defects.AsNoTracking()
                .Where(d => d.ResponsibleWorkerId == workerId && d.Status != DefectStatuses.Cancelled)
                .Where(d => query.From == null || d.WorkDate >= query.From)
                .Where(d => query.To == null || d.WorkDate <= query.To)
                .OrderByDescending(d => d.WorkDate).ThenByDescending(d => d.DefectRecordId))
            .Take(Paging.MaxPageSize)
            .ToListAsync(ct);
        return Result<IReadOnlyList<DefectDto>>.Success(items.Select(Finish).ToList());
    }

    // ---------- Ghi ----------

    public async Task<Result<int>> CreateAsync(CreateDefectDto dto, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);   // Quản đốc không ghi phiếu hộ: phiếu do tổ trưởng của tổ lập
        if (!team.IsSuccess) return Fail.As<int>(team);

        var type = CodeText.ToCode(dto.DefectType);
        if (type is not (DefectTypes.Rework or DefectTypes.Scrap))
            return Invalid(Messages.DefectTypeInvalid);
        var reasonCode = CodeText.ToCode(dto.ReasonCode);
        if (!DefectReasons.All.Contains(reasonCode)) return Invalid(Messages.DefectReasonInvalid);
        var note = string.IsNullOrWhiteSpace(dto.ReasonNote) ? null : dto.ReasonNote.Trim();
        if (reasonCode == DefectReasons.Other && note is null) return Invalid(Messages.DefectNoteRequired);   // CK_DR_Other
        if (dto.Quantity <= 0) return Invalid(Messages.OutputQtyInvalid);
        if (dto.WorkDate > _clock.Today) return Invalid(Messages.OutputDateFuture);

        string? shift = null;
        if (!string.IsNullOrWhiteSpace(dto.ShiftCode))
        {
            shift = CodeText.ToCode(dto.ShiftCode);
            if (!ShiftCodes.All.Contains(shift)) return Invalid("Ca làm việc không hợp lệ.");
        }
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<int>(_guard.PeriodLockedFailure(dto.WorkDate));

        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        var operation = await _uow.Repository<Operation>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperationId == dto.OperationId, ct);
        if (operation is null) return Fail.NotFound<int>();
        if (operation.Status != OperationStatuses.Active)
            return Result<int>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive);
        if (operation.ProductId != line.ProductId)
            return Result<int>.Failure(ErrorCodes.OperationProductMismatch, Messages.OperationProductMismatch);

        var worker = await _uow.Repository<Employee>().Query().AsNoTracking().FirstOrDefaultAsync(
            e => e.EmployeeId == dto.ResponsibleWorkerId && e.TeamId == team.Data && e.Status == RecordStatus.Active, ct);
        if (worker is null)
            return Result<int>.Failure(ErrorCodes.AssignmentWorkerInvalid, Messages.AssignmentWorkerInvalid);

        decimal? scrapRate = null;
        if (type == DefectTypes.Scrap)
        {
            var rate = await _rates.FindEffectiveAsync(dto.OperationId, dto.WorkDate, ct);
            if (rate is null)   // MSG18: Scrap trừ lương theo đơn giá duyệt nên phải có đơn giá
                return Result<int>.Failure(ErrorCodes.PieceRateNotApproved, string.Format(Messages.PieceRateNotApproved,
                    operation.OperationCode, dto.WorkDate.ToString("dd/MM/yyyy")));
            scrapRate = rate.Rate;   // khóa tại thời điểm ghi (CK_DR_Rate)
        }

        var teamCode = await _uow.Repository<Team>().Query().AsNoTracking()
            .Where(t => t.TeamId == team.Data).Select(t => t.TeamCode).FirstAsync(ct);
        var now = _clock.Now;
        var defect = new DefectRecord
        {
            DefectCode = await NextCodeAsync(teamCode, dto.WorkDate, ct), WorkDate = dto.WorkDate, ShiftCode = shift,
            SewingTeamId = team.Data, OrderLineId = dto.OrderLineId, OperationId = dto.OperationId,
            ResponsibleWorkerId = dto.ResponsibleWorkerId, DefectType = type, Quantity = dto.Quantity,
            DefectReasonCode = reasonCode, ReasonNote = note, ScrapUnitRate = scrapRate, WorkerNotifiedAt = now,
            EditDeadline = ProductionGuard.NextWorkingDayEnd(dto.WorkDate), Status = DefectStatuses.Recorded,
            RecordedBy = _user.EmployeeId, RecordedAt = now,
        };

        return await _uow.InTransactionAsync(async () =>   // log cần DefectRecordId nên lưu trước
        {
            await _uow.Repository<DefectRecord>().AddAsync(defect);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(DefectRecord), defect.DefectRecordId, AuditActions.Insert, null, Snapshot(defect)));
            _notifier.NotifyEmployees([worker.EmployeeId], new NotificationMessage(NotificationSources.Production,
                Messages.NotifyDefectTitle,
                string.Format(Messages.NotifyDefectBody, dto.WorkDate.ToString("dd/MM/yyyy"), dto.Quantity,
                    type == DefectTypes.Scrap ? "hỏng không sửa được (Scrap)" : "cần sửa lại (Rework)", operation.OperationName),
                null, nameof(DefectRecord), defect.DefectRecordId));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(defect.DefectRecordId, Messages.DefectSaved);
        });
    }

    public async Task<Result> CancelAsync(int defectRecordId, CancelDefectDto dto, CancellationToken ct)
    {
        var defect = await Defects.FirstOrDefaultAsync(d => d.DefectRecordId == defectRecordId, ct);
        if (defect is null || !CanSeeTeam(defect.SewingTeamId)) return Fail.NotFound();
        if (defect.Status != DefectStatuses.Recorded)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectInvalidStatus);

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var window = _guard.CheckEditWindow(defect.EditDeadline, reason);
        if (window is not null) return window;
        if (await _guard.IsPeriodLockedAsync(defect.WorkDate, ct)) return _guard.PeriodLockedFailure(defect.WorkDate);

        _uow.Repository<DefectRecord>().SetOriginalRowVersion(defect, dto.RowVer);
        defect.Status = DefectStatuses.Cancelled;
        defect.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(DefectRecord), defectRecordId, AuditActions.Update,
            new { Status = DefectStatuses.Recorded }, new { defect.Status }, reason));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.DefectCancelled);
    }

    public async Task<Result> ObjectAsync(int workerId, int defectRecordId, ObjectDefectDto dto, CancellationToken ct)
    {
        var objection = dto.Objection?.Trim();
        if (string.IsNullOrEmpty(objection))
            return Result.Failure(ErrorCodes.DefectInvalid, Messages.DefectObjectionRequired, ResultKind.Validation);

        // Công nhân chỉ phản hồi phiếu của chính mình.
        var defect = await Defects.FirstOrDefaultAsync(d => d.DefectRecordId == defectRecordId && d.ResponsibleWorkerId == workerId, ct);
        if (defect is null) return Fail.NotFound();
        if (defect.Status != DefectStatuses.Recorded)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectInvalidStatus);

        var workerName = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.EmployeeId == workerId).Select(e => e.FullName).FirstAsync(ct);
        var leaderIds = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.TeamId == defect.SewingTeamId && e.Status == RecordStatus.Active
                        && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .Select(e => e.EmployeeId).ToListAsync(ct);

        _uow.Repository<DefectRecord>().SetOriginalRowVersion(defect, dto.RowVer);
        defect.WorkerObjection = objection;
        defect.ObjectedAt = _clock.Now;
        defect.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(DefectRecord), defectRecordId, AuditActions.Update, null,
            new { defect.WorkerObjection }, objection));
        if (leaderIds.Count > 0)
            _notifier.NotifyEmployees(leaderIds, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyDefectObjectionTitle,
                string.Format(Messages.NotifyDefectObjectionBody, workerName, defect.DefectCode, objection),
                null, nameof(DefectRecord), defectRecordId));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.DefectObjected);
    }

    // ---------- Dùng chung ----------

    /// <summary>Tổ trưởng / tổ phó chỉ thao tác phiếu của tổ mình; Quản đốc thao tác mọi tổ.</summary>
    private bool CanSeeTeam(int teamId) => _guard.IsManager || _user.TeamId == teamId;

    /// <summary>Mã phiếu: &lt;mã tổ&gt;-&lt;yyMMdd&gt;-&lt;NNN&gt; (UQ_DefectRecord_Code); hai người ghi cùng lúc trùng
    /// mã thì unique báo lỗi → middleware trả 409.</summary>
    private async Task<string> NextCodeAsync(string teamCode, DateOnly workDate, CancellationToken ct)
    {
        var prefix = $"{teamCode}-{workDate:yyMMdd}-";
        var codes = await Defects.AsNoTracking().Where(d => d.DefectCode.StartsWith(prefix))
            .Select(d => d.DefectCode).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:000}";
    }

    private static IQueryable<DefectDto> Project(IQueryable<DefectRecord> source) => source.Select(d => new DefectDto(
        d.DefectRecordId, d.DefectCode, d.WorkDate, d.ShiftCode, d.SewingTeamId, d.OrderLineId, d.OrderLine.Order.OrderCode,
        d.OrderLine.Product.ProductCode, d.OperationId, d.Operation.OperationCode, d.Operation.OperationName,
        d.ResponsibleWorkerId, d.ResponsibleWorker.FullName, d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote,
        d.ScrapUnitRate, d.WorkerNotifiedAt, d.WorkerObjection, d.ObjectedAt, d.EditDeadline, d.Status, d.RecordedAt,
        false, d.RowVer));

    /// <summary>Đổi mã DB sang PascalCase và tính CanEdit (còn hiệu lực và còn hạn, hoặc là Quản đốc).</summary>
    private DefectDto Finish(DefectDto d) => d with
    {
        DefectType = CodeText.ToPascal(d.DefectType), DefectReasonCode = CodeText.ToPascal(d.DefectReasonCode),
        Status = CodeText.ToPascal(d.Status),
        CanEdit = d.Status == DefectStatuses.Recorded && (_clock.Now <= d.EditDeadline || _guard.IsManager),
    };

    private static object Snapshot(DefectRecord d) => new
    {
        d.DefectCode, d.WorkDate, d.ShiftCode, d.SewingTeamId, d.OrderLineId, d.OperationId, d.ResponsibleWorkerId,
        d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote, d.ScrapUnitRate,
    };

    private static Result<int> Invalid(string message) =>
        Result<int>.Failure(ErrorCodes.DefectInvalid, message, ResultKind.Validation);
}
