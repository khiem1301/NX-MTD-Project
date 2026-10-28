using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// KI-01 (UC25, UC26): đơn giá công đoạn. Kế toán lập (DRAFT) và gửi duyệt, Admin duyệt hoặc từ chối (bắt buộc lý do).
/// Đơn giá đã APPROVED không sửa nữa — muốn đổi giá thì lập dòng mới với ngày hiệu lực mới; sản lượng đã nhập giữ
/// đơn giá khóa lúc nhập (GBR-11). Mọi chuyển trạng thái ghi AuditLog (R6).
/// </summary>
public sealed class PieceRateService : IPieceRateService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;

    public PieceRateService(IUnitOfWork uow, IAuditLogService audit, INotificationService notifier, IClock clock)
    {
        _uow = uow; _audit = audit; _notifier = notifier; _clock = clock;
    }

    private IQueryable<PieceRate> Rates => _uow.Repository<PieceRate>().Query();

    public async Task<Result<PagedResult<PieceRateDto>>> ListAsync(PieceRateQuery query, CancellationToken ct, bool includeDraft = true)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : CodeText.ToCode(query.Status);
        var keyword = query.Keyword?.Trim();

        var page = await Rates.AsNoTracking()
            .Where(r => query.OperationId == null || r.OperationId == query.OperationId)
            .Where(r => query.ProductId == null || r.Operation.ProductId == query.ProductId)
            .Where(r => status == null || r.Status == status)
            .Where(r => includeDraft || r.Status != PieceRateStatuses.Draft)
            .Where(r => string.IsNullOrEmpty(keyword) || r.Operation.OperationCode.Contains(keyword)
                || r.Operation.OperationName.Contains(keyword) || r.Operation.Product.ProductCode.Contains(keyword))
            .OrderByDescending(r => r.EffectiveDate).ThenByDescending(r => r.PieceRateId)
            .Select(r => new PieceRateDto(r.PieceRateId, r.OperationId, r.Operation.OperationCode,
                r.Operation.OperationName, r.Operation.ProductId, r.Operation.Product.ProductCode, r.Rate,
                r.EffectiveDate, r.Status, r.SubmittedAt, r.ApprovedAt,
                r.Status == PieceRateStatuses.Approved ? r.DecisionNote : null,
                r.Status == PieceRateStatuses.Rejected ? r.DecisionNote : null))
            .ToPagedAsync(query, ct);

        return Result<PagedResult<PieceRateDto>>.Success(
            page with { Items = page.Items.Select(i => i with { Status = CodeText.ToPascal(i.Status) }).ToList() });
    }

    public async Task<Result<int>> CreateAsync(SavePieceRateDto dto, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperationId == dto.OperationId, ct);
        if (operation is null) return Fail.NotFound<int>();
        if (operation.Status != OperationStatuses.Active)
            return Result<int>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive);
        if (await DateTakenAsync(dto.OperationId, dto.EffectiveDate, null, ct))
            return Result<int>.Failure(ErrorCodes.PieceRateDuplicateDate, Messages.PieceRateDuplicateDate);

        var rate = new PieceRate
        {
            OperationId = dto.OperationId, Rate = dto.Rate, EffectiveDate = dto.EffectiveDate,
            Status = PieceRateStatuses.Draft,
        };
        return await _uow.InTransactionAsync(async () =>
        {
            await _uow.Repository<PieceRate>().AddAsync(rate);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Insert, null, Snapshot(rate)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(rate.PieceRateId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int pieceRateId, SavePieceRateDto dto, CancellationToken ct)
    {
        var rate = await _uow.Repository<PieceRate>().GetByIdAsync(pieceRateId);
        if (rate is null) return Fail.NotFound();
        if (!PieceRateStatuses.Editable.Contains(rate.Status))
            return Result.Failure(ErrorCodes.PieceRateInvalidStatus, Messages.PieceRateInvalidStatus);

        // Công đoạn của một dòng đơn giá không đổi (đổi công đoạn = lập dòng mới).
        if (await DateTakenAsync(rate.OperationId, dto.EffectiveDate, pieceRateId, ct))
            return Result.Failure(ErrorCodes.PieceRateDuplicateDate, Messages.PieceRateDuplicateDate);

        var before = Snapshot(rate);
        rate.Rate = dto.Rate;
        rate.EffectiveDate = dto.EffectiveDate;
        rate.Status = PieceRateStatuses.Draft;     // sửa sau khi bị từ chối thì phải gửi duyệt lại
        rate.DecisionNote = null;
        _audit.Add(new AuditEntry(nameof(PieceRate), pieceRateId, AuditActions.Update, before, Snapshot(rate)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> SubmitAsync(int pieceRateId, CancellationToken ct)
    {
        var rate = await LoadWithOperationAsync(pieceRateId, ct);
        if (rate is null) return Fail.NotFound();
        if (rate.Status != PieceRateStatuses.Draft)
            return Result.Failure(ErrorCodes.PieceRateInvalidStatus, Messages.PieceRateInvalidStatus);

        rate.Status = PieceRateStatuses.PendingApproval;
        rate.SubmittedAt = _clock.Now;
        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PieceRate), pieceRateId, AuditActions.Update,
                new { Status = PieceRateStatuses.Draft }, new { rate.Status }));
            await _notifier.NotifyRoleAsync(RoleCodes.Admin, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyPieceRateSubmittedTitle,
                string.Format(Messages.NotifyPieceRateSubmittedBody, rate.Operation.OperationCode, rate.Operation.Product.ProductCode),
                null, nameof(PieceRate), pieceRateId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PieceRateSubmitted);
        });
    }

    public async Task<Result> ApproveAsync(int pieceRateId, ApprovePieceRateDto dto, CancellationToken ct)
    {
        var rate = await LoadWithOperationAsync(pieceRateId, ct);
        if (rate is null) return Fail.NotFound();
        if (rate.Status != PieceRateStatuses.PendingApproval)
            return Result.Failure(ErrorCodes.PieceRateInvalidStatus, Messages.PieceRateInvalidStatus);   // GBR-05: quyết định không đổi lại

        rate.Status = PieceRateStatuses.Approved;
        rate.ApprovedAt = _clock.Now;
        rate.DecisionNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        return await DecideAsync(rate, AuditActions.Approve, null, "đã được duyệt", Messages.PieceRateApproved, ct);
    }

    public async Task<Result> RejectAsync(int pieceRateId, RejectPieceRateDto dto, CancellationToken ct)
    {
        var rate = await LoadWithOperationAsync(pieceRateId, ct);
        if (rate is null) return Fail.NotFound();
        if (rate.Status != PieceRateStatuses.PendingApproval)
            return Result.Failure(ErrorCodes.PieceRateInvalidStatus, Messages.PieceRateInvalidStatus);

        var reason = dto.Reason.Trim();
        rate.Status = PieceRateStatuses.Rejected;
        rate.DecisionNote = reason;   // CK_PieceRate_Reject
        return await DecideAsync(rate, AuditActions.Reject, reason, $"bị từ chối: {reason}", Messages.PieceRateRejected, ct);
    }

    public async Task<Result<int>> ApproveManyAsync(ApproveManyPieceRatesDto dto, CancellationToken ct)
    {
        var ids = dto.Ids.Distinct().ToList();
        if (ids.Count == 0)
            return Result<int>.Failure(ErrorCodes.CommonValidation, Messages.PieceRateNoneSelected, ResultKind.Validation);

        var rates = await Rates.Include(r => r.Operation)
            .Where(r => ids.Contains(r.PieceRateId) && r.Status == PieceRateStatuses.PendingApproval)
            .ToListAsync(ct);
        if (rates.Count == 0)
            return Result<int>.Failure(ErrorCodes.PieceRateInvalidStatus, Messages.PieceRateInvalidStatus);

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        var now = _clock.Now;
        return await _uow.InTransactionAsync(async () =>
        {
            foreach (var rate in rates)
            {
                rate.Status = PieceRateStatuses.Approved;
                rate.ApprovedAt = now;
                rate.DecisionNote = note;
                _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Approve,
                    new { Status = PieceRateStatuses.PendingApproval }, new { rate.Status, rate.DecisionNote }));
            }

            await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyPieceRateDecidedTitle,
                string.Format(Messages.NotifyPieceRatesApprovedManyBody, rates.Count), null, null, null), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(rates.Count, string.Format(Messages.PieceRatesApprovedMany, rates.Count));
        });
    }

    public async Task<EffectivePieceRate?> FindEffectiveAsync(int operationId, DateOnly date, CancellationToken ct)
    {
        var rate = await Rates.AsNoTracking()
            .Where(r => r.OperationId == operationId && r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= date)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new EffectivePieceRate(r.PieceRateId, r.Rate))
            .FirstOrDefaultAsync(ct);
        return rate;
    }

    private Task<Result> DecideAsync(PieceRate rate, string action, string? reason, string outcome, string message,
        CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, action,
                new { Status = PieceRateStatuses.PendingApproval }, new { rate.Status, rate.DecisionNote }, reason));
            await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyPieceRateDecidedTitle,
                string.Format(Messages.NotifyPieceRateDecidedBody, rate.Operation.OperationCode, outcome),
                null, nameof(PieceRate), rate.PieceRateId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(message);
        });

    private Task<PieceRate?> LoadWithOperationAsync(int pieceRateId, CancellationToken ct) =>
        Rates.Include(r => r.Operation).ThenInclude(o => o.Product)
            .FirstOrDefaultAsync(r => r.PieceRateId == pieceRateId, ct);

    private Task<bool> DateTakenAsync(int operationId, DateOnly date, int? exceptId, CancellationToken ct) =>
        Rates.AsNoTracking().AnyAsync(r => r.OperationId == operationId && r.EffectiveDate == date
            && (exceptId == null || r.PieceRateId != exceptId), ct);

    private static object Snapshot(PieceRate r) => new { r.OperationId, r.Rate, r.EffectiveDate, r.Status };
}
