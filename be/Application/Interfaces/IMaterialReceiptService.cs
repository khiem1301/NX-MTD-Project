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

    /// <summary>Quản đốc xác nhận: tăng tồn kho NVL theo số đạt.</summary>
    Task<Result> ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct);

    Task<Result> ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct);
}
