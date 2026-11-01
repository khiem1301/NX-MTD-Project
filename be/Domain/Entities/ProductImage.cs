using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Ảnh phụ của mã hàng (tối đa 10, SortOrder 1-10). Ảnh chính là Product.SampleImageUrl.
/// </summary>
public partial class ProductImage
{
    public int ProductImageId { get; set; }

    public int ProductId { get; set; }

    public string ImageKey { get; set; } = null!;

    public byte SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;
}
