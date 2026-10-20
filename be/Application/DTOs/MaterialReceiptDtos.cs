using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialReceiptListItemDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    DateOnly ReceiptDate, string Status, DateTime SubmittedAt, DateTime? ConfirmedAt, string? ConfirmedByName,
    int LineCount);

public sealed record MaterialReceiptLineDto(
    int MaterialReceiptLineId, int? OrderMaterialId, int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal AcceptedQty, decimal ShortQty, decimal DefectiveQty);

public sealed record MaterialReceiptDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    DateOnly ReceiptDate, string Status, string? ReturnReason, string? Note, DateTime SubmittedAt,
    DateTime? ConfirmedAt, string? ConfirmedByName, IReadOnlyList<MaterialReceiptLineDto> Lines);

public sealed record SaveReceiptLineDto(int OrderMaterialId, decimal AcceptedQty, decimal ShortQty, decimal DefectiveQty);

/// <summary>Phase 1 chỉ nhận Source = CUSTOMER (NVL khách cấp); nhập từ NCC làm ở TH-12.</summary>
public sealed record SaveMaterialReceiptDto(int OrderId, string Source, DateOnly ReceiptDate, string? Note,
    IReadOnlyList<SaveReceiptLineDto> Lines);

public sealed record ReasonDto(string Reason);

public sealed record MaterialReceiptQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
}
