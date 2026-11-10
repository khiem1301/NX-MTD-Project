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

/// <summary>Số mét vải một NVL cần cho các dòng (màu, size) đã chọn của lệnh cắt: Thủ kho dùng để đề xuất số cấp.</summary>
public sealed record FabricNeedDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor, decimal NeedMeters);

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

/// <summary>
/// Vải ước theo bìa của đơn (DEV-ALL-85, ghép bìa DEV-ALL-88), riêng từng NVL. Status là một giá trị của FabricCheckStatuses; Message = null khi đủ vải.
/// MaterialId = 0: dòng báo màu – size chưa có định mức vải. AvailableQty = WarehouseQty (kho khả dụng) + HoldingQty (tổ cắt đang giữ).
/// </summary>
/// <param name="MarkerPlans">Bìa nên dùng theo từng nhóm bìa, hệ thống ghép cho ít mét nhất (ước tính, có thể chênh lệch với thực tế); rỗng khi chưa tính được.</param>
public sealed record WorkOrderFabricCheckDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor,
    string Status, decimal? NeedMeters, decimal? AvailableQty, string? Message,
    IReadOnlyList<FabricMarkerPlanDto>? MarkerPlans = null, decimal? WarehouseQty = null, decimal? HoldingQty = null);

/// <summary>Phương án trải bìa của một nhóm bìa trên một vải. IsExtra = phần cắt lẻ / cắt bù; Surplus = sản phẩm (cắt lẻ: miếng) dư.</summary>
public sealed record FabricMarkerPlanDto(int MarkerNo, bool IsExtra, decimal Meters, int Surplus, bool IsProvenOptimal,
    IReadOnlyList<FabricMarkerUseDto> Markers);

/// <summary>Một bìa trong phương án: trải Plies lớp × LengthM = Meters; Ratio = số sản phẩm mỗi size trên một lớp (theo thứ tự size).</summary>
public sealed record FabricMarkerUseDto(string MarkerCode, decimal LengthM, int Plies, decimal Meters, IReadOnlyList<MarkerRatioDto> Ratio);

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
