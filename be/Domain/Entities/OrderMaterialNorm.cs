using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Định mức NVL cho 1 sản phẩm (vải theo chi tiết, phụ liệu theo sản phẩm)
/// </summary>
public partial class OrderMaterialNorm
{
    public int OrderMaterialNormId { get; set; }

    public int OrderMaterialId { get; set; }

    public int ProductId { get; set; }

    public string ComponentName { get; set; } = null!;

    public string? GarmentColor { get; set; }

    public string? SizeCode { get; set; }

    public decimal QtyPerPiece { get; set; }

    public decimal WastagePercent { get; set; }

    public virtual OrderMaterial OrderMaterial { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
