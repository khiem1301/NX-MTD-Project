using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Vai trò (9 vai trò, gồm Tổ trưởng và Tổ phó)
/// </summary>
public partial class Role
{
    public int RoleId { get; set; }

    public string RoleCode { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public bool IsProduction { get; set; }

    public virtual ICollection<AnnouncementTarget> AnnouncementTargets { get; set; } = new List<AnnouncementTarget>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
