namespace NX_MTD.Domain.Entities;

/// <summary>Chi tiết nằm trên nhóm bìa nào của mã hàng (DEV-ALL-85); dùng ước mét cho dòng cắt lẻ / cắt bù.</summary>
public partial class ProductMarkerComponent
{
    public int ProductId { get; set; }

    public byte MarkerNo { get; set; }

    public int ProductComponentId { get; set; }

    public virtual ProductComponent ProductComponent { get; set; } = null!;
}
