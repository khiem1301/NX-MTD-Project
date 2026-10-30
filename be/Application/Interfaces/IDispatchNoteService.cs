using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC45–UC47: phiếu xuất hàng — Quản đốc lập, Thủ kho xuất / từ chối, Kế toán xác nhận khách nhận.</summary>
public interface IDispatchNoteService
{
    Task<Result<PagedResult<DispatchNoteListItemDto>>> GetPagedAsync(DispatchNoteQuery query, CancellationToken ct);
    Task<Result<DispatchNoteDto>> GetByIdAsync(int dispatchNoteId, CancellationToken ct);
    Task<Result<IReadOnlyList<DispatchableLineDto>>> GetDispatchableAsync(int orderId, CancellationToken ct);
    Task<Result<DispatchNoteDto>> CreateAsync(CreateDispatchNoteDto dto, CancellationToken ct);

    /// <summary>Quản đốc hủy phiếu chưa xuất; số giữ chỗ được nhả.</summary>
    Task<Result> CancelAsync(int dispatchNoteId, CancellationToken ct);

    /// <summary>Thủ kho xuất hàng (cần ảnh phiếu có Quản đốc ký): trừ kho thành phẩm, phiếu PENDING sang IN_DELIVERY.</summary>
    Task<Result> ExportAsync(int dispatchNoteId, int employeeId, CancellationToken ct);

    Task<Result> RejectAsync(int dispatchNoteId, ReasonDto dto, int employeeId, CancellationToken ct);

    /// <summary>Kế toán xác nhận khách đã nhận (ngày nhận + số nhận từng dòng).</summary>
    Task<Result> ConfirmDeliveryAsync(int dispatchNoteId, ConfirmDeliveryDto dto, CancellationToken ct);
}
