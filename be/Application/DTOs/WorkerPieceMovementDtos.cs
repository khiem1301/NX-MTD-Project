using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record WorkerPieceMovementDto(
    int WorkerPieceMovementId, string MovementType, int SewingTeamId, string TeamName, int WorkerId, string WorkerCode,
    string WorkerName, int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode,
    int? OperationId, string? OperationName, int Quantity, int? ConfirmedQty, string? DiscrepancyReason,
    string? RetrievalReason, string ConfirmMode, string? OnBehalfReason, string Status, string? RejectReason,
    DateTime CreatedAt, DateTime? ConfirmedAt);

/// <summary>BTP tổ đang giữ của một dòng đơn, đã trừ số đang chờ công nhân xác nhận nhận.</summary>
public sealed record TeamHoldingLineDto(
    int OrderLineId, string OrderCode, string ProductCode, string Color, string SizeCode, int HeldByTeam,
    int PendingHandoverQty, int AvailableQty);

/// <summary>BTP một công nhân đang giữ (đã nhận − đã trả lại), đã trừ số đang chờ xác nhận thu hồi.</summary>
public sealed record WorkerHoldingLineDto(
    int WorkerId, string WorkerCode, string WorkerName, int OrderLineId, string OrderCode, string ProductCode,
    string Color, string SizeCode, int HeldQty, int PendingRetrievalQty, int RetrievableQty);

public sealed record PieceMovementHoldingsDto(
    IReadOnlyList<TeamHoldingLineDto> Team, IReadOnlyList<WorkerHoldingLineDto> Workers);

/// <param name="MovementType">HANDOVER (giao) hoặc RETRIEVAL (thu hồi).</param>
/// <param name="RetrievalReason">Bắt buộc khi thu hồi: OPERATION_CHANGED, WORKER_ABSENT, STYLE_FINISHED.</param>
public sealed record CreateWorkerMovementDto(string MovementType, int WorkerId, int OrderLineId, int? OperationId,
    int Quantity, string? RetrievalReason);

public sealed record ConfirmWorkerMovementDto(int ConfirmedQty, string? DiscrepancyReason);

/// <summary>Tổ trưởng xác nhận hộ công nhân (UC54): bắt buộc lý do.</summary>
public sealed record ConfirmOnBehalfDto(int ConfirmedQty, string? DiscrepancyReason, string OnBehalfReason);

public sealed record WorkerMovementQuery : PageQuery
{
    public string? Status { get; init; }
    public string? MovementType { get; init; }
    public int? WorkerId { get; init; }
    public int? OrderId { get; init; }
}
