using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record CuttingLayLineDto(
    int CuttingLayLineId, int OrderLineId, string Color, string SizeCode, int RatioValue, int CalculatedQty,
    int ActualQty, string? AdjustReason);

public sealed record CuttingLayDto(
    int CuttingLayId, int WorkOrderId, string WorkOrderCode, DateOnly WorkDate, string ShiftCode, int LaySequence,
    int? FabricMaterialId, string? FabricMaterialCode, string FabricColor, int PlyCount, string SizeRatio, string? Note,
    string Status, DateTime CreatedAt, IReadOnlyList<CuttingLayLineDto> Lines);

/// <summary>ActualQty bỏ trống = bằng số tính (số lớp × tỉ lệ); khác số tính thì bắt buộc AdjustReason.</summary>
public sealed record SaveLayLineDto(int OrderLineId, int RatioValue, int? ActualQty, string? AdjustReason);

public sealed record SaveCuttingLayDto(
    int WorkOrderId, DateOnly WorkDate, string ShiftCode, int? FabricMaterialId, string FabricColor, int PlyCount,
    string? Note, IReadOnlyList<SaveLayLineDto> Lines);

public sealed record CuttingLayQuery : PageQuery
{
    public int? WorkOrderId { get; init; }
    public DateOnly? WorkDate { get; init; }
}

public sealed record CutHandoverListItemDto(
    int CutHandoverId, string HandoverCode, int WorkOrderId, string WorkOrderCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, DateTime HandedAt, string Status, string? ConfirmedByName, DateTime? ConfirmedAt,
    int TotalHandedQty);

public sealed record CutHandoverLineDto(
    int CutHandoverLineId, int OrderLineId, string ProductCode, string Color, string SizeCode, int HandedQty,
    int? ConfirmedQty, string? DiscrepancyReason);

public sealed record CutHandoverDto(
    int CutHandoverId, string HandoverCode, int WorkOrderId, string WorkOrderCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, DateTime HandedAt, string Status, string? ConfirmedByName, DateTime? ConfirmedAt,
    string? RejectReason, IReadOnlyList<CutHandoverLineDto> Lines);

/// <summary>Số còn được bàn giao của từng dòng đơn = đã cắt − đã bàn giao (chờ nhận + đã nhận).</summary>
public sealed record HandoverableLineDto(int OrderLineId, string Color, string SizeCode, int CutQty, int HandedQty,
    int RemainingQty);

public sealed record CreateHandoverLineDto(int OrderLineId, int HandedQty);

public sealed record CreateCutHandoverDto(int WorkOrderId, IReadOnlyList<CreateHandoverLineDto> Lines);

public sealed record ConfirmHandoverLineDto(int CutHandoverLineId, int ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmCutHandoverDto(IReadOnlyList<ConfirmHandoverLineDto> Lines);

public sealed record CutHandoverQuery : PageQuery
{
    public string? Status { get; init; }
    public int? WorkOrderId { get; init; }
}
