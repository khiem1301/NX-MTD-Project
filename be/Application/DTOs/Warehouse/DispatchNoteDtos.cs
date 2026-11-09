using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record DispatchNoteListItemDto(
    int DispatchNoteId, string DispatchCode, int OrderId, string OrderCode, string CustomerName, DateOnly DeliveryDate,
    string Status, DateTime CreatedAt, int TotalIssueQty);

public sealed record DispatchNoteLineDto(
    int DispatchNoteLineId, int OrderLineId, string ProductCode, string Color, string SizeCode, int IssueQty);

public sealed record DispatchNoteDto(
    int DispatchNoteId, string DispatchCode, int OrderId, string OrderCode, string CustomerName, DateOnly DeliveryDate,
    string DeliveryAddress, string? Note, string Status, DateTime CreatedAt, string? RejectReason,
    string? DispatchedByName, DateTime? DispatchedAt, DateOnly? CustomerReceivedDate,
    IReadOnlyList<DispatchNoteLineDto> Lines, byte[] RowVer);

/// <summary>Số còn xuất được của từng dòng đơn: đặt − đã xuất (phiếu chờ + đang giao + đã giao); kèm tồn thành phẩm khả dụng.</summary>
public sealed record DispatchableLineDto(int OrderLineId, string ProductCode, string Color, string SizeCode,
    int OrderedQty, int IssuedQty, int RemainingQty, decimal AvailableQty);

public sealed record DispatchLineInputDto(int OrderLineId, int IssueQty);

public sealed record CreateDispatchNoteDto(int OrderId, DateOnly DeliveryDate, string DeliveryAddress, string? Note,
    IReadOnlyList<DispatchLineInputDto> Lines);

/// <summary>
/// Kế toán xác nhận khách nhận (khách nhận đúng số đã xuất); RowVer là phiên bản phiếu đang xem để phát hiện người khác
/// đã đổi phiếu.
/// </summary>
public sealed record ConfirmDeliveryDto(DateOnly CustomerReceivedDate, byte[] RowVer);

/// <summary>Hủy phiếu hoặc xuất hàng: chỉ cần phiên bản phiếu đang xem.</summary>
public sealed record DispatchActionDto(byte[] RowVer);

/// <summary>Thủ kho từ chối phiếu: lý do (bắt buộc) và phiên bản phiếu đang xem.</summary>
public sealed record RejectDispatchDto(string Reason, byte[] RowVer);

public sealed record DispatchNoteQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
