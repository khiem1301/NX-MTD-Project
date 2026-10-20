using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IOrderMaterialService
{
    Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct);
    Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct);
    Task<Result<OrderMaterialDto>> UpdateAsync(int orderMaterialId, UpdateOrderMaterialDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int orderMaterialId, CancellationToken ct);
}
