namespace NX_MTD.Application.DTOs;

/// <summary>Một dòng lịch sử. OldValues / NewValues là chuỗi JSON nguyên văn trong DB.</summary>
public sealed record AuditLogDto(long AuditLogId, string Action, string? OldValues, string? NewValues, string? Reason,
    int? ChangedBy, string? ChangedByName, DateTime ChangedAt);
