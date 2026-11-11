using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Số mét từng vải của bàn cắt (DEV-ALL-93): cắt cả bộ thì đủ mọi vải chi tiết của màu áo; cắt bù / cắt lẻ thì vải của chi tiết.
/// </summary>
public partial class CuttingLayFabric
{
    public int CuttingLayFabricId { get; set; }

    public int CuttingLayId { get; set; }

    public int MaterialId { get; set; }

    /// <summary>null = bàn cũ chưa có chiều dài bàn cắt.</summary>
    public decimal? UsedM { get; set; }

    public virtual CuttingLay CuttingLay { get; set; } = null!;

    public virtual Material Material { get; set; } = null!;
}
