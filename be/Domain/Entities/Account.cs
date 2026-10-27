using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Tài khoản đăng nhập (1–1 nhân viên)
/// </summary>
public partial class Account
{
    public int AccountId { get; set; }

    public int EmployeeId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public bool IsTemporaryPassword { get; set; }

    public Guid SecurityStamp { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? LockedAt { get; set; }

    public string? LockReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
