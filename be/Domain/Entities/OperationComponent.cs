using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Công đoạn làm trên chi tiết vải (PANEL) nào (DEV-ALL-86); vải của chi tiết tra qua ProductComponentMaterial
/// </summary>
public partial class OperationComponent
{
    public int OperationId { get; set; }

    public int ProductComponentId { get; set; }

    public int ProductId { get; set; }

    public virtual Operation Operation { get; set; } = null!;

    public virtual ProductComponent ProductComponent { get; set; } = null!;
}
