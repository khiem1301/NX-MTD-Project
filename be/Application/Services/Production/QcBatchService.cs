using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// KI-06 + KI-11 (UC57, UC22): QA/QC nhập số hàng hoàn thiện đạt theo đợt trong ca, không phân loại lỗi.
/// Trần (MSG19): số đạt ≤ hàng đang có trên chuyền = BTP tổ đã nhận − đã QC đạt − Scrap. Hạn sửa hết ca; sau hạn chỉ
/// Quản đốc sửa, bắt buộc lý do, ghi AuditLog (MSG20). "Hàng trên chuyền" tính theo tổ + dòng đơn — DEV-KI-06-1.
/// </summary>
public sealed class QcBatchService : IQcBatchService
{
    private readonly IUnitOfWork _uow;
    private readonly ProductionGuard _guard;
    private readonly ICurrentUser _user;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public QcBatchService(IUnitOfWork uow, ProductionGuard guard, ICurrentUser user, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _guard = guard; _user = user; _audit = audit; _clock = clock;
    }

    private IQueryable<QcBatch> Batches => _uow.Repository<QcBatch>().Query();

    public async Task<Result<PagedResult<QcBatchDto>>> ListAsync(QcBatchQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var teamScope = query.SewingTeamId;
        if (!_guard.IsManager)   // QA/QC thuộc một tổ may: chỉ thấy đợt QC của tổ mình
        {
            var own = _guard.ResolveQcTeam(null);
            if (!own.IsSuccess) return Fail.As<PagedResult<QcBatchDto>>(own);
            teamScope = own.Data;
        }
        var page = await Batches.AsNoTracking()
            .Where(b => query.WorkDate == null || b.WorkDate == query.WorkDate)
            .Where(b => teamScope == null || b.SewingTeamId == teamScope)
            .Where(b => query.OrderLineId == null || b.OrderLineId == query.OrderLineId)
            .Where(b => string.IsNullOrEmpty(keyword) || b.OrderLine.Order.OrderCode.Contains(keyword)
                        || b.OrderLine.ProductVariant.Product.ProductCode.Contains(keyword))
            .OrderByDescending(b => b.WorkDate).ThenByDescending(b => b.BatchTime).ThenByDescending(b => b.QcBatchId)
            .Select(b => new QcBatchDto(b.QcBatchId, b.WorkDate, b.ShiftCode, b.BatchTime, b.SewingTeamId,
                b.SewingTeam.TeamCode, b.OrderLineId, b.OrderLine.Order.OrderCode, b.OrderLine.ProductVariant.Product.ProductCode,
                b.OrderLine.ProductVariant.ProductColor.Color, b.OrderLine.ProductVariant.SizeCode, b.FinishedQty, b.InspectorId, b.Inspector.FullName,
                b.EditDeadline, false, b.RowVer))
            .ToPagedAsync(query, ct);

        var now = _clock.Now;
        var items = page.Items.Select(i => i with { CanEdit = now <= i.EditDeadline || _guard.IsManager }).ToList();
        return Result<PagedResult<QcBatchDto>>.Success(page with { Items = items });
    }

    public async Task<Result<OnLineQtyDto>> GetOnLineAsync(int? sewingTeamId, int orderLineId, CancellationToken ct)
    {
        var team = _guard.ResolveQcTeam(sewingTeamId);
        return team.IsSuccess
            ? Result<OnLineQtyDto>.Success(await _guard.ComputeOnLineAsync(team.Data, orderLineId, null, ct))
            : Fail.As<OnLineQtyDto>(team);
    }

