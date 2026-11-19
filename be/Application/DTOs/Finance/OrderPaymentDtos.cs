using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// DEV-ALL-118: Kế toán chốt giá trị đơn hàng khách (có thể âm = mình phải đền hợp đồng) và ghi nhận thanh toán nhiều lần.
// Số tiền dương = khách trả mình, âm = mình trả khách. Không có đơn giá gia công (GBR-02): giá trị chốt do Kế toán nhập.

public sealed record OrderPaymentQuery : PageQuery
{
    /// <summary>"NotSet" (chưa chốt) | "Unpaid" | "Partial" | "Settled"; trống = tất cả.</summary>
    public string? State { get; init; }
}

public sealed record OrderPaymentListItemDto(
    int OrderId, string OrderCode, string CustomerName, string OrderStatus, decimal? FinalAmount, decimal PaidAmount,
    decimal? Remaining, string State);

/// <summary>Một lần thanh toán. <c>CanEdit</c>: trong 24 giờ kể từ lúc nhập.</summary>
public sealed record OrderPaymentLineDto(
    int OrderPaymentId, DateOnly PaymentDate, decimal Amount, string PaymentMethod, string? PaymentNote,
    DateTime CreatedAt, DateTime? UpdatedAt, string? EditReason, bool CanEdit);

/// <summary>Remaining = giá trị chốt − đã thanh toán (dương: khách còn phải trả; âm: mình còn phải trả khách).</summary>
public sealed record OrderPaymentDto(
    int OrderId, string OrderCode, string CustomerName, string OrderStatus, decimal? FinalAmount, string? FinalAmountReason,
    decimal PaidAmount, decimal? Remaining, string State, byte[] RowVer, IReadOnlyList<OrderPaymentLineDto> Payments,
    IReadOnlyList<AuditLogDto> History);

/// <summary>Giá trị chốt cả đơn (cho phép âm hoặc 0); bắt buộc lý do. Tổng đã thanh toán phải còn nằm giữa 0 và giá trị mới.</summary>
public sealed record SetFinalAmountDto(decimal Amount, string Reason, byte[] RowVer);

public sealed record AddOrderPaymentDto(DateOnly PaymentDate, decimal Amount, string PaymentMethod, string? PaymentNote, byte[] RowVer);

public sealed record UpdateOrderPaymentDto(
    DateOnly PaymentDate, decimal Amount, string PaymentMethod, string? PaymentNote, string Reason, byte[] RowVer);
