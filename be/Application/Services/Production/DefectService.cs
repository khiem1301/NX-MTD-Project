using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// KI-08 (UC56): tổ trưởng / tổ phó may ghi hàng hỏng theo QA/QC báo. REWORK trả lại công nhân sửa, không trừ sản
/// lượng; SCRAP trừ khỏi hàng trên chuyền và sản lượng được trả của công nhân gây lỗi (khóa <c>PieceRateId</c> bằng
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
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : CodeText.ToCode(query.Status);
        var type = string.IsNullOrWhiteSpace(query.DefectType) ? null : CodeText.ToCode(query.DefectType);
        var objectionPending = query.Objection == DefectObjectionFilters.Pending;
        var objectionResolved = query.Objection == DefectObjectionFilters.Resolved;
        var objectionNone = query.Objection == DefectObjectionFilters.None;
        var page = await Project(Defects.AsNoTracking()
                .Where(d => teamId == null || d.SewingTeamId == teamId)
                // Quản đốc lọc theo tổ; tổ trưởng đã bị giới hạn ở tổ mình ở trên.
                .Where(d => teamId != null || query.SewingTeamId == null || d.SewingTeamId == query.SewingTeamId)
                .Where(d => status == null || d.Status == status)
                .Where(d => type == null || d.DefectType == type)
                // Chờ quyết định = công nhân phản đối mà phiếu vẫn ở trạng thái đã ghi; đã quyết định = đã chấp nhận hoặc hủy.
                .Where(d => !objectionPending || (d.WorkerObjection != null && d.Status == DefectStatuses.Recorded))
                .Where(d => !objectionResolved || (d.WorkerObjection != null && d.Status != DefectStatuses.Recorded))
                .Where(d => !objectionNone || d.WorkerObjection == null)
                .Where(d => query.WorkDate == null || d.WorkDate == query.WorkDate)
                .Where(d => query.From == null || d.WorkDate >= query.From)
                .Where(d => query.To == null || d.WorkDate <= query.To)
                // Tìm theo mã lỗi, tên công nhân, mã hàng, tổ (tên / mã) và mã đơn.
                .Where(d => string.IsNullOrEmpty(keyword) || d.DefectCode.Contains(keyword)
                            || d.ResponsibleWorker.FullName.Contains(keyword)
                            || d.OrderLine.ProductVariant.Product.ProductCode.Contains(keyword)
                            || d.SewingTeam.TeamName.Contains(keyword) || d.SewingTeam.TeamCode.Contains(keyword)
                            || d.OrderLine.Order.OrderCode.Contains(keyword))
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

        var input = ValidateInput(dto);
        if (input.Failure is not null) return input.Failure;
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<int>(_guard.PeriodLockedFailure(dto.WorkDate));

        var refs = await ValidateReferencesAsync(dto, team.Data, ct);
        if (refs.Failure is not null) return refs.Failure;
        var (operation, worker) = (refs.Operation!, refs.Worker!);

        int? scrapRateId = null;
        if (input.Type == DefectTypes.Scrap)
        {
            var rate = await _rates.FindEffectiveAsync(dto.OperationId, dto.WorkDate, ct);
            if (rate is null)   // MSG18: Scrap trừ lương theo đơn giá duyệt nên phải có đơn giá
                return Result<int>.Failure(ErrorCodes.PieceRateNotApproved, string.Format(Messages.PieceRateNotApproved,
                    operation.OperationCode, dto.WorkDate.ToString("dd/MM/yyyy")));
            scrapRateId = rate.PieceRateId;   // khóa tại thời điểm ghi (CK_DR_Rate)
        }

        var defect = await BuildDefectAsync(dto, team.Data, input, scrapRateId, ct);
        return await _uow.InTransactionAsync(async () =>   // log cần DefectRecordId nên lưu trước
        {
            await _uow.Repository<DefectRecord>().AddAsync(defect);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(DefectRecord), defect.DefectRecordId, AuditActions.Insert, null, Snapshot(defect)));
            _notifier.NotifyEmployees([worker.EmployeeId], new NotificationMessage(NotificationSources.Production,
                Messages.NotifyDefectTitle,
                string.Format(Messages.NotifyDefectBody, dto.WorkDate.ToString("dd/MM/yyyy"), dto.Quantity,
                    input.Type == DefectTypes.Scrap ? "hỏng không sửa được (Scrap)" : "cần sửa lại (Rework)", operation.OperationName),
                null, nameof(DefectRecord), defect.DefectRecordId));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(defect.DefectRecordId, Messages.DefectSaved);
        });
    }

    private sealed record DefectInput(Result<int>? Failure, string Type = "", string ReasonCode = "", string? Note = null,
        string? Shift = null);

    private sealed record DefectRefs(Result<int>? Failure, Operation? Operation = null, Employee? Worker = null);

    /// <summary>Kiểm tra các trường nhập: loại lỗi, nguyên nhân (Khác phải có ghi chú), số lượng, ngày, ca.</summary>
    private DefectInput ValidateInput(CreateDefectDto dto)
    {
        var type = CodeText.ToCode(dto.DefectType);
        if (type is not (DefectTypes.Rework or DefectTypes.Scrap)) return new DefectInput(Invalid(Messages.DefectTypeInvalid));
        var reasonCode = CodeText.ToCode(dto.ReasonCode);
        if (!DefectReasons.All.Contains(reasonCode)) return new DefectInput(Invalid(Messages.DefectReasonInvalid));
        var note = string.IsNullOrWhiteSpace(dto.ReasonNote) ? null : dto.ReasonNote.Trim();
        if (reasonCode == DefectReasons.Other && note is null) return new DefectInput(Invalid(Messages.DefectNoteRequired));   // CK_DR_Other
        if (dto.Quantity <= 0) return new DefectInput(Invalid(Messages.OutputQtyInvalid));
        if (dto.WorkDate > _clock.Today) return new DefectInput(Invalid(Messages.OutputDateFuture));

        string? shift = null;
        if (!string.IsNullOrWhiteSpace(dto.ShiftCode))
        {
            shift = CodeText.ToCode(dto.ShiftCode);
            if (!ShiftCodes.All.Contains(shift)) return new DefectInput(Invalid("Ca làm việc không hợp lệ."));
        }
        return new DefectInput(null, type, reasonCode, note, shift);
    }

    /// <summary>Kiểm tra dòng đơn (chưa đóng), công đoạn (đang dùng, đúng mã hàng) và công nhân chịu trách nhiệm (đúng tổ, còn làm).</summary>
    private async Task<DefectRefs> ValidateReferencesAsync(CreateDefectDto dto, int teamId, CancellationToken ct)
    {
        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid));

        var operation = await _uow.Repository<Operation>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperationId == dto.OperationId, ct);
        if (operation is null) return new DefectRefs(Fail.NotFound<int>());
        if (operation.Status != OperationStatuses.Active)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive));
        if (operation.ProductId != line.ProductId)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.OperationProductMismatch, Messages.OperationProductMismatch));

        var worker = await _uow.Repository<Employee>().Query().AsNoTracking().FirstOrDefaultAsync(
            e => e.EmployeeId == dto.ResponsibleWorkerId && e.TeamId == teamId && e.Status == RecordStatus.Active, ct);
        return worker is null
            ? new DefectRefs(Result<int>.Failure(ErrorCodes.AssignmentWorkerInvalid, Messages.AssignmentWorkerInvalid))
            : new DefectRefs(null, operation, worker);
    }

    private async Task<DefectRecord> BuildDefectAsync(CreateDefectDto dto, int teamId, DefectInput input, int? scrapRateId,
        CancellationToken ct)
    {
        var teamCode = await _uow.Repository<Team>().Query().AsNoTracking()
            .Where(t => t.TeamId == teamId).Select(t => t.TeamCode).FirstAsync(ct);
        var now = _clock.Now;
        return new DefectRecord
        {
            DefectCode = await NextCodeAsync(teamCode, dto.WorkDate, ct), WorkDate = dto.WorkDate, ShiftCode = input.Shift,
            SewingTeamId = teamId, OrderLineId = dto.OrderLineId, OperationId = dto.OperationId,
            ResponsibleWorkerId = dto.ResponsibleWorkerId, DefectType = input.Type, Quantity = dto.Quantity,
            DefectReasonCode = input.ReasonCode, ReasonNote = input.Note, PieceRateId = scrapRateId, WorkerNotifiedAt = now,
            EditDeadline = ProductionGuard.NextWorkingDayEnd(dto.WorkDate), Status = DefectStatuses.Recorded,
            RecordedBy = _user.EmployeeId, RecordedAt = now,
        };
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
        if (defect.Status == DefectStatuses.Confirmed)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectObjectionDecided);
        if (defect.Status != DefectStatuses.Recorded)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectInvalidStatus);
        if (_clock.Now > defect.RecordedAt.AddHours(DefectObjectionWindow.Hours))
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectObjectionExpired);

        var workerName = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.EmployeeId == workerId).Select(e => e.FullName).FirstAsync(ct);
        var leaderIds = await LeaderIdsAsync(defect.SewingTeamId, ct);

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

    // ---------- Chấp nhận phiếu (Quản đốc / Admin) ----------

    /// <summary>
    /// Quản đốc / Admin chấp nhận phiếu sau khi công nhân phản đối: phiếu được giữ (Status = CONFIRMED), công nhân không phản đối lại
    /// được. Không chấp nhận thì hủy phiếu bằng <see cref="CancelAsync"/>. Kiểm khóa kỳ vì DB chặn sửa phiếu của tháng đã khóa;
    /// ghi chú (không bắt buộc) và người thao tác nằm ở AuditLog. Công nhân và tổ trưởng, tổ phó được báo.
    /// </summary>
    public async Task<Result> ConfirmAsync(int defectRecordId, ConfirmDefectDto dto, CancellationToken ct)
    {
        if (!_guard.IsManager)
            return Result.Failure(ErrorCodes.ProductionForbidden, Messages.DefectManagerOnly, ResultKind.Forbidden);
        var defect = await Defects.FirstOrDefaultAsync(d => d.DefectRecordId == defectRecordId, ct);
        if (defect is null) return Fail.NotFound();
        if (defect.Status != DefectStatuses.Recorded)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectInvalidStatus);
        if (defect.WorkerObjection is null)
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectNoObjection);
        if (await _guard.IsPeriodLockedAsync(defect.WorkDate, ct)) return _guard.PeriodLockedFailure(defect.WorkDate);

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        _uow.Repository<DefectRecord>().SetOriginalRowVersion(defect, dto.RowVer);
        defect.Status = DefectStatuses.Confirmed;
        defect.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(DefectRecord), defectRecordId, AuditActions.Update,
            new { Status = DefectStatuses.Recorded }, new { defect.Status }, note));
        _notifier.NotifyEmployees(await RecipientsAsync(defect, ct), new NotificationMessage(NotificationSources.Production,
            Messages.NotifyDefectConfirmedTitle, string.Format(Messages.NotifyDefectConfirmedBody, defect.DefectCode),
            null, nameof(DefectRecord), defectRecordId));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.DefectConfirmed);
    }

    // ---------- Dùng chung ----------

    /// <summary>Tổ trưởng / tổ phó đang làm của tổ.</summary>
    private Task<List<int>> LeaderIdsAsync(int teamId, CancellationToken ct) =>
        _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active
                        && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .Select(e => e.EmployeeId).ToListAsync(ct);

    /// <summary>Người nhận thông báo khi Quản đốc chấp nhận phiếu: tổ trưởng / tổ phó của tổ và công nhân gây lỗi.</summary>
    private async Task<List<int>> RecipientsAsync(DefectRecord defect, CancellationToken ct) =>
        (await LeaderIdsAsync(defect.SewingTeamId, ct)).Append(defect.ResponsibleWorkerId).Distinct().ToList();

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
        d.OrderLine.ProductVariant.Product.ProductCode, d.OperationId, d.Operation.OperationCode, d.Operation.OperationName,
        d.ResponsibleWorkerId, d.ResponsibleWorker.FullName, d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote,
        d.PieceRate == null ? (decimal?)null : d.PieceRate.Rate, d.WorkerNotifiedAt, d.WorkerObjection, d.ObjectedAt, d.EditDeadline, d.Status, d.RecordedAt,
        false, d.RowVer, d.OrderLine.OrderId,
        d.Request == null ? (int?)null : d.Request.RequestId, d.Request == null ? null : d.Request.RequestCode,
        d.Request == null ? null : d.Request.Status, d.SewingTeam.TeamName, d.UpdatedAt));

    /// <summary>Đổi mã DB sang PascalCase và tính CanEdit (còn hiệu lực và còn hạn, hoặc là Quản đốc).</summary>
    private DefectDto Finish(DefectDto d) => d with
    {
        DefectType = CodeText.ToPascal(d.DefectType), DefectReasonCode = CodeText.ToPascal(d.DefectReasonCode),
        Status = CodeText.ToPascal(d.Status),
        ReplacementRequestStatus = d.ReplacementRequestStatus is null ? null : CodeText.ToPascal(d.ReplacementRequestStatus),
        CanEdit = d.Status == DefectStatuses.Recorded && (_clock.Now <= d.EditDeadline || _guard.IsManager),
    };

    private static object Snapshot(DefectRecord d) => new
    {
        d.DefectCode, d.WorkDate, d.ShiftCode, d.SewingTeamId, d.OrderLineId, d.OperationId, d.ResponsibleWorkerId,
        d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote, d.PieceRateId,
    };

    private static Result<int> Invalid(string message) =>
        Result<int>.Failure(ErrorCodes.DefectInvalid, message, ResultKind.Validation);
}
