using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record DispatchNoteListItemDto(
    int DispatchNoteId, string DispatchCode, int OrderId, string OrderCode, string CustomerName, DateOnly DeliveryDate,
    string Status, DateTime CreatedAt, int TotalIssueQty, int? TotalReceivedQty);

public sealed record DispatchNoteLineDto(
    int DispatchNoteLineId, int OrderLineId, string ProductCode, string Color, string SizeCode, int IssueQty,
    int? ReceivedQty);

public sealed record DispatchNoteDto(
    int DispatchNoteId, string DispatchCode, int OrderId, string OrderCode, string CustomerName, DateOnly DeliveryDate,
    string DeliveryAddress, string? Note, string Status, DateTime CreatedAt, string? RejectReason,
    string? DispatchedByName, DateTime? DispatchedAt, DateOnly? CustomerReceivedDate, DateTime? DeliveryConfirmedAt,
    IReadOnlyList<DispatchNoteLineDto> Lines);

/// <summary>Số còn xuất được của từng dòng đơn: đặt − đã xuất (phiếu chờ + đang giao + đã giao); kèm tồn thành phẩm khả dụng.</summary>
public sealed record DispatchableLineDto(int OrderLineId, string ProductCode, string Color, string SizeCode,
    int OrderedQty, int IssuedQty, int RemainingQty, decimal AvailableQty);

public sealed record DispatchLineInputDto(int OrderLineId, int IssueQty);

public sealed record CreateDispatchNoteDto(int OrderId, DateOnly DeliveryDate, string DeliveryAddress, string? Note,
    IReadOnlyList<DispatchLineInputDto> Lines);

public sealed record DeliveryLineInputDto(int DispatchNoteLineId, int ReceivedQty);

public sealed record ConfirmDeliveryDto(DateOnly CustomerReceivedDate, IReadOnlyList<DeliveryLineInputDto> Lines);

public sealed record DispatchNoteQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
