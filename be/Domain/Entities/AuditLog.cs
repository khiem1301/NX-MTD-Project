using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Nhật ký thay đổi
/// </summary>
public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public string TableName { get; set; } = null!;

    public long RecordId { get; set; }

    public string Action { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? Reason { get; set; }

    public int? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual Employee? ChangedByNavigation { get; set; }
}
