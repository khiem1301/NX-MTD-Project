using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Lịch khách giao NVL của cả đơn hàng: số lần, ngày / khoảng ngày dự kiến (DEV-ALL-95).</summary>
public interface ICustomerDeliveryPlanService
{
    Task<Result<OrderCustomerDeliveryDto>> GetAsync(int orderId, CancellationToken ct);
    Task<Result<OrderCustomerDeliveryDto>> SaveAsync(int orderId, SaveOrderCustomerDeliveryDto dto, CancellationToken ct);
}
