using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>
/// Vải của lệnh cắt (29/10: không còn lượng cần theo định mức): đã cấp (chờ nhận + đã nhận), khả dụng trong kho, tổ đã nhận,
/// đã trả lại kho (phiếu trả đã xác nhận) và thực dùng = đã nhận − đã trả.
/// </summary>
public sealed record FabricRequirementDto(
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal IssuedQty, decimal AvailableQty, decimal ReceivedQty = 0, decimal ReturnedQty = 0, decimal UsedQty = 0);

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

/// <param name="Reason">Bắt buộc khi BTP vào kho chưa đủ kế hoạch (cắt thiếu).</param>
public sealed record FinishCuttingDto(string? Reason);

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
