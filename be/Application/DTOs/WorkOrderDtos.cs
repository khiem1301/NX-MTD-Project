using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record FabricNeed(int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal RequiredQty);

/// <summary>Vải của lệnh cắt: cần (định mức), đã cấp (chờ nhận + đã nhận), còn phải cấp, khả dụng trong kho.</summary>
public sealed record FabricRequirementDto(
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal RequiredQty, decimal IssuedQty, decimal RemainingQty, decimal AvailableQty);

/// <summary>
/// Một dòng xem trước vải khi lập lệnh cắt: cần theo định mức, khả dụng trong kho, phần kho đã giữ cho các lệnh cắt khác,
/// vải tổ cắt đang giữ còn trống, và thiếu bao nhiêu.
/// </summary>
public sealed record FabricPreviewRowDto(
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal RequiredQty, decimal WarehouseAvailable, decimal ReservedByOtherWorkOrders, decimal TeamHolding,
    decimal AvailableQty, decimal ShortQty);

/// <param name="MissingNormLines">Dòng đơn (màu - size) chưa có định mức vải thân (PANEL) nên không tính được vải cần.</param>
public sealed record FabricPreviewDto(IReadOnlyList<FabricPreviewRowDto> Rows, IReadOnlyList<string> MissingNormLines, bool Enough);

/// <summary>Vải theo từng đơn: cần theo các lệnh cắt đang chạy, đã cấp cho tổ cắt, còn phải cấp (xem "đơn nào dùng vải nào").</summary>
public sealed record FabricByOrderRowDto(
    int OrderId, string OrderCode, int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal RequiredQty, decimal IssuedQty, decimal RemainingQty, IReadOnlyList<string> WorkOrderCodes);

public sealed record WorkOrderListItemDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    int CuttingTeamId, string CuttingTeamName, DateOnly DueDate, string Status, int TotalPlannedQty, DateTime CreatedAt);

public sealed record WorkOrderLineDto(
    int WorkOrderLineId, int OrderLineId, string Color, string SizeCode, int? ProductComponentMaterialId, string ComponentName,
    int PlannedQty, int CutQty, int HandedQty);

public sealed record WorkOrderDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    string ProductName, int CuttingTeamId, string CuttingTeamName, int? SourceRequestId, DateOnly DueDate,
    string Status, string? Note, DateTime CreatedAt, IReadOnlyList<WorkOrderLineDto> Lines,
    IReadOnlyList<FabricRequirementDto> Fabrics);

public sealed record SaveWorkOrderLineDto(int OrderLineId, int PlannedQty);

/// <summary>Đơn cắt bù (REPLACEMENT_CUTTING) đã được Quản đốc duyệt và chưa có lệnh cắt bù.</summary>
public sealed record ReplacementRequestDto(
    int RequestId, string RequestCode, int OrderId, string OrderCode, string ProductCode, string? Content,
    IReadOnlyList<ReplacementRequestLineDto> Lines);

public sealed record ReplacementRequestLineDto(int OrderLineId, string Color, string SizeCode, int ProductComponentMaterialId,
    string ComponentName, int Quantity);

/// <summary>Lập lệnh cắt bù từ một đơn cắt bù đã duyệt; dòng lệnh lấy từ dòng đơn (chi tiết + số lượng).</summary>
public sealed record CreateReplacementWorkOrderDto(int RequestId, int CuttingTeamId, DateOnly DueDate, string? Note);

public sealed record SaveWorkOrderDto(int OrderId, int ProductId, int CuttingTeamId, DateOnly DueDate, string? Note,
    IReadOnlyList<SaveWorkOrderLineDto> Lines);

public sealed record WorkOrderQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
