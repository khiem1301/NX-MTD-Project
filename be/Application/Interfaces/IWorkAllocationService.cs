using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC53 / UC54 / UC60: giao và thu hồi việc (số lần làm công đoạn, kèm BTP) giữa tổ trưởng may và công nhân.</summary>
public interface IWorkAllocationService
{
    /// <param name="teamId">Có giá trị = chỉ phiếu của tổ này (tổ trưởng / tổ phó).</param>
    /// <param name="workerId">Có giá trị = chỉ phiếu của công nhân này.</param>
    Task<Result<PagedResult<WorkAllocationDto>>> GetPagedAsync(WorkAllocationQuery query, int? teamId, int? workerId,
        CancellationToken ct);

    /// <summary>Bảng chia việc: BTP tổ giữ, tiến độ từng công đoạn, số lần từng công nhân đang giữ.</summary>
    Task<Result<WorkAllocationBoardDto>> GetBoardAsync(int teamId, int? orderId, CancellationToken ct);

    Task<Result<WorkAllocationDto>> CreateAsync(CreateWorkAllocationDto dto, int teamId, CancellationToken ct);

    /// <summary>Công nhân tự xác nhận (UC60); lệch số phải có lý do.</summary>
    Task<Result> ConfirmByWorkerAsync(int allocationId, ConfirmWorkAllocationDto dto, int workerId, CancellationToken ct);

    Task<Result> RejectByWorkerAsync(int allocationId, ReasonDto dto, int workerId, CancellationToken ct);

    /// <summary>Tổ trưởng xác nhận hộ (UC54): bắt buộc lý do.</summary>
    Task<Result> ConfirmOnBehalfAsync(int allocationId, ConfirmOnBehalfDto dto, int teamId, CancellationToken ct);
}
