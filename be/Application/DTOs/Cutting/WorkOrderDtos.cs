using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>
/// Vải của lệnh cắt (29/10: không còn lượng cần theo định mức): đã cấp (chờ nhận + đã nhận), khả dụng trong kho, tổ đã nhận,
/// đã trả lại kho (phiếu trả đã xác nhận) và thực dùng = đã nhận − đã trả.
/// </summary>
/// <param name="HeldQty">Vải tổ cắt đang giữ của đơn = đã nhận − đã trả về kho.</param>
/// <param name="CutQty">Vải đã cắt của tổ trên đơn = Σ số mét vải đã dùng của các bàn.</param>
/// <param name="RemainingQty">Vải còn lại = HeldQty − CutQty; âm nghĩa là đã cắt nhiều hơn số đang giữ.</param>
/// <param name="UnmeasuredLayCount">Số bàn dùng vải này chưa có số mét vải đã dùng (bàn cũ) nên chưa cộng vào CutQty.</param>
public sealed record FabricRequirementDto(
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal IssuedQty, decimal AvailableQty, decimal ReceivedQty = 0, decimal ReturnedQty = 0, decimal UsedQty = 0,
    decimal HeldQty = 0, decimal CutQty = 0, decimal RemainingQty = 0, int UnmeasuredLayCount = 0);

/// <summary>Số mét vải một NVL cần cho các dòng (màu, size) đã chọn của lệnh cắt: Thủ kho dùng để đề xuất số cấp.</summary>
public sealed record FabricNeedDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor, decimal NeedMeters);

public sealed record WorkOrderListItemDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    int CuttingTeamId, string CuttingTeamName, DateOnly DueDate, string Status, int TotalPlannedQty, DateTime CreatedAt);

/// <param name="FabricMaterialIds">Vải dòng này cần (DEV-ALL-93): cả bộ = mọi vải chi tiết của màu áo; cắt bù / cắt lẻ = vải của chi tiết.</param>
public sealed record WorkOrderLineDto(
    int WorkOrderLineId, int OrderLineId, string Color, string SizeCode, int? ProductComponentMaterialId, string ComponentName,
    int PlannedQty, int CutQty, int HandedQty, IReadOnlyList<int>? FabricMaterialIds = null);

public sealed record WorkOrderDto(
    int WorkOrderId, string WorkOrderCode, int OrderId, string OrderCode, int ProductId, string ProductCode,
    string ProductName, int CuttingTeamId, string CuttingTeamName, int? SourceRequestId, DateOnly DueDate,
    string Status, string? Note, DateTime CreatedAt, IReadOnlyList<WorkOrderLineDto> Lines,
    IReadOnlyList<FabricRequirementDto> Fabrics, IReadOnlyList<WorkOrderFabricCheckDto> FabricChecks,
    decimal FabricTolerancePercent = 0);

/// <summary>
/// Vải ước theo bìa của đơn (DEV-ALL-85, ghép bìa DEV-ALL-88), riêng từng NVL. Status là một giá trị của FabricCheckStatuses; Message = null khi đủ vải.
/// MaterialId = 0: dòng báo màu – size chưa có định mức vải. AvailableQty = WarehouseQty (kho khả dụng) + HoldingQty (tổ cắt đang giữ).
/// </summary>
/// <param name="MarkerPlans">Bìa nên dùng theo từng nhóm bìa, hệ thống ghép cho ít mét nhất (ước tính, có thể chênh lệch với thực tế); rỗng khi chưa tính được.</param>
/// <param name="WidthWarning">DEV-ALL-103: cảnh báo bìa nên dùng rộng hơn khổ vải (vải không đủ rộng để cắt); null = không có hoặc vải chưa có khổ.</param>
public sealed record WorkOrderFabricCheckDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor,
    string Status, decimal? NeedMeters, decimal? AvailableQty, string? Message,
    IReadOnlyList<FabricMarkerPlanDto>? MarkerPlans = null, decimal? WarehouseQty = null, decimal? HoldingQty = null,
    string? WidthWarning = null);

/// <summary>Phương án trải bìa của một nhóm bìa trên một vải. IsExtra = phần cắt lẻ / cắt bù; Surplus = sản phẩm (cắt lẻ: miếng) dư.</summary>
public sealed record FabricMarkerPlanDto(int MarkerNo, bool IsExtra, decimal Meters, int Surplus, bool IsProvenOptimal,
    IReadOnlyList<FabricMarkerUseDto> Markers);

/// <summary>
/// Một bìa trong phương án: trải Plies lớp × LengthM = Meters; Ratio = số sản phẩm mỗi size trên một bìa (theo thứ tự size);
/// Across = số bìa trải song song trên một lớp (DEV-ALL-105), một lớp cắt ra Across × Ratio.
/// </summary>
public sealed record FabricMarkerUseDto(string MarkerCode, decimal LengthM, int Plies, decimal Meters, IReadOnlyList<MarkerRatioDto> Ratio,
    int Across = 1);

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

/// <param name="FabricTolerancePercent">Tỉ lệ sai lệch (%) trên số mét vải hệ thống tính, làm trần đơn xin NVL của lệnh (DEV-ALL-92).</param>
public sealed record SaveWorkOrderDto(int OrderId, int ProductId, int CuttingTeamId, DateOnly DueDate, string? Note,
    IReadOnlyList<SaveWorkOrderLineDto> Lines, decimal FabricTolerancePercent = 0);

/// <summary>
/// Vải của lệnh cắt cho đơn xin NVL (DEV-ALL-92): số mét hệ thống tính, trần = cần × (1 + tỉ lệ sai lệch), đã xin ở các đơn xin NVL
/// khác của lệnh (trừ đơn bị từ chối / hủy), còn xin được. NeedQty null = chưa tính được (mã hàng chưa có bìa), không xin theo lệnh được.
/// </summary>
public sealed record WorkOrderMaterialAllowanceDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialColor,
    string Unit, decimal? NeedQty, decimal? CapQty, decimal RequestedQty, decimal? RemainingQty);

public sealed record WorkOrderQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
