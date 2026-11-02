using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>
/// Vải của lệnh cắt (29/10: không còn lượng cần theo định mức): đã cấp (chờ nhận + đã nhận), khả dụng trong kho, tổ đã nhận,
/// đã trả lại kho (phiếu trả đã xác nhận) và thực dùng = đã nhận − đã trả.
/// </summary>
/// <param name="HeldQty">Vải tổ cắt đang giữ của đơn = đã nhận − đã trả về kho.</param>
/// <param name="CutQty">Vải đã cắt của tổ trên đơn = Σ số lớp × chiều dài bàn cắt.</param>
/// <param name="RemainingQty">Vải còn lại = HeldQty − CutQty; âm nghĩa là đã cắt nhiều hơn số đang giữ.</param>
/// <param name="UnmeasuredLayCount">Số bàn dùng vải này chưa có chiều dài bàn cắt nên chưa cộng vào CutQty.</param>
public sealed record FabricRequirementDto(
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal IssuedQty, decimal AvailableQty, decimal ReceivedQty = 0, decimal ReturnedQty = 0, decimal UsedQty = 0,
    decimal HeldQty = 0, decimal CutQty = 0, decimal RemainingQty = 0, int UnmeasuredLayCount = 0);

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
    IReadOnlyList<FabricRequirementDto> Fabrics, IReadOnlyList<WorkOrderFabricCheckDto> FabricChecks);

/// <summary>Vải theo kích thước chi tiết của một NVL. Status là một giá trị của FabricCheckStatuses; Message = null khi đủ vải. MaterialId = 0: dòng báo màu – size chưa có định mức vải.</summary>
public sealed record WorkOrderFabricCheckDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor,
    string Status, decimal? NeedMeters, decimal? AvailableQty, string? Message);

/// <summary>Kiểm vải khi đang lập lệnh, chưa lưu: chỉ cảnh báo, không chặn lưu.</summary>
public sealed record CheckWorkOrderFabricDto(int OrderId, int CuttingTeamId, IReadOnlyList<SaveWorkOrderLineDto> Lines);

/// <param name="Reason">Lý do cắt thiếu kế hoạch, bắt buộc.</param>
public sealed record CloseShortWorkOrderDto(string Reason);

/// <param name="ProductComponentMaterialId">null = cả bộ; có giá trị = cắt lẻ thêm chi tiết này, PlannedQty là số chi tiết (không tính vào trần số đặt).</param>
public sealed record SaveWorkOrderLineDto(int OrderLineId, int PlannedQty, int? ProductComponentMaterialId = null);

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
