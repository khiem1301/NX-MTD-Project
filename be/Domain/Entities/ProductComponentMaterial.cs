using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Định mức NVL theo biến thể × thành phần: NVL, màu NVL, lượng / sản phẩm, hao hụt, kích thước chi tiết
/// </summary>
public partial class ProductComponentMaterial
{
    public int ProductComponentMaterialId { get; set; }

    public int ProductId { get; set; }

    public int ProductVariantId { get; set; }

    public int ProductComponentId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialColor { get; set; } = null!;

    public decimal QtyPerGarment { get; set; }

    public decimal WastagePercent { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public string? SpecNote { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual ProductComponent ProductComponent { get; set; } = null!;

    public virtual ProductVariant ProductVariant { get; set; } = null!;
}
