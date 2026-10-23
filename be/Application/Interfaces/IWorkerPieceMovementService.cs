using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC53 / UC54 / UC60: giao và thu hồi BTP giữa tổ trưởng may và công nhân.</summary>
public interface IWorkerPieceMovementService
{
    /// <param name="teamId">Có giá trị = chỉ phiếu của tổ này (tổ trưởng / tổ phó).</param>
    /// <param name="workerId">Có giá trị = chỉ phiếu của công nhân này.</param>
    Task<Result<PagedResult<WorkerPieceMovementDto>>> GetPagedAsync(WorkerMovementQuery query, int? teamId, int? workerId,
        CancellationToken ct);

    /// <summary>BTP tổ đang giữ và BTP từng công nhân đang giữ (để chọn khi giao / thu hồi).</summary>
    Task<Result<PieceMovementHoldingsDto>> GetHoldingsAsync(int teamId, int? orderId, CancellationToken ct);

    Task<Result<WorkerPieceMovementDto>> CreateAsync(CreateWorkerMovementDto dto, int teamId, CancellationToken ct);

    /// <summary>Công nhân tự xác nhận (UC60); lệch số phải có lý do.</summary>
    Task<Result> ConfirmByWorkerAsync(int movementId, ConfirmWorkerMovementDto dto, int workerId, CancellationToken ct);

    Task<Result> RejectByWorkerAsync(int movementId, ReasonDto dto, int workerId, CancellationToken ct);

    /// <summary>Tổ trưởng xác nhận hộ (UC54): bắt buộc lý do.</summary>
    Task<Result> ConfirmOnBehalfAsync(int movementId, ConfirmOnBehalfDto dto, int teamId, CancellationToken ct);
}
