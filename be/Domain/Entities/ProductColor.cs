namespace NX_MTD.Domain.Entities;

/// <summary>
/// Màu áo của mã hàng: nơi duy nhất lưu tên màu. Biến thể (màu × size), vải của bìa theo màu và ảnh theo màu
/// trỏ về đây bằng ProductColorId (DEV-ALL-71).
/// </summary>
public partial class ProductColor
{
    public int ProductColorId { get; set; }

    public int ProductId { get; set; }

    public string Color { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductColorImage> ProductColorImages { get; set; } = new List<ProductColorImage>();

    public virtual ICollection<ProductMarkerFabric> ProductMarkerFabrics { get; set; } = new List<ProductMarkerFabric>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
