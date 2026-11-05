using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record AuditLogQuery : PageQuery
{
    public string? TableName { get; init; }
    public string? Action { get; init; }
    public int? ChangedBy { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public long? RecordId { get; init; }
}

public sealed record AuditLogRowDto(long AuditLogId, string TableName, long RecordId, string Action, string? OldValues,
    string? NewValues, string? Reason, int? ChangedBy, string? ChangedByName, DateTime ChangedAt, string? Summary = null);