    /// <summary>Kiểm ca, số lượng, ngày, kỳ khóa, tổ may, dòng đơn chưa đóng, lô trùng và số lượng không vượt số đang trên chuyền (MSG19).</summary>
    private async Task<Result<int>?> ValidateAsync(CreateQcBatchDto dto, int sewingTeamId, string shift, CancellationToken ct)
    {
        if (!ShiftCodes.All.Contains(shift))
            return Result<int>.Failure(ErrorCodes.OutputInvalid, "Ca làm việc không hợp lệ.", ResultKind.Validation);
        if (dto.FinishedQty <= 0)
            return Result<int>.Failure(ErrorCodes.OutputInvalid, Messages.OutputQtyInvalid, ResultKind.Validation);
        if (dto.WorkDate > _clock.Today)
            return Result<int>.Failure(ErrorCodes.OutputInvalid, Messages.QcDateFuture, ResultKind.Validation);
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<int>(_guard.PeriodLockedFailure(dto.WorkDate));

        var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(
            t => t.TeamId == sewingTeamId && t.TeamType == TeamTypes.Sewing && t.Status == RecordStatus.Active, ct);
        if (team is null) return Fail.NotFound<int>();

        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.Order)
            .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);
        if (ProductionGuard.BeforeOrderStart(dto.WorkDate, line.Order.ReceivedDate) is { } beforeStart)
            return Result<int>.Failure(ErrorCodes.OutputInvalid, beforeStart, ResultKind.Validation);

        var duplicate = await Batches.AsNoTracking().AnyAsync(b => b.WorkDate == dto.WorkDate && b.ShiftCode == shift
            && b.BatchTime == dto.BatchTime && b.SewingTeamId == sewingTeamId && b.OrderLineId == dto.OrderLineId, ct);
        if (duplicate) return Result<int>.Failure(ErrorCodes.QcBatchDuplicate, Messages.QcBatchDuplicate);

        var onLine = await _guard.ComputeOnLineAsync(sewingTeamId, dto.OrderLineId, null, ct);
        return dto.FinishedQty > onLine.OnLine
            ? Result<int>.Failure(ErrorCodes.QcExceedsOnLine, string.Format(Messages.QcExceedsOnLine, onLine.OnLine))   // MSG19
            : null;
    }

    public async Task<Result<int>> CreateAsync(CreateQcBatchDto dto, CancellationToken ct)
    {
        var shift = CodeText.ToCode(dto.ShiftCode);
        var team = _guard.ResolveQcTeam(dto.SewingTeamId);
        if (!team.IsSuccess) return team;
        var invalid = await ValidateAsync(dto, team.Data, shift, ct);
        if (invalid is not null) return invalid;

        var batch = new QcBatch
        {
            WorkDate = dto.WorkDate, ShiftCode = shift, BatchTime = dto.BatchTime, SewingTeamId = team.Data,
            OrderLineId = dto.OrderLineId, FinishedQty = dto.FinishedQty, InspectorId = _user.EmployeeId,
            EditDeadline = await _guard.ShiftEndAsync(dto.WorkDate, shift, ct), CreatedAt = _clock.Now,
        };
        return await _uow.InTransactionAsync(async () =>   // log cần QcBatchId nên lưu trước
        {
            await _uow.Repository<QcBatch>().AddAsync(batch);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(QcBatch), batch.QcBatchId, AuditActions.Insert, null, Snapshot(batch)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(batch.QcBatchId, Messages.QcBatchSaved);
        });
    }

    public async Task<Result> UpdateAsync(int qcBatchId, UpdateQcBatchDto dto, CancellationToken ct)
    {
        if (dto.FinishedQty <= 0)
            return Result.Failure(ErrorCodes.OutputInvalid, Messages.QcQtyInvalid, ResultKind.Validation);
        var batch = await Batches.FirstOrDefaultAsync(b => b.QcBatchId == qcBatchId, ct);
        if (batch is null || !IsOwnTeam(batch)) return Fail.NotFound();

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var window = _guard.CheckEditWindow(batch.EditDeadline, reason);
        if (window is not null) return window;
        if (await _guard.IsPeriodLockedAsync(batch.WorkDate, ct)) return _guard.PeriodLockedFailure(batch.WorkDate);

        // Trần tính trên chính đợt này đã được trừ ra, để sửa giảm / tăng nhẹ vẫn hợp lệ.
        var onLine = await _guard.ComputeOnLineAsync(batch.SewingTeamId, batch.OrderLineId, qcBatchId, ct);
        if (dto.FinishedQty > onLine.OnLine)
            return Result.Failure(ErrorCodes.QcExceedsOnLine, string.Format(Messages.QcExceedsOnLine, onLine.OnLine));
        var handedOver = await CheckHandedOverAsync(batch, dto.FinishedQty, ct);
        if (handedOver is not null) return handedOver;

        _uow.Repository<QcBatch>().SetOriginalRowVersion(batch, dto.RowVer);
        var before = Snapshot(batch);
        batch.FinishedQty = dto.FinishedQty;
        batch.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(QcBatch), qcBatchId, AuditActions.Update, before, Snapshot(batch), reason));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.QcBatchSaved);
    }

    public async Task<Result> DeleteAsync(int qcBatchId, DeleteQcBatchDto dto, CancellationToken ct)
    {
        var batch = await Batches.FirstOrDefaultAsync(b => b.QcBatchId == qcBatchId, ct);
        if (batch is null || !IsOwnTeam(batch)) return Fail.NotFound();

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var window = _guard.CheckEditWindow(batch.EditDeadline, reason);
        if (window is not null) return window;
        if (await _guard.IsPeriodLockedAsync(batch.WorkDate, ct)) return _guard.PeriodLockedFailure(batch.WorkDate);

        var handedOver = await CheckHandedOverAsync(batch, 0, ct);
        if (handedOver is not null) return handedOver;

        _uow.Repository<QcBatch>().SetOriginalRowVersion(batch, dto.RowVer);
        _audit.Add(new AuditEntry(nameof(QcBatch), qcBatchId, AuditActions.Delete, Snapshot(batch), null, reason));
        _uow.Repository<QcBatch>().Remove(batch);
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.QcBatchDeleted);
    }

    /// <summary>
    /// Số QC đạt của dòng đơn (mọi tổ) sau khi đợt này đổi thành <paramref name="newQty"/> không được thấp hơn số đã bàn giao cho kho
    /// thành phẩm (phiếu chờ nhận + đã nhận), nếu không "còn bàn giao được" = QC đạt − đã bàn giao bị âm.
    /// </summary>
    private async Task<Result?> CheckHandedOverAsync(QcBatch batch, int newQty, CancellationToken ct)
    {
        var others = await Batches.AsNoTracking()
            .Where(b => b.OrderLineId == batch.OrderLineId && b.QcBatchId != batch.QcBatchId)
            .SumAsync(b => (int?)b.FinishedQty, ct) ?? 0;
        var handed = await _uow.Repository<FgHandoverLine>().Query().AsNoTracking()
            .Where(l => l.OrderLineId == batch.OrderLineId
                        && (l.FgHandover.Status == HandoverStatuses.Pending || l.FgHandover.Status == HandoverStatuses.Confirmed))
            .SumAsync(l => (int?)l.HandedQty, ct) ?? 0;
        var total = others + newQty;
        return total < handed
            ? Result.Failure(ErrorCodes.QcBelowHandedOver, string.Format(Messages.QcBelowHandedOver, total, handed))
            : null;
    }

    /// <summary>Quản đốc sửa được mọi tổ; QA/QC chỉ đợt QC của tổ mình.</summary>
    private bool IsOwnTeam(QcBatch batch)
    {
        if (_guard.IsManager) return true;
        var own = _guard.ResolveQcTeam(null);
        return own.IsSuccess && own.Data == batch.SewingTeamId;
    }

    private static object Snapshot(QcBatch b) =>
        new { b.WorkDate, b.ShiftCode, b.BatchTime, b.SewingTeamId, b.OrderLineId, b.FinishedQty };
}
