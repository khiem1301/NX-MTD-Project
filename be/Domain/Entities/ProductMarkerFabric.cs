namespace NX_MTD.Domain.Entities;

/// <summary>Bìa nào, với màu áo nào, trải trên vải nào. Vải không đổi theo size (DEV-ALL-42).</summary>
public partial class ProductMarkerFabric
{
    public int ProductMarkerFabricId { get; set; }

    public int ProductId { get; set; }

    public byte MarkerNo { get; set; }

    public int ProductColorId { get; set; }

    public int MaterialId { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual ProductColor ProductColor { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
