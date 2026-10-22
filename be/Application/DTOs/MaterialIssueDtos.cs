using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialIssueListItemDto(
    int MaterialIssueId, string IssueCode, string WarehouseType, int OrderId, string OrderCode, int? WorkOrderId,
    string? WorkOrderCode, int? RequestId, int ReceivingTeamId, string ReceivingTeamName, DateOnly IssueDate,
    string Status, string IssuedByName, DateTime? ReceivedAt, int LineCount);

/// <summary>Dòng NVL có MaterialId + MaterialColor; dòng BTP có OrderLineId.</summary>
public sealed record MaterialIssueLineDto(
    int MaterialIssueLineId, int? MaterialId, string? MaterialCode, string? MaterialName, string? Unit,
    string? MaterialColor, int? OrderLineId, string? ProductCode, string? Color, string? SizeCode, decimal IssuedQty,
    decimal? ConfirmedQty, string? DiscrepancyReason);

public sealed record MaterialIssueDto(
    int MaterialIssueId, string IssueCode, string WarehouseType, int OrderId, string OrderCode, int? WorkOrderId,
    string? WorkOrderCode, int? RequestId, int ReceivingTeamId, string ReceivingTeamName, DateOnly IssueDate,
    string Status, string IssuedByName, string? ReceivedByName, DateTime? ReceivedAt, string? RejectReason,
    DateTime CreatedAt, IReadOnlyList<MaterialIssueLineDto> Lines);

public sealed record CreateIssueLineDto(int? MaterialId, string? MaterialColor, int? OrderLineId, decimal IssuedQty);

/// <summary>
/// MATERIAL: cấp vải cho tổ cắt theo lệnh cắt (WorkOrderId bắt buộc; đơn và tổ lấy từ lệnh).
/// SEMI_FINISHED: cấp BTP cho tổ may (OrderId + ReceivingTeamId bắt buộc, dòng theo OrderLineId).
/// RequestId: đơn Xin NVL / Xin BTP đang chờ xử lý của đúng tổ nhận; bắt buộc khi cấp BTP cho tổ may, tùy chọn khi cấp vải.
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
