namespace NX_MTD.Domain.Entities;

/// <summary>Một bìa cắt của mã hàng ở một size: dài, rộng. MarkerNo là số bìa; một bìa tính cho một sản phẩm (DEV-ALL-42).</summary>
public partial class ProductMarker
{
    public int ProductMarkerId { get; set; }

    public int ProductId { get; set; }

    public byte MarkerNo { get; set; }

    public string SizeCode { get; set; } = null!;

    public decimal LengthCm { get; set; }

    public decimal WidthCm { get; set; }

    public virtual Product Product { get; set; } = null!;
}
