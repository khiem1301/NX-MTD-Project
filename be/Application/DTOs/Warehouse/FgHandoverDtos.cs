using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record FgHandoverListItemDto(
    int FgHandoverId, string HandoverCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    int InspectorId, string InspectorName, DateTime HandedAt, string Status, string? ConfirmedByName,
    DateTime? ConfirmedAt, int TotalHandedQty);

public sealed record FgHandoverLineDto(
    int FgHandoverLineId, int OrderLineId, string Color, string SizeCode, int HandedQty, int? ConfirmedQty,
    string? DiscrepancyReason);

public sealed record FgHandoverDto(
    int FgHandoverId, string HandoverCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    int InspectorId, string InspectorName, DateTime HandedAt, string Status, string? ConfirmedByName,
    DateTime? ConfirmedAt, string? RejectReason, IReadOnlyList<FgHandoverLineDto> Lines);

/// <summary>Số còn được bàn giao của từng dòng đơn = số QC đạt − đã bàn giao (chờ nhận + đã nhận).</summary>
public sealed record FgHandoverableLineDto(int OrderLineId, int ProductId, string ProductCode, string Color,
    string SizeCode, int QcQty, int HandedQty, int RemainingQty);

public sealed record CreateFgHandoverDto(int OrderId, int ProductId, IReadOnlyList<CreateHandoverLineDto> Lines);

public sealed record ConfirmFgHandoverLineDto(int FgHandoverLineId, int ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmFgHandoverDto(IReadOnlyList<ConfirmFgHandoverLineDto> Lines);

public sealed record FgHandoverQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
