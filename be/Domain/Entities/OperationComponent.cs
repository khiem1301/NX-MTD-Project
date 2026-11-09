using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Công đoạn làm trên chi tiết vải nào, vải nào (DEV-ALL-86): chi tiết PANEL × NVL loại vải có trong định mức của chi tiết
/// </summary>
public partial class OperationComponent
{
    public int OperationId { get; set; }

    public int ProductComponentId { get; set; }

    public int ProductId { get; set; }

    public int MaterialId { get; set; }

    public virtual Operation Operation { get; set; } = null!;

    public virtual Material Material { get; set; } = null!;

    public virtual ProductComponent ProductComponent { get; set; } = null!;
}
