using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Tệp / ảnh đính kèm
/// </summary>
public partial class Attachment
{
    public long AttachmentId { get; set; }

    public string OwnerType { get; set; } = null!;

    public int OwnerId { get; set; }

    public string Purpose { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public bool IsCurrent { get; set; }

    /// <summary>Lúc gỡ ảnh (IsCurrent = 0); job dọn tệp xóa file và dòng sau 15 ngày.</summary>
    public DateTime? RemovedAt { get; set; }

    public DateTime UploadedAt { get; set; }
}
