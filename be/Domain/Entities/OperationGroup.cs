using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Công đoạn to (nhóm công đoạn)
/// </summary>
public partial class OperationGroup
{
    public int OperationGroupId { get; set; }

    public int ProductId { get; set; }

    public string GroupName { get; set; } = null!;

    public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();

    public virtual Product Product { get; set; } = null!;
}
