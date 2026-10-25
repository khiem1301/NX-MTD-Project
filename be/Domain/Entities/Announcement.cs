using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Thông báo nhanh
/// </summary>
public partial class Announcement
{
    public int AnnouncementId { get; set; }

    public string Content { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public string TargetScope { get; set; } = null!;

    public int SenderId { get; set; }

    public DateTime SentAt { get; set; }

    public virtual ICollection<AnnouncementTarget> AnnouncementTargets { get; set; } = new List<AnnouncementTarget>();

    public virtual Employee Sender { get; set; } = null!;
}
