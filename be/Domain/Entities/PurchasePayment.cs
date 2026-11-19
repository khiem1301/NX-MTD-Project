namespace NX_MTD.Domain.Entities;

/// <summary>
/// Một lần Kế toán thanh toán đơn mua (DEV-ALL-117). Không xóa; sửa được trong 24 giờ kể từ <see cref="CreatedAt"/>, bắt buộc có lý do.
/// </summary>
public partial class PurchasePayment
{
    public int PurchasePaymentId { get; set; }

    public int PurchaseOrderId { get; set; }

    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? PaymentNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? EditReason { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
