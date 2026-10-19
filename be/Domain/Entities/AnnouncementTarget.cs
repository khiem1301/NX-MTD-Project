using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phạm vi nhận thông báo nhanh
/// </summary>
public partial class AnnouncementTarget
{
    public int AnnouncementTargetId { get; set; }

    public int AnnouncementId { get; set; }

    public int? TeamId { get; set; }

    public int? RoleId { get; set; }

    public virtual Announcement Announcement { get; set; } = null!;

    public virtual Role? Role { get; set; }

    public virtual Team? Team { get; set; }
}
