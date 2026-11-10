using System;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Cache nhân viên từ Ronald Jack Pro (chỉ mã chấm công + tên) để gán mã / tạo NV trên web.
/// </summary>
public partial class RjEmployeeSnapshot
{
    public int AttendanceCode { get; set; }

    public string FullName { get; set; } = null!;

    public string? RjEmployeeCode { get; set; }

    public DateTime SyncedAt { get; set; }
}
