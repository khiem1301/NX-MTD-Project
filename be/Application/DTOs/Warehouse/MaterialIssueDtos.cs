using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialIssueListItemDto(
    int MaterialIssueId, string IssueCode, string WarehouseType, int OrderId, string OrderCode, int? WorkOrderId,
    string? WorkOrderCode, int? RequestId, int ReceivingTeamId, string ReceivingTeamName, DateOnly IssueDate,
    string Status, string IssuedByName, DateTime? ReceivedAt, int LineCount);

/// <summary>Dòng NVL có MaterialId (MaterialColor = Material.Color); dòng BTP có OrderLineId.</summary>
public sealed record MaterialIssueLineDto(
    int MaterialIssueLineId, int? MaterialId, string? MaterialCode, string? MaterialName, string? Unit,
    string? MaterialColor, int? OrderLineId, int? ProductComponentMaterialId, string? ComponentName, string? ProductCode,
    string? Color, string? SizeCode, decimal IssuedQty, decimal? ConfirmedQty, string? DiscrepancyReason);

public sealed record MaterialIssueDto(
    int MaterialIssueId, string IssueCode, string WarehouseType, int OrderId, string OrderCode, int? WorkOrderId,
    string? WorkOrderCode, int? RequestId, int ReceivingTeamId, string ReceivingTeamName, DateOnly IssueDate,
    string Status, string IssuedByName, string? ReceivedByName, DateTime? ReceivedAt, string? RejectReason,
    DateTime CreatedAt, IReadOnlyList<MaterialIssueLineDto> Lines);

/// <param name="MaterialId">Chỉ cấp NVL: NVL đã gồm màu (26/10).</param>
/// <param name="ProductComponentMaterialId">Chỉ cấp BTP: null = bộ đủ; có giá trị = chi tiết lẻ (cắt bù).</param>
public sealed record CreateIssueLineDto(int? MaterialId, int? OrderLineId, int? ProductComponentMaterialId,
    decimal IssuedQty);

/// <summary>
/// MATERIAL: cấp NVL (vải, chỉ, cúc…) cho tổ cắt hoặc tổ may. WorkOrderId tùy chọn (có thì đơn và tổ lấy từ lệnh cắt, chỉ cấp vải
/// trong định mức của lệnh); không có thì cần OrderId + ReceivingTeamId.
/// SEMI_FINISHED: cấp BTP cho tổ may (OrderId + ReceivingTeamId, dòng theo OrderLineId).
/// RequestId (tùy chọn, cả hai loại): đơn Xin NVL / Xin BTP đã duyệt của đúng tổ nhận; thiếu lệnh cắt / đơn / tổ thì lấy từ đơn xin.
/// Lập phiếu xong thì đơn xin tự chuyển sang Hoàn thành.
/// </summary>
public sealed record CreateMaterialIssueDto(
    string WarehouseType, int? WorkOrderId, int? OrderId, int? ReceivingTeamId, int? RequestId, DateOnly IssueDate,
    IReadOnlyList<CreateIssueLineDto> Lines);

public sealed record ConfirmIssueLineDto(int MaterialIssueLineId, decimal ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmIssueDto(IReadOnlyList<ConfirmIssueLineDto> Lines);

public sealed record MaterialIssueQuery : PageQuery
{
    public string? Status { get; init; }
    public string? WarehouseType { get; init; }
    public int? OrderId { get; init; }
    public int? WorkOrderId { get; init; }
}
