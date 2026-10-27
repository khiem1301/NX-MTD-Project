using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Thông báo người dùng (giữ 90 ngày)
/// </summary>
public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientId { get; set; }

    public string SourceType { get; set; } = null!;

    public string? RefTable { get; set; }

    public long? RefId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool? IsRead { get; set; }

    public string PushStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Employee Recipient { get; set; } = null!;
}
