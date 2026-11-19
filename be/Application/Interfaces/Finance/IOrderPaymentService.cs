using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>DEV-ALL-137: giá trị chốt và các lần thanh toán của đơn hàng khách (Kế toán). Không có thao tác xóa.</summary>
public interface IOrderPaymentService
{
    Task<Result<PagedResult<OrderPaymentListItemDto>>> ListAsync(OrderPaymentQuery query, CancellationToken ct);

    Task<Result<OrderPaymentDto>> GetAsync(int orderId, CancellationToken ct);

    Task<Result> SetFinalAmountAsync(int orderId, SetFinalAmountDto dto, CancellationToken ct);

    Task<Result> AddPaymentAsync(int orderId, AddOrderPaymentDto dto, CancellationToken ct);

    /// <summary>Sửa một lần thanh toán trong 24 giờ kể từ lúc nhập, bắt buộc lý do.</summary>
    Task<Result> UpdatePaymentAsync(int orderId, int paymentId, UpdateOrderPaymentDto dto, CancellationToken ct);
}
