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
/// đơn giá duyệt tại ngày làm việc). Bắt buộc công đoạn, công nhân gây lỗi, lý do (OTHER phải ghi chú); báo cho công nhân.
/// Công nhân không phản hồi / khiếu nại phiếu (DEV-ALL-106). Sửa / hủy phiếu: tổ trưởng của tổ trong <see cref="DefectEditWindow.Hours"/>
/// giờ kể từ lúc lập phiếu; Quản đốc / Admin không giới hạn giờ nhưng bị chặn khi đã có ràng buộc phát sinh từ phiếu (kỳ lương
/// khóa, đơn đã đóng, đã có đơn cắt bù, hủy / sửa phiếu nhập sai làm sản lượng vượt trần). Hủy luôn bắt buộc lý do. Không sinh
/// đơn cắt bù (KI-09 — DEV-KI-08-1).
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

    /// <summary>Tổ trưởng / tổ phó chỉ thấy phiếu của tổ mình; Quản đốc / Admin thấy mọi tổ (lọc được theo tổ).</summary>
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
        var page = await Project(Defects.AsNoTracking()
                .Where(d => teamId == null || d.SewingTeamId == teamId)
                // Quản đốc lọc theo tổ; tổ trưởng đã bị giới hạn ở tổ mình ở trên.
                .Where(d => teamId != null || query.SewingTeamId == null || d.SewingTeamId == query.SewingTeamId)
                .Where(d => status == null || d.Status == status)
                .Where(d => type == null || d.DefectType == type)
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

    /// <summary>Công nhân chỉ xem phiếu của chính mình (không thấy phiếu đã hủy); không có thao tác phản hồi.</summary>
    public async Task<Result<IReadOnlyList<DefectDto>>> ListMineAsync(int workerId, DefectQuery query, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : CodeText.ToCode(query.Status);
        var items = await Project(Defects.AsNoTracking()
                .Where(d => d.ResponsibleWorkerId == workerId && d.Status != DefectStatuses.Cancelled)
                .Where(d => status == null || d.Status == status)
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

        var input = ValidateInput(dto.DefectType, dto.ReasonCode, dto.ReasonNote, dto.Quantity, dto.WorkDate, dto.ShiftCode);
        if (input.Failure is not null) return input.Failure;
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<int>(_guard.PeriodLockedFailure(dto.WorkDate));

        var refs = await ValidateReferencesAsync(dto.OrderLineId, dto.OperationId, dto.ResponsibleWorkerId, dto.WorkDate,
            team.Data, ct);
        if (refs.Failure is not null) return refs.Failure;
        var (operation, worker) = (refs.Operation!, refs.Worker!);

        int? scrapRateId = null;
        if (input.Type != DefectTypes.Rework)
        {
            var rate = await _rates.FindEffectiveAsync(dto.OperationId, dto.WorkDate, ct);
            if (rate is null)   // MSG18: Scrap trừ lương theo đơn giá duyệt nên phải có đơn giá
                return Result<int>.Failure(ErrorCodes.PieceRateNotApproved, string.Format(Messages.PieceRateNotApproved,
                    operation.OperationCode, dto.WorkDate.ToString("dd/MM/yyyy")));
            scrapRateId = rate.PieceRateId;   // khóa tại thời điểm ghi (CK_DR_Rate)
        }
        if (input.Type == DefectTypes.OutputError)
        {
            var excess = await CheckOutputErrorQuantityAsync(dto.WorkDate, dto.ResponsibleWorkerId, dto.OrderLineId,
                dto.OperationId, dto.Quantity, null, ct);
            if (excess is not null) return excess;
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
                    DescribeType(input.Type), operation.OperationName),
                null, nameof(DefectRecord), defect.DefectRecordId));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(defect.DefectRecordId, Messages.DefectSaved);
        });
    }

    private sealed record DefectInput(Result<int>? Failure, string Type = "", string ReasonCode = "", string? Note = null,
        string? Shift = null);

    private sealed record DefectRefs(Result<int>? Failure, Operation? Operation = null, Employee? Worker = null);

    /// <summary>Kiểm tra các trường nhập: loại lỗi, nguyên nhân (Khác phải có ghi chú), số lượng, ngày, ca.</summary>
    private DefectInput ValidateInput(string defectType, string reasonCodeText, string? reasonNote, int quantity, DateOnly workDate,
        string? shiftCode)
    {
        var type = CodeText.ToCode(defectType);
        if (!DefectTypes.All.Contains(type)) return new DefectInput(Invalid(Messages.DefectTypeInvalid));
        var reasonCode = CodeText.ToCode(reasonCodeText);
        if (type == DefectTypes.OutputError) reasonCode = DefectReasons.Other;   // nhập sai sản lượng: lý do là ghi chú bắt buộc
        if (!DefectReasons.All.Contains(reasonCode)) return new DefectInput(Invalid(Messages.DefectReasonInvalid));
        var note = string.IsNullOrWhiteSpace(reasonNote) ? null : reasonNote.Trim();
        if (reasonCode == DefectReasons.Other && note is null) return new DefectInput(Invalid(Messages.DefectNoteRequired));   // CK_DR_Other
        if (quantity <= 0) return new DefectInput(Invalid(Messages.OutputQtyInvalid));
        if (workDate > _clock.Today) return new DefectInput(Invalid(Messages.OutputDateFuture));

        string? shift = null;
        if (!string.IsNullOrWhiteSpace(shiftCode))
        {
            shift = CodeText.ToCode(shiftCode);
            if (!ShiftCodes.All.Contains(shift)) return new DefectInput(Invalid("Ca làm việc không hợp lệ."));
        }
        return new DefectInput(null, type, reasonCode, note, shift);
    }

    /// <summary>Kiểm tra dòng đơn (chưa đóng), công đoạn (đang dùng, đúng mã hàng) và công nhân chịu trách nhiệm (đúng tổ, còn làm).</summary>
    private async Task<DefectRefs> ValidateReferencesAsync(int orderLineId, int operationId, int workerId, DateOnly workDate,
        int teamId, CancellationToken ct)
    {
        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.Order)
            .FirstOrDefaultAsync(l => l.OrderLineId == orderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid));
        if (ProductionGuard.BeforeOrderStart(workDate, line.Order.ReceivedDate) is { } beforeStart)
            return new DefectRefs(Invalid(beforeStart));

        var operation = await _uow.Repository<Operation>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperationId == operationId, ct);
        if (operation is null) return new DefectRefs(Fail.NotFound<int>());
        if (operation.Status != OperationStatuses.Active)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive));
        if (operation.ProductId != line.ProductId)
            return new DefectRefs(Result<int>.Failure(ErrorCodes.OperationProductMismatch, Messages.OperationProductMismatch));

        var worker = await _uow.Repository<Employee>().Query().AsNoTracking().FirstOrDefaultAsync(
            e => e.EmployeeId == workerId && e.TeamId == teamId && e.Status == RecordStatus.Active, ct);
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
            EditDeadline = now.AddHours(DefectEditWindow.Hours), Status = DefectStatuses.Recorded,
            RecordedBy = _user.EmployeeId, RecordedAt = now,
        };
    }

    // ---------- Sửa / hủy ----------

    /// <summary>Sửa phiếu (không đổi ngày và dòng đơn). Quyền và ràng buộc xem <see cref="CheckMutableAsync"/>.</summary>
    public async Task<Result> UpdateAsync(int defectRecordId, UpdateDefectDto dto, CancellationToken ct)
    {
        var defect = await Defects.FirstOrDefaultAsync(d => d.DefectRecordId == defectRecordId, ct);
        if (defect is null || !CanSeeTeam(defect.SewingTeamId)) return Fail.NotFound();
        var blocked = await CheckMutableAsync(defect, ct);
        if (blocked is not null) return blocked;

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        if (_guard.IsManager && reason is null)   // Quản đốc sửa ngoài hạn của tổ trưởng nên phải có lý do để truy vết
            return Result.Failure(ErrorCodes.DefectInvalid, Messages.DefectUpdateReasonRequired, ResultKind.Validation);

        var input = ValidateInput(dto.DefectType, dto.ReasonCode, dto.ReasonNote, dto.Quantity, defect.WorkDate, dto.ShiftCode);
        if (input.Failure is not null) return input.Failure;
        var refs = await ValidateReferencesAsync(defect.OrderLineId, dto.OperationId, dto.ResponsibleWorkerId, defect.WorkDate,
            defect.SewingTeamId, ct);
        if (refs.Failure is not null) return refs.Failure;
        var (operation, worker) = (refs.Operation!, refs.Worker!);

        var changed = defect.ShiftCode != input.Shift || defect.OperationId != dto.OperationId
                      || defect.ResponsibleWorkerId != dto.ResponsibleWorkerId || defect.DefectType != input.Type
                      || defect.Quantity != dto.Quantity || defect.DefectReasonCode != input.ReasonCode
                      || defect.ReasonNote != input.Note;
        if (!changed) return Result.Failure(ErrorCodes.DefectInvalid, Messages.DefectNothingChanged, ResultKind.Validation);

        var rate = await ResolveRateAsync(defect, dto.OperationId, input.Type, operation, ct);
        if (rate.Failure is not null) return rate.Failure;
        var overOnLine = await CheckIncreaseWithinOnLineAsync(defect, input.Type, dto.Quantity, ct);
        if (overOnLine is not null) return overOnLine;
        var released = ReleasedOutputErrorQty(defect, input.Type, dto);
        if (released > 0)
        {
            var overCap = await CheckReleaseOutputErrorAsync(defect, released, ct);
            if (overCap is not null) return overCap;
        }
        if (input.Type == DefectTypes.OutputError)
        {
            var excess = await CheckOutputErrorQuantityAsync(defect.WorkDate, dto.ResponsibleWorkerId, defect.OrderLineId,
                dto.OperationId, dto.Quantity, defect.DefectRecordId, ct);
            if (excess is not null) return excess;
        }

        var before = Snapshot(defect);
        var previousWorkerId = defect.ResponsibleWorkerId;
        _uow.Repository<DefectRecord>().SetOriginalRowVersion(defect, dto.RowVer);
        defect.ShiftCode = input.Shift; defect.OperationId = dto.OperationId; defect.ResponsibleWorkerId = dto.ResponsibleWorkerId;
        defect.DefectType = input.Type; defect.Quantity = dto.Quantity; defect.DefectReasonCode = input.ReasonCode;
        defect.ReasonNote = input.Note; defect.PieceRateId = rate.PieceRateId;
        defect.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(DefectRecord), defectRecordId, AuditActions.Update, before, Snapshot(defect), reason));

        var recipients = new[] { previousWorkerId, worker.EmployeeId }.Distinct().ToList();
        _notifier.NotifyEmployees(recipients, new NotificationMessage(NotificationSources.Production,
            Messages.NotifyDefectUpdatedTitle,
            string.Format(Messages.NotifyDefectUpdatedBody, defect.DefectCode, dto.Quantity, DescribeType(input.Type),
                operation.OperationName),
            null, nameof(DefectRecord), defectRecordId));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.DefectUpdated);
    }

    /// <summary>Hủy phiếu: lý do bắt buộc với mọi vai trò (ghi vào nhật ký và báo cho công nhân).</summary>
    public async Task<Result> CancelAsync(int defectRecordId, CancelDefectDto dto, CancellationToken ct)
    {
        var defect = await Defects.FirstOrDefaultAsync(d => d.DefectRecordId == defectRecordId, ct);
        if (defect is null || !CanSeeTeam(defect.SewingTeamId)) return Fail.NotFound();
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        if (reason is null)
            return Result.Failure(ErrorCodes.DefectInvalid, Messages.DefectCancelReasonRequired, ResultKind.Validation);

        var blocked = await CheckMutableAsync(defect, ct);
        if (blocked is not null) return blocked;
        var overCap = await CheckReleaseOutputErrorAsync(defect, defect.Quantity, ct);
        if (overCap is not null) return overCap;

        var previousStatus = defect.Status;
        _uow.Repository<DefectRecord>().SetOriginalRowVersion(defect, dto.RowVer);
        defect.Status = DefectStatuses.Cancelled;
        defect.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(DefectRecord), defectRecordId, AuditActions.Update,
            new { Status = previousStatus }, new { defect.Status }, reason));
        _notifier.NotifyEmployees([defect.ResponsibleWorkerId], new NotificationMessage(NotificationSources.Production,
            Messages.NotifyDefectCancelledTitle, string.Format(Messages.NotifyDefectCancelledBody, defect.DefectCode, reason),
            null, nameof(DefectRecord), defectRecordId));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.DefectCancelled);
    }

    /// <summary>
    /// Điều kiện chung để sửa / hủy: phiếu còn hiệu lực; tổ trưởng chỉ trong <see cref="DefectEditWindow.Hours"/> giờ kể từ lúc lập
    /// (Quản đốc / Admin không giới hạn giờ); kỳ lương chưa khóa; đơn hàng chưa đóng; chưa có đơn cắt bù còn hiệu lực gắn với phiếu.
    /// </summary>
    private async Task<Result?> CheckMutableAsync(DefectRecord defect, CancellationToken ct)
    {
        if (!DefectStatuses.Editable.Contains(defect.Status))
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectInvalidStatus);
        if (!_guard.IsManager && _clock.Now > defect.RecordedAt.AddHours(DefectEditWindow.Hours))
            return Result.Failure(ErrorCodes.ProductionEditDeadlinePassed, Messages.DefectEditWindowPassed, ResultKind.Forbidden);
        if (await _guard.IsPeriodLockedAsync(defect.WorkDate, ct)) return _guard.PeriodLockedFailure(defect.WorkDate);

        var closed = OrderStatuses.Closed.ToArray();
        if (await _uow.Repository<OrderLine>().Query().AsNoTracking()
                .AnyAsync(l => l.OrderLineId == defect.OrderLineId && closed.Contains(l.Order.Status), ct))
            return Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectOrderClosed);

        var dead = RequestStatuses.Dead.ToArray();
        var request = await _uow.Repository<Request>().Query().AsNoTracking()
            .Where(r => r.DefectRecordId == defect.DefectRecordId && !dead.Contains(r.Status))
            .Select(r => new { r.RequestCode, r.Status }).FirstOrDefaultAsync(ct);
        return request is null
            ? null
            : Result.Failure(ErrorCodes.DefectInvalidStatus,
                string.Format(Messages.DefectHasReplacement, request.RequestCode, request.Status));
    }

    private sealed record RateResult(Result? Failure, int? PieceRateId);

    /// <summary>
    /// Đơn giá khóa của phiếu sau khi sửa: Rework không có; giữ đơn giá cũ khi vẫn là phiếu trừ lương cùng công đoạn (đơn giá khóa
    /// tại lúc ghi); đổi công đoạn hoặc chuyển từ Rework sang loại trừ lương thì lấy lại đơn giá duyệt của ngày làm việc.
    /// </summary>
    private async Task<RateResult> ResolveRateAsync(DefectRecord defect, int operationId, string newType, Operation operation,
        CancellationToken ct)
    {
        if (newType == DefectTypes.Rework) return new RateResult(null, null);
        if (defect.PieceRateId is { } kept && defect.OperationId == operationId) return new RateResult(null, kept);

        var rate = await _rates.FindEffectiveAsync(operationId, defect.WorkDate, ct);
        return rate is null
            ? new RateResult(Result.Failure(ErrorCodes.PieceRateNotApproved, string.Format(Messages.PieceRateNotApproved,
                operation.OperationCode, defect.WorkDate.ToString("dd/MM/yyyy"))), null)
            : new RateResult(null, rate.PieceRateId);
    }

    /// <summary>Số lượng nhập sai mà phiếu cũ đang trừ sẽ được trả lại cho công nhân cũ sau khi sửa (0 nếu không trả gì).</summary>
    /// <summary>
    /// Sửa tăng số lượng hàng hỏng (Rework / Scrap) chỉ được tăng thêm tối đa bằng số hàng đang trên chuyền của tổ ở dòng đơn (QA/QC chưa
    /// nhận, chưa hủy). Giảm hoặc giữ số lượng, và phiếu nhập sai sản lượng (có trần riêng) không bị kiểm.
    /// </summary>
    private async Task<Result?> CheckIncreaseWithinOnLineAsync(DefectRecord defect, string newType, int newQuantity, CancellationToken ct)
    {
        var increase = newType switch
        {
            DefectTypes.Scrap => newQuantity - (defect.DefectType == DefectTypes.Scrap ? defect.Quantity : 0),
            DefectTypes.Rework => newQuantity - (defect.DefectType == DefectTypes.OutputError ? 0 : defect.Quantity),
            _ => 0,
        };
        if (increase <= 0) return null;

        var onLine = await _guard.ComputeOnLineAsync(defect.SewingTeamId, defect.OrderLineId, null, ct);
        return increase > onLine.OnLine
            ? Result.Failure(ErrorCodes.DefectInvalid, string.Format(Messages.DefectIncreaseOverOnLine, increase, onLine.OnLine))
            : null;
    }

    private static int ReleasedOutputErrorQty(DefectRecord defect, string newType, UpdateDefectDto dto)
    {
        if (defect.DefectType != DefectTypes.OutputError) return 0;
        var sameSlot = newType == DefectTypes.OutputError && defect.ResponsibleWorkerId == dto.ResponsibleWorkerId
                       && defect.OperationId == dto.OperationId;
        return sameSlot ? Math.Max(0, defect.Quantity - dto.Quantity) : defect.Quantity;
    }

    private async Task<Result?> CheckReleaseOutputErrorAsync(DefectRecord defect, int releasedQty, CancellationToken ct)
    {
        if (defect.DefectType != DefectTypes.OutputError) return null;
        var remaining = await _uow.Repository<VwWorkerOutputCap>().Query().AsNoTracking()
            .Where(c => c.WorkerId == defect.ResponsibleWorkerId && c.OrderLineId == defect.OrderLineId && c.OperationId == defect.OperationId)
            .Select(c => c.RemainingQty).FirstOrDefaultAsync(ct);
        return remaining is { } left && left < releasedQty
            ? Result.Failure(ErrorCodes.DefectInvalidStatus, Messages.DefectOutputErrorCancelOverCap)
            : null;
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
        d.OrderLine.ProductVariant.Product.ProductCode, d.OperationId, d.Operation.OperationCode, d.Operation.OperationName,
        d.ResponsibleWorkerId, d.ResponsibleWorker.FullName, d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote,
        d.PieceRate == null ? (decimal?)null : d.PieceRate.Rate, d.WorkerNotifiedAt, d.EditDeadline, d.Status, d.RecordedAt,
        false, d.RowVer, d.OrderLine.OrderId,
        d.Request == null ? (int?)null : d.Request.RequestId, d.Request == null ? null : d.Request.RequestCode,
        d.Request == null ? null : d.Request.Status, d.SewingTeam.TeamName, d.UpdatedAt));

    /// <summary>
    /// Đổi mã DB sang PascalCase; hạn sửa của tổ trưởng luôn là lúc lập + 24 giờ (kể cả phiếu cũ); CanEdit = còn hiệu lực và còn
    /// hạn, hoặc là Quản đốc (ràng buộc khác như kỳ lương khóa, đã có đơn cắt bù chỉ biết khi bấm sửa / hủy).
    /// </summary>
    private DefectDto Finish(DefectDto d)
    {
        var deadline = d.RecordedAt.AddHours(DefectEditWindow.Hours);
        return d with
        {
            DefectType = CodeText.ToPascal(d.DefectType), DefectReasonCode = CodeText.ToPascal(d.DefectReasonCode),
            Status = CodeText.ToPascal(d.Status),
            ReplacementRequestStatus = d.ReplacementRequestStatus is null ? null : CodeText.ToPascal(d.ReplacementRequestStatus),
            EditDeadline = deadline,
            CanEdit = DefectStatuses.Editable.Contains(CodeText.ToCode(d.Status)) && (_clock.Now <= deadline || _guard.IsManager),
        };
    }

    private static object Snapshot(DefectRecord d) => new
    {
        d.DefectCode, d.WorkDate, d.ShiftCode, d.SewingTeamId, d.OrderLineId, d.OperationId, d.ResponsibleWorkerId,
        d.DefectType, d.Quantity, d.DefectReasonCode, d.ReasonNote, d.PieceRateId,
    };

    private static string DescribeType(string type) => type switch
    {
        DefectTypes.Scrap => "hỏng không sửa được (Scrap)",
        DefectTypes.OutputError => Messages.DefectOutputErrorBody,
        _ => "cần sửa lại (Rework)",
    };

    /// <summary>
    /// Phiếu nhập sai sản lượng chỉ trừ được phần công nhân đã nhập ở công đoạn đó trong ngày, kể cả các phiếu nhập sai đã ghi trước
    /// (DEV-ALL-79); <paramref name="excludeDefectId"/> bỏ qua chính phiếu đang sửa. Trả null khi số trừ hợp lệ.
    /// </summary>
    private async Task<Result<int>?> CheckOutputErrorQuantityAsync(DateOnly workDate, int workerId, int orderLineId, int operationId,
        int quantity, int? excludeDefectId, CancellationToken ct)
    {
        var date = workDate.ToString("dd/MM/yyyy");
        var output = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(r => r.WorkDate == workDate && r.WorkerId == workerId
                        && r.OrderLineId == orderLineId && r.OperationId == operationId)
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;
        if (output == 0) return Invalid(string.Format(Messages.DefectOutputErrorNoOutput, date));

        var deducted = await Defects.AsNoTracking()
            .Where(d => d.DefectType == DefectTypes.OutputError && d.Status != DefectStatuses.Cancelled && d.WorkDate == workDate
                        && d.ResponsibleWorkerId == workerId && d.OrderLineId == orderLineId
                        && d.OperationId == operationId && d.DefectRecordId != excludeDefectId)
            .SumAsync(d => (int?)d.Quantity, ct) ?? 0;
        var left = output - deducted;
        return quantity > left
            ? Invalid(string.Format(Messages.DefectOutputErrorExceeds, date, output, deducted, left))
            : null;
    }

    private static Result<int> Invalid(string message) =>
        Result<int>.Failure(ErrorCodes.DefectInvalid, message, ResultKind.Validation);
}
