using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-07 — nhà cung cấp (UC14): Admin thêm / sửa / ngừng hợp tác; Quản đốc chỉ chọn nhà cung cấp đang hợp tác.</summary>
public interface ISupplierService
{
    Task<Result<PagedResult<SupplierDto>>> ListAsync(SupplierQuery query, CancellationToken ct);
    Task<Result<SupplierDetailDto>> GetAsync(int supplierId, CancellationToken ct);
    Task<Result<int>> CreateAsync(SaveSupplierDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int supplierId, SaveSupplierDto dto, CancellationToken ct);
    Task<Result> DiscontinueAsync(int supplierId, CancellationToken ct);
    Task<Result> ReactivateAsync(int supplierId, CancellationToken ct);

    /// <summary>Chỉ nhà cung cấp đang hợp tác, sắp theo tên (ô chọn khi đặt hàng).</summary>
    Task<Result<IReadOnlyList<SupplierOptionDto>>> GetActiveOptionsAsync(CancellationToken ct);
}

/// <summary>
/// MI-07 — đơn mua NVL (UC23, UC34), nối tiếp bước tạo đơn của Thái (<see cref="IPurchaseOrderService"/>):
/// Quản đốc chọn nhà cung cấp và chốt đặt hàng (hoặc hủy), Kế toán xác nhận thanh toán kèm ảnh chứng từ hoặc trả lại.
/// Phân quyền theo vai trò ở controller; service chỉ kiểm trạng thái đơn.
/// </summary>
public interface IPurchaseOrderFlowService
{
    Task<Result<PagedResult<PurchaseOrderFlowListItemDto>>> ListAsync(PurchaseOrderFlowQuery query, CancellationToken ct);
    Task<Result<PurchaseOrderFlowDto>> GetAsync(int purchaseOrderId, CancellationToken ct);

    // ---------- Quản đốc ----------

    /// <summary>PENDING_FINALISATION → ORDERED: chọn nhà cung cấp đang hợp tác, ngày đặt, chốt số lượng + đơn giá.</summary>
    Task<Result> PlaceAsync(int purchaseOrderId, PlacePurchaseOrderDto dto, CancellationToken ct);

    /// <summary>PENDING_FINALISATION → REJECTED (bắt buộc lý do). Đã đặt hàng thì không hủy được.</summary>
    Task<Result> CancelAsync(int purchaseOrderId, CancelPurchaseOrderDto dto, CancellationToken ct);

    /// <summary>Sau khi Kế toán trả lại: sửa đơn giá rồi gửi thanh toán lại (RETURNED → UNPAID).</summary>
    Task<Result> RepriceAsync(int purchaseOrderId, RepricePurchaseOrderDto dto, CancellationToken ct);

    // ---------- Kế toán ----------

    /// <summary>UNPAID → PAID: ngày, số tiền, hình thức; ảnh chứng từ tùy chọn (Attachment). Không sửa nhà cung cấp / số lượng (GBR-18).</summary>
    Task<Result> ConfirmPaymentAsync(int purchaseOrderId, ConfirmPaymentDto dto, CancellationToken ct);

    /// <summary>UNPAID → RETURNED (bắt buộc lý do): trả lại Quản đốc sửa.</summary>
    Task<Result> ReturnPaymentAsync(int purchaseOrderId, ReturnPaymentDto dto, CancellationToken ct);
}
