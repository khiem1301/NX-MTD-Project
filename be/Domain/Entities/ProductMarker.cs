namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bìa cắt của đơn (DEV-ALL-85): phía trên giác theo tỉ lệ size của đơn. MarkerNo là nhóm bìa của mã hàng, MarkerCode là mã bìa
/// trong nhóm (1A, 1B...). Chỉ dùng để ước số mét vải khi lập lệnh cắt; bàn cắt không gắn bìa.
/// </summary>
public partial class ProductMarker
{
    public int ProductMarkerId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public byte MarkerNo { get; set; }

    public string MarkerCode { get; set; } = null!;

    public decimal LengthM { get; set; }

    /// <summary>Khổ bìa (m), tối đa 3,5 m (DEV-ALL-102).</summary>
    public decimal WidthM { get; set; }

    public string Status { get; set; } = null!;

    public virtual CustomerOrder Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductMarkerRatio> ProductMarkerRatios { get; set; } = new List<ProductMarkerRatio>();
}
