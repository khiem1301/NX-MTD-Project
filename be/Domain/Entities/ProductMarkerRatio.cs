namespace NX_MTD.Domain.Entities;

/// <summary>Tỉ lệ size của bìa: số sản phẩm size này trên một lớp vải (DEV-ALL-85).</summary>
public partial class ProductMarkerRatio
{
    public int ProductMarkerId { get; set; }

    public string SizeCode { get; set; } = null!;

    public short PiecesPerPly { get; set; }

    public virtual ProductMarker ProductMarker { get; set; } = null!;
}
