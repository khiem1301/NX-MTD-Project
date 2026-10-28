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

    /// <summary>Thủ kho lập phiếu xuất: chốt số xuất thực tế từng dòng (≤ số đặt) rồi gửi Quản đốc duyệt (PENDING → PENDING_APPROVAL).</summary>
    Task<Result> PrepareAsync(int dispatchNoteId, PrepareDispatchDto dto, CancellationToken ct);

    /// <summary>Quản đốc duyệt phiếu xuất (PENDING_APPROVAL → APPROVED) để Thủ kho in, chụp ảnh và xuất.</summary>
    Task<Result> ApproveAsync(int dispatchNoteId, CancellationToken ct);

    /// <summary>Quản đốc từ chối duyệt: trả phiếu về Thủ kho làm lại (PENDING_APPROVAL → PENDING), bắt buộc lý do.</summary>
    Task<Result> ReturnAsync(int dispatchNoteId, ReasonDto dto, CancellationToken ct);

    /// <summary>Quản đốc hủy phiếu chưa xuất; số giữ chỗ được nhả.</summary>
    Task<Result> CancelAsync(int dispatchNoteId, CancellationToken ct);

    /// <summary>Thủ kho xuất hàng sau khi Quản đốc duyệt (APPROVED): trừ kho thành phẩm, phiếu sang IN_DELIVERY.</summary>
    Task<Result> ExportAsync(int dispatchNoteId, int employeeId, CancellationToken ct);

    Task<Result> RejectAsync(int dispatchNoteId, ReasonDto dto, int employeeId, CancellationToken ct);

    /// <summary>Kế toán xác nhận khách đã nhận (ngày nhận + số nhận từng dòng).</summary>
    Task<Result> ConfirmDeliveryAsync(int dispatchNoteId, ConfirmDeliveryDto dto, CancellationToken ct);
}
