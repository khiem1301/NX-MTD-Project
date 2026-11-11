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

    /// <summary>Số lượng sản phẩm (cắt bù: chi tiết) sau khi cắt, tổ cắt nhập thẳng (DEV-ALL-90).</summary>
    public int ActualQty { get; set; }

    public virtual CuttingLay CuttingLay { get; set; } = null!;

    public virtual OrderLine OrderLine { get; set; } = null!;

    public virtual ProductComponentMaterial? ProductComponentMaterial { get; set; }
}
