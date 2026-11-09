using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC22: Admin xem nhật ký thay đổi (AuditLog) của toàn hệ thống.</summary>
public interface IAuditLogQueryService
{
    Task<Result<PagedResult<AuditLogRowDto>>> ListAsync(AuditLogQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<string>>> GetTableNamesAsync(CancellationToken ct);
}
