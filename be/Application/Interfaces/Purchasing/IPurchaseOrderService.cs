using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IPurchaseOrderService
{
    Task<Result<PagedResult<PurchaseOrderListItemDto>>> GetPagedAsync(PurchaseOrderQuery query, CancellationToken ct);
    Task<Result<PurchaseOrderDto>> GetByIdAsync(int purchaseOrderId, CancellationToken ct);

    /// <summary>Quản đốc tạo đơn mua NVL; có thể không gắn đơn hàng (mua dự trữ dùng cho nhiều đơn).</summary>
    Task<Result<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderDto dto, CancellationToken ct);
}
