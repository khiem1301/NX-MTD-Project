using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialReceiptListItemDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    int? PurchaseOrderId, string? PoCode, DateOnly ReceiptDate, string Status, DateTime SubmittedAt,
    DateTime? ConfirmedAt, string? ConfirmedByName, int LineCount);

public sealed record MaterialReceiptLineDto(
    int MaterialReceiptLineId, int? OrderMaterialId, int? PurchaseOrderLineId, int? OriginalReceiptLineId,
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal? UnitPrice, decimal? ToReplenishQty, decimal AcceptedQty, decimal ShortQty, decimal DefectiveQty);

/// <param name="ShortageRequestId">Phiếu bổ sung: yêu cầu thiếu NVL mà phiếu này trả lời.</param>
/// <param name="ShortDeliveryRequestId">Phiếu gốc có thiếu / lỗi: yêu cầu xử lý thiếu NVL đã sinh ra từ phiếu này.</param>
public sealed record MaterialReceiptDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    int? PurchaseOrderId, string? PoCode, int? OriginalReceiptId, string? OriginalReceiptCode, int? ShortageRequestId,
    int? ShortDeliveryRequestId, DateOnly ReceiptDate, string Status, string? ReturnReason, string? Note,
    DateTime SubmittedAt, DateTime? ConfirmedAt, string? ConfirmedByName,
    IReadOnlyList<MaterialReceiptLineDto> Lines);

/// <summary>Khách cấp: dòng chọn NVL của đơn (OrderMaterialId). Nhà cung cấp: dòng chọn dòng đơn mua (PurchaseOrderLineId).</summary>
public sealed record SaveReceiptLineDto(int? OrderMaterialId, int? PurchaseOrderLineId, decimal AcceptedQty,
    decimal ShortQty, decimal DefectiveQty);

/// <summary>Source = CUSTOMER cần OrderId; Source = SUPPLIER cần PurchaseOrderId (đơn hàng lấy theo đơn mua).</summary>
public sealed record SaveMaterialReceiptDto(int? OrderId, string Source, int? PurchaseOrderId, DateOnly ReceiptDate,
    string? Note, IReadOnlyList<SaveReceiptLineDto> Lines);

public sealed record SupplementaryLineDto(int OriginalReceiptLineId, decimal AcceptedQty, decimal ShortQty,
    decimal DefectiveQty);

/// <summary>Phiếu bổ sung: bên giao bù theo yêu cầu thiếu NVL đã chọn "giao bù".</summary>
public sealed record SaveSupplementaryReceiptDto(int ShortageRequestId, DateOnly ReceiptDate, string? Note,
    IReadOnlyList<SupplementaryLineDto> Lines);

public sealed record ReasonDto(string Reason);

public sealed record MaterialReceiptQuery : PageQuery
{
    public string? Status { get; init; }
    public int? OrderId { get; init; }
    public int? PurchaseOrderId { get; init; }
    public string? Source { get; init; }
}

// ---------- Xử lý thiếu NVL khi nhập (UC08, SHORT_DELIVERY) ----------

/// <param name="ShortfallQty">Thiếu + lỗi của dòng phiếu nhập gốc (số cần bù).</param>
/// <param name="ReplenishedQty">Đã nhận bù (phiếu bổ sung đã xác nhận).</param>
public sealed record ShortDeliveryLineDto(
    int RequestLineId, int MaterialReceiptLineId, int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal ShortQty, decimal DefectiveQty, decimal ShortfallQty,
    decimal? ReplenishQty, decimal ReplenishedQty);

public sealed record ShortDeliveryListItemDto(
    int RequestId, string RequestCode, string Status, string? Resolution, int ReceiptId, string ReceiptCode,
    string? PoCode, string? OrderCode, DateTime CreatedAt);

public sealed record ShortDeliveryDto(
    int RequestId, string RequestCode, string Status, string? Resolution, string? ResultNote, int ReceiptId,
    string ReceiptCode, string Source, string? PoCode, string? OrderCode, DateTime CreatedAt,
    IReadOnlyList<ShortDeliveryLineDto> Lines);

public sealed record ReplenishInputDto(int RequestLineId, decimal ReplenishQty);

/// <param name="Resolution">REPLENISH (giao bù) hoặc NO_REPLENISH (chốt không bù).</param>
public sealed record ResolveShortDeliveryDto(string Resolution, IReadOnlyList<ReplenishInputDto>? Lines, string? Note);

public sealed record ShortDeliveryQuery : PageQuery
{
    /// <summary>true = chỉ yêu cầu đã chọn giao bù và còn đang chờ nhận bù (Thủ kho lập phiếu bổ sung).</summary>
    public bool? AwaitingReplenish { get; init; }
}
