using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>ProductComponentMaterialId null = cả bộ; có giá trị = chi tiết lẻ của lệnh cắt bù. ActualQty = số lượng sau khi cắt.</summary>
public sealed record CuttingLayLineDto(
    int CuttingLayLineId, int OrderLineId, int? ProductComponentMaterialId, string? ComponentName, string Color, string SizeCode,
    int ActualQty);

/// <param name="UsedM">null = bàn cũ chưa có chiều dài bàn cắt.</param>
public sealed record CuttingLayFabricDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor, decimal? UsedM);

/// <param name="Fabrics">Số mét từng vải của bàn (DEV-ALL-93).</param>
/// <param name="SizeSummary">Số lượng sau cắt theo size, VD "M:90, L:60".</param>
public sealed record CuttingLayDto(
    int CuttingLayId, int WorkOrderId, string WorkOrderCode, DateOnly WorkDate, string ShiftCode, int LaySequence,
    IReadOnlyList<CuttingLayFabricDto> Fabrics, string SizeSummary, string? Note,
    string Status, DateTime CreatedAt, IReadOnlyList<CuttingLayLineDto> Lines);

/// <summary>ActualQty = số lượng sản phẩm (cắt bù: chi tiết) sau khi cắt của dòng.</summary>
public sealed record SaveLayLineDto(int OrderLineId, int? ProductComponentMaterialId, int ActualQty);

public sealed record SaveLayFabricDto(int MaterialId, decimal? UsedM);

/// <param name="Fabrics">Đủ mọi vải các dòng cần (cả bộ: mọi vải chi tiết của màu áo; cắt bù / cắt lẻ: vải của chi tiết), mỗi vải
/// không vượt vải còn lại của tổ trên đơn (DEV-ALL-93).</param>
public sealed record SaveCuttingLayDto(
    int WorkOrderId, DateOnly WorkDate, string ShiftCode, IReadOnlyList<SaveLayFabricDto> Fabrics,
    string? Note, IReadOnlyList<SaveLayLineDto> Lines);

public sealed record CuttingLayQuery : PageQuery
{
    public int? WorkOrderId { get; init; }
    public DateOnly? WorkDate { get; init; }
    public string? Status { get; init; }
}

public sealed record CutHandoverListItemDto(
    int CutHandoverId, string HandoverCode, int WorkOrderId, string WorkOrderCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, DateTime HandedAt, string Status, string? ConfirmedByName, DateTime? ConfirmedAt,
    int TotalHandedQty);

public sealed record CutHandoverLineDto(
    int CutHandoverLineId, int OrderLineId, int? ProductComponentMaterialId, string? ComponentName, string ProductCode, string Color,
    string SizeCode, int HandedQty, int? ConfirmedQty, string? DiscrepancyReason);

public sealed record CutHandoverDto(
    int CutHandoverId, string HandoverCode, int WorkOrderId, string WorkOrderCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, DateTime HandedAt, string Status, string? ConfirmedByName, DateTime? ConfirmedAt,
    string? RejectReason, IReadOnlyList<CutHandoverLineDto> Lines);

/// <summary>Số còn được bàn giao của từng dòng đơn = đã cắt − đã bàn giao (chờ nhận + đã nhận).</summary>
public sealed record HandoverableLineDto(int OrderLineId, int? ProductComponentMaterialId, string? ComponentName, string Color,
    string SizeCode, int CutQty, int HandedQty, int RemainingQty);

public sealed record CreateHandoverLineDto(int OrderLineId, int HandedQty);

/// <param name="ProductComponentMaterialId">null = bộ đủ; có giá trị = chi tiết lẻ của lệnh cắt bù.</param>
public sealed record CreateCutHandoverLineDto(int OrderLineId, int? ProductComponentMaterialId, int HandedQty);

public sealed record CreateCutHandoverDto(int WorkOrderId, IReadOnlyList<CreateCutHandoverLineDto> Lines);

public sealed record ConfirmHandoverLineDto(int CutHandoverLineId, int ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmCutHandoverDto(IReadOnlyList<ConfirmHandoverLineDto> Lines);

public sealed record CutHandoverQuery : PageQuery
{
    public string? Status { get; init; }
    public int? WorkOrderId { get; init; }
}
