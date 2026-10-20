using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record FabricNeed(int OrderMaterialId, int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal RequiredQty);

/// <summary>Vải của lệnh cắt: cần (định mức), đã cấp (chờ nhận + đã nhận), còn phải cấp, khả dụng trong kho.</summary>
public sealed record FabricRequirementDto(
    int OrderMaterialId, int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal RequiredQty, decimal IssuedQty, decimal RemainingQty, decimal AvailableQty);

public sealed record WorkOrderListItemDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    int CuttingTeamId, string CuttingTeamName, DateOnly DueDate, string Status, int TotalPlannedQty, DateTime CreatedAt);

public sealed record WorkOrderLineDto(
    int WorkOrderLineId, int OrderLineId, string Color, string SizeCode, string PanelName, int PlannedQty,
    int CutQty, int HandedQty);

public sealed record WorkOrderDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    string ProductName, int CuttingTeamId, string CuttingTeamName, int? SourceRequestId, DateOnly DueDate,
    string Status, string? Note, DateTime CreatedAt, IReadOnlyList<WorkOrderLineDto> Lines,
    IReadOnlyList<FabricRequirementDto> Fabrics);

public sealed record SaveWorkOrderLineDto(int OrderLineId, int PlannedQty);

public sealed record SaveWorkOrderDto(int OrderId, int ProductId, int CuttingTeamId, DateOnly DueDate, string? Note,
    IReadOnlyList<SaveWorkOrderLineDto> Lines);

public sealed record WorkOrderQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
