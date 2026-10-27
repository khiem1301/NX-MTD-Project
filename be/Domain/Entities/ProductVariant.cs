using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Biến thể mã hàng: màu × size
/// </summary>
public partial class ProductVariant
{
    public int ProductVariantId { get; set; }

    public int ProductId { get; set; }

    public string Color { get; set; } = null!;

    public string SizeCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductComponentMaterial> ProductComponentMaterials { get; set; } = new List<ProductComponentMaterial>();
}
