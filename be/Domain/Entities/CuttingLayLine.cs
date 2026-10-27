using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Số cắt theo size của bàn cắt
/// </summary>
public partial class CuttingLayLine
{
    public int CuttingLayLineId { get; set; }

    public int CuttingLayId { get; set; }

    public int OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public short RatioValue { get; set; }

    public int CalculatedQty { get; set; }

    public int ActualQty { get; set; }

    public string? AdjustReason { get; set; }

    public virtual CuttingLay CuttingLay { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual ProductComponentMaterial? ProductComponentMaterial { get; set; }
}
