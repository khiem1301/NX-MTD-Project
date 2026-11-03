using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IOrderMaterialService
{
    Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Bảng NVL của đơn có tìm kiếm, lọc, phân trang; GetByOrderAsync vẫn trả đủ dòng cho các form.</summary>
    Task<Result<PagedResult<OrderMaterialDto>>> GetPagedAsync(OrderMaterialPageQuery query, CancellationToken ct);
    /// <summary>Mã hàng trong đơn, mỗi mã kèm các NVL định mức để khai báo cho đơn.</summary>
    Task<Result<IReadOnlyList<OrderProductMaterialsDto>>> GetProductMaterialsAsync(int orderId, CancellationToken ct);
    Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct);
    Task<Result<IReadOnlyList<OrderMaterialDto>>> CreateBatchAsync(CreateOrderMaterialBatchDto dto, CancellationToken ct);
    Task<Result<OrderMaterialDto>> UpdateAsync(int orderMaterialId, UpdateOrderMaterialDto dto, CancellationToken ct);
    Task<Result> DeleteAsync(int orderMaterialId, CancellationToken ct);
}
