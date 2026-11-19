namespace NX_MTD.Domain.Entities;

/// <summary>
/// Một lần thanh toán của đơn hàng khách (DEV-ALL-137). <see cref="Amount"/> dương = khách trả mình, âm = mình trả khách (đền hợp đồng).
/// Không xóa; sửa được trong 24 giờ kể từ <see cref="CreatedAt"/>, bắt buộc có lý do.
/// </summary>
public partial class OrderPayment
{
    public int OrderPaymentId { get; set; }

    public int OrderId { get; set; }

    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? PaymentNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? EditReason { get; set; }

    public virtual CustomerOrder Order { get; set; } = null!;
}
