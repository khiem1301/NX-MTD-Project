using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

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
        var page = await Batches.AsNoTracking()
            .Where(b => query.WorkDate == null || b.WorkDate == query.WorkDate)
            .Where(b => query.SewingTeamId == null || b.SewingTeamId == query.SewingTeamId)
            .Where(b => query.OrderLineId == null || b.OrderLineId == query.OrderLineId)
            .Where(b => string.IsNullOrEmpty(keyword) || b.OrderLine.Order.OrderCode.Contains(keyword)
                        || b.OrderLine.ProductVariant.Product.ProductCode.Contains(keyword))
            .OrderByDescending(b => b.WorkDate).ThenByDescending(b => b.BatchTime).ThenByDescending(b => b.QcBatchId)
            .Select(b => new QcBatchDto(b.QcBatchId, b.WorkDate, b.ShiftCode, b.BatchTime, b.SewingTeamId,
                b.SewingTeam.TeamName, b.OrderLineId, b.OrderLine.Order.OrderCode, b.OrderLine.ProductVariant.Product.ProductCode,
                b.OrderLine.ProductVariant.Color, b.OrderLine.ProductVariant.SizeCode, b.FinishedQty, b.InspectorId, b.Inspector.FullName,
                b.EditDeadline, false, b.RowVer))
            .ToPagedAsync(query, ct);

        var now = _clock.Now;
        var items = page.Items.Select(i => i with { CanEdit = now <= i.EditDeadline || _guard.IsManager }).ToList();
        return Result<PagedResult<QcBatchDto>>.Success(page with { Items = items });
    }

    public async Task<Result<OnLineQtyDto>> GetOnLineAsync(int sewingTeamId, int orderLineId, CancellationToken ct) =>
        Result<OnLineQtyDto>.Success(await ComputeOnLineAsync(sewingTeamId, orderLineId, null, ct));

    public async Task<Result<int>> CreateAsync(CreateQcBatchDto dto, CancellationToken ct)
    {
        var shift = CodeText.ToCode(dto.ShiftCode);
        if (!ShiftCodes.All.Contains(shift))
            return Result<int>.Failure(ErrorCodes.OutputInvalid, "Ca làm việc không hợp lệ.", ResultKind.Validation);
        if (dto.FinishedQty <= 0)
            return Result<int>.Failure(ErrorCodes.OutputInvalid, Messages.OutputQtyInvalid, ResultKind.Validation);
        if (dto.WorkDate > _clock.Today)
            return Result<int>.Failure(ErrorCodes.OutputInvalid, Messages.QcDateFuture, ResultKind.Validation);
        if (await _guard.IsPeriodLockedAsync(dto.WorkDate, ct))
            return Fail.As<int>(_guard.PeriodLockedFailure(dto.WorkDate));

        var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(
            t => t.TeamId == dto.SewingTeamId && t.TeamType == TeamTypes.Sewing && t.Status == RecordStatus.Active, ct);
        if (team is null) return Fail.NotFound<int>();

        var closed = OrderStatuses.Closed.ToArray();
        var line = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrderLineId == dto.OrderLineId && !closed.Contains(l.Order.Status), ct);
        if (line is null)
            return Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        var duplicate = await Batches.AsNoTracking().AnyAsync(b => b.WorkDate == dto.WorkDate && b.ShiftCode == shift
            && b.BatchTime == dto.BatchTime && b.SewingTeamId == dto.SewingTeamId && b.OrderLineId == dto.OrderLineId, ct);
        if (duplicate) return Result<int>.Failure(ErrorCodes.QcBatchDuplicate, Messages.QcBatchDuplicate);

        var onLine = await ComputeOnLineAsync(dto.SewingTeamId, dto.OrderLineId, null, ct);
        if (dto.FinishedQty > onLine.OnLine)
            return Result<int>.Failure(ErrorCodes.QcExceedsOnLine, string.Format(Messages.QcExceedsOnLine, onLine.OnLine));   // MSG19

        var batch = new QcBatch
        {
            WorkDate = dto.WorkDate, ShiftCode = shift, BatchTime = dto.BatchTime, SewingTeamId = dto.SewingTeamId,
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
            return Result.Failure(ErrorCodes.OutputInvalid, Messages.OutputQtyInvalid, ResultKind.Validation);
        var batch = await Batches.FirstOrDefaultAsync(b => b.QcBatchId == qcBatchId, ct);
        if (batch is null) return Fail.NotFound();

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var window = _guard.CheckEditWindow(batch.EditDeadline, reason);
        if (window is not null) return window;
        if (await _guard.IsPeriodLockedAsync(batch.WorkDate, ct)) return _guard.PeriodLockedFailure(batch.WorkDate);

        // Trần tính trên chính đợt này đã được trừ ra, để sửa giảm / tăng nhẹ vẫn hợp lệ.
        var onLine = await ComputeOnLineAsync(batch.SewingTeamId, batch.OrderLineId, qcBatchId, ct);
        if (dto.FinishedQty > onLine.OnLine)
            return Result.Failure(ErrorCodes.QcExceedsOnLine, string.Format(Messages.QcExceedsOnLine, onLine.OnLine));

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
        if (batch is null) return Fail.NotFound();

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var window = _guard.CheckEditWindow(batch.EditDeadline, reason);
        if (window is not null) return window;
        if (await _guard.IsPeriodLockedAsync(batch.WorkDate, ct)) return _guard.PeriodLockedFailure(batch.WorkDate);

        _uow.Repository<QcBatch>().SetOriginalRowVersion(batch, dto.RowVer);
        _audit.Add(new AuditEntry(nameof(QcBatch), qcBatchId, AuditActions.Delete, Snapshot(batch), null, reason));
        _uow.Repository<QcBatch>().Remove(batch);
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.QcBatchDeleted);
    }

    /// <summary>Hàng đang có trên chuyền = BTP tổ đã nhận − Σ QC đạt (trừ đợt đang sửa) − Σ Scrap chưa hủy.</summary>
    private async Task<OnLineQtyDto> ComputeOnLineAsync(int teamId, int orderLineId, int? exceptBatchId, CancellationToken ct)
    {
        var received = await _uow.Repository<VwTeamPieceHolding>().Query().AsNoTracking()
            .Where(h => h.SewingTeamId == teamId && h.OrderLineId == orderLineId)
            .Select(h => (int?)h.ReceivedFromWarehouse).FirstOrDefaultAsync(ct) ?? 0;
        var finished = await Batches.AsNoTracking()
            .Where(b => b.SewingTeamId == teamId && b.OrderLineId == orderLineId
                        && (exceptBatchId == null || b.QcBatchId != exceptBatchId))
            .SumAsync(b => (int?)b.FinishedQty, ct) ?? 0;
        var scrap = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.SewingTeamId == teamId && d.OrderLineId == orderLineId && d.DefectType == DefectTypes.Scrap
                        && d.Status != DefectStatuses.Cancelled)
            .SumAsync(d => (int?)d.Quantity, ct) ?? 0;
        return new OnLineQtyDto(teamId, orderLineId, received, finished, scrap, Math.Max(0, received - finished - scrap));
    }

    private static object Snapshot(QcBatch b) =>
        new { b.WorkDate, b.ShiftCode, b.BatchTime, b.SewingTeamId, b.OrderLineId, b.FinishedQty };
}
