using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC22: Admin xem AuditLog (lọc theo bảng, hành động, người thực hiện, khoảng ngày, mã bản ghi).</summary>
public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IUnitOfWork _uow;

    public AuditLogQueryService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<PagedResult<AuditLogRowDto>>> ListAsync(AuditLogQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim();
        var from = q.FromDate?.ToDateTime(TimeOnly.MinValue);
        var to = q.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var page = await _uow.Repository<AuditLog>().Query().AsNoTracking()
            .Where(a => string.IsNullOrEmpty(q.TableName) || a.TableName == q.TableName)
            .Where(a => string.IsNullOrEmpty(q.Action) || a.Action == q.Action)
            .Where(a => q.ChangedBy == null || a.ChangedBy == q.ChangedBy)
            .Where(a => q.RecordId == null || a.RecordId == q.RecordId)
            .Where(a => from == null || a.ChangedAt >= from)
            .Where(a => to == null || a.ChangedAt < to)
            .Where(a => string.IsNullOrEmpty(keyword) || (a.Reason != null && a.Reason.Contains(keyword))
                        || (a.ChangedByNavigation != null && a.ChangedByNavigation.FullName.Contains(keyword)))
            .OrderByDescending(a => a.AuditLogId)
            .Select(a => new AuditLogRowDto(a.AuditLogId, a.TableName, a.RecordId, a.Action, a.OldValues, a.NewValues, a.Reason, a.ChangedBy,
                a.ChangedByNavigation == null ? null : a.ChangedByNavigation.FullName, a.ChangedAt, a.Summary))
            .ToPagedAsync(q, ct);
        return Result<PagedResult<AuditLogRowDto>>.Success(page);
    }

    public async Task<Result<IReadOnlyList<string>>> GetTableNamesAsync(CancellationToken ct) =>
        Result<IReadOnlyList<string>>.Success(await _uow.Repository<AuditLog>().Query().AsNoTracking()
            .Select(a => a.TableName).Distinct().OrderBy(n => n).ToListAsync(ct));
}
