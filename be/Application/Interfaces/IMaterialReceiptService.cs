using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IMaterialReceiptService
{
    Task<Result<PagedResult<MaterialReceiptListItemDto>>> GetPagedAsync(MaterialReceiptQuery query, CancellationToken ct);
    Task<Result<MaterialReceiptDto>> GetByIdAsync(int receiptId, CancellationToken ct);
    Task<Result<MaterialReceiptDto>> CreateAsync(SaveMaterialReceiptDto dto, CancellationToken ct);

    /// <summary>Sửa phiếu đang chờ hoặc bị trả lại; lưu xong phiếu quay về PENDING (gửi lại).</summary>
    Task<Result<MaterialReceiptDto>> UpdateAsync(int receiptId, SaveMaterialReceiptDto dto, CancellationToken ct);

    /// <summary>Phiếu bổ sung: nhận hàng giao bù theo đơn thiếu NVL đã chọn "giao bù" (UC39).</summary>
    Task<Result<MaterialReceiptDto>> CreateSupplementaryAsync(SaveSupplementaryReceiptDto dto, CancellationToken ct);

    /// <summary>Quản đốc xác nhận: tăng tồn kho NVL theo số đạt; có thiếu / lỗi thì sinh đơn SHORT_DELIVERY (MSG31).</summary>
    Task<Result> ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct);

    Task<Result> ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct);
}

/// <summary>UC08 / UC39: Quản đốc xử lý đơn SHORT_DELIVERY (hàng thiếu / lỗi khi nhập): giao bù hoặc chốt không bù.</summary>
public interface IShortDeliveryService
{
    Task<Result<PagedResult<ShortDeliveryListItemDto>>> GetPagedAsync(ShortDeliveryQuery query, CancellationToken ct);
    Task<Result<ShortDeliveryDto>> GetByIdAsync(int requestId, CancellationToken ct);
    Task<Result> ResolveAsync(int requestId, ResolveShortDeliveryDto dto, int employeeId, CancellationToken ct);
}
