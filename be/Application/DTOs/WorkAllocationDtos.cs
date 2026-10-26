using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Một lần giao / thu hồi việc với công nhân (WorkAllocation): số lần làm công đoạn, kèm BTP nếu công đoạn dùng BTP.</summary>
public sealed record WorkAllocationDto(
    int WorkAllocationId, string MovementType, int SewingTeamId, string TeamName, int WorkerId, string WorkerCode,
    string WorkerName, int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode,
    int OperationId, string OperationCode, string OperationName, bool UsesSemiFinished, int Quantity, int PieceQty,
    int? ConfirmedQty, int? ConfirmedPieceQty, string? DiscrepancyReason, string? RetrievalReason, string ConfirmMode,
    string? OnBehalfReason, string Status, string? RejectReason, DateTime CreatedAt, DateTime? ConfirmedAt);

/// <summary>BTP (bộ đủ) tổ đang giữ của một dòng đơn, đã trừ BTP đang chờ công nhân xác nhận nhận.</summary>
public sealed record TeamHoldingLineDto(
    int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode, int HeldByTeam,
    int PendingPieceQty, int AvailablePieceQty);

/// <summary>Tiến độ chia việc của một công đoạn × dòng đơn (vw_OperationAllocationProgress).</summary>
public sealed record OperationProgressDto(
    int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode, int OperationId,
    string OperationCode, string OperationName, string? GroupName, bool UsesSemiFinished, int RequiredQty,
    int AllocatedQty, int PendingQty, int UnallocatedQty, int OutputQty);

/// <summary>Số lần một công nhân đang được giao ở một công đoạn × dòng đơn; còn thu hồi được = đang giữ − chờ thu hồi − đã báo sản lượng.</summary>
public sealed record WorkerAllocationLineDto(
    int WorkerId, string WorkerCode, string WorkerName, int OrderLineId, int OperationId, int HeldQty,
    int PendingRetrievalQty, int OutputQty, int RetrievableQty);

public sealed record WorkAllocationBoardDto(
    IReadOnlyList<TeamHoldingLineDto> Team, IReadOnlyList<OperationProgressDto> Operations,
    IReadOnlyList<WorkerAllocationLineDto> Workers);

/// <param name="MovementType">HANDOVER (giao việc) hoặc RETRIEVAL (thu hồi).</param>
/// <param name="PieceQty">BTP giao kèm / trả lại; để trống = điền sẵn bằng Quantity nếu công đoạn dùng BTP, 0 nếu không.</param>
/// <param name="RetrievalReason">Bắt buộc khi thu hồi: OPERATION_CHANGED, WORKER_ABSENT, STYLE_FINISHED.</param>
public sealed record CreateWorkAllocationDto(string MovementType, int WorkerId, int OrderLineId, int OperationId,
    int Quantity, int? PieceQty, string? RetrievalReason);

/// <param name="ConfirmedPieceQty">Để trống = theo số trên phiếu (hoặc tỉ lệ nếu số lần xác nhận ít hơn).</param>
public sealed record ConfirmWorkAllocationDto(int ConfirmedQty, int? ConfirmedPieceQty, string? DiscrepancyReason);

/// <summary>Tổ trưởng xác nhận hộ công nhân (UC54): bắt buộc lý do.</summary>
public sealed record ConfirmOnBehalfDto(int ConfirmedQty, int? ConfirmedPieceQty, string? DiscrepancyReason, string OnBehalfReason);

public sealed record WorkAllocationQuery : PageQuery
{
    public string? Status { get; init; }
    public string? MovementType { get; init; }
    public int? WorkerId { get; init; }
    public int? OrderId { get; init; }
}
