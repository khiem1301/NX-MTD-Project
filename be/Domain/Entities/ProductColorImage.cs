namespace NX_MTD.Domain.Entities;

/// <summary>Ảnh theo màu áo của mã hàng: mỗi màu 1-3 ảnh (SortOrder 1-3), DEV-ALL-71.</summary>
public partial class ProductColorImage
{
    public int ProductColorImageId { get; set; }

    public int ProductColorId { get; set; }

    public string ImageKey { get; set; } = null!;

    public byte SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ProductColor ProductColor { get; set; } = null!;
}
