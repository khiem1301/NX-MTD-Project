using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialReceiptListItemDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    int? PurchaseOrderId, string? PoCode, DateOnly ReceiptDate, string Status, DateTime SubmittedAt,
    DateTime? ConfirmedAt, string? ConfirmedByName, int LineCount);

/// <param name="ToReplenishQty">Phiếu thường: số còn thiếu sau phiếu này (đặt hoặc khách cấp − đã nhận ở các phiếu xác nhận trước
/// − số phiếu này); phiếu bổ sung: số Quản đốc chọn giao bù.</param>
/// <param name="DeliveryNo">Phiếu này là lần nhận thứ mấy của NVL (dòng đơn mua hoặc NVL khách cấp).</param>
/// <param name="ExpectedDeliveryCount">Số lần giao dự kiến của nguồn (đơn mua hoặc NVL khách cấp), mặc định 1.</param>
public sealed record MaterialReceiptLineDto(
    int MaterialReceiptLineId, int? OrderMaterialId, int? PurchaseOrderLineId, int? OriginalReceiptLineId,
    int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor,
    decimal? UnitPrice, decimal? ToReplenishQty, decimal AcceptedQty, int? DeliveryNo = null, int? ExpectedDeliveryCount = null);

/// <param name="ReceiptType">Mới (New) hoặc Bổ sung (Supplementary): phiếu bổ sung là phiếu có ShortageRequestId.</param>
/// <param name="OriginalReceiptId">Phiếu bổ sung: phiếu gốc = phiếu nhập đã sinh ra yêu cầu thiếu NVL.</param>
/// <param name="ShortageRequestId">Phiếu bổ sung: yêu cầu thiếu NVL mà phiếu này trả lời.</param>
/// <param name="ShortDeliveryRequestId">Phiếu gốc có hàng thiếu: yêu cầu xử lý thiếu NVL đã sinh ra từ phiếu này.</param>
public sealed record MaterialReceiptDto(
    int MaterialReceiptId, string ReceiptCode, string ReceiptType, int? OrderId, string? OrderCode, string Source,
    int? PurchaseOrderId, string? PoCode, int? OriginalReceiptId, string? OriginalReceiptCode, int? ShortageRequestId,
    int? ShortDeliveryRequestId, DateOnly ReceiptDate, string Status, string? ReturnReason, string? Note,
    DateTime SubmittedAt, DateTime? ConfirmedAt, string? ConfirmedByName,
    IReadOnlyList<MaterialReceiptLineDto> Lines);

/// <summary>Khách cấp: dòng chọn NVL của đơn (OrderMaterialId). Nhà cung cấp: dòng chọn dòng đơn mua (PurchaseOrderLineId).</summary>
public sealed record SaveReceiptLineDto(int? OrderMaterialId, int? PurchaseOrderLineId, decimal AcceptedQty);

/// <summary>Source = CUSTOMER cần OrderId; Source = SUPPLIER cần PurchaseOrderId (đơn hàng lấy theo đơn mua).</summary>
public sealed record SaveMaterialReceiptDto(int? OrderId, string Source, int? PurchaseOrderId, DateOnly ReceiptDate,
    string? Note, IReadOnlyList<SaveReceiptLineDto> Lines);

/// <summary>Một dòng phiếu bổ sung: nhận bù cho dòng đơn thiếu NVL (DEV-ALL-95: theo dòng đơn thiếu, kể cả NVL chưa về lần nào).</summary>
public sealed record SupplementaryLineDto(int RequestLineId, decimal AcceptedQty);

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

/// <param name="MaterialReceiptLineId">Dòng phiếu nhập gốc; null = NVL chưa về lần nào khi đơn đủ số lần giao (DEV-ALL-95).</param>
/// <param name="ShortfallQty">Số còn thiếu của dòng phiếu nhập gốc khi hệ thống tạo yêu cầu (số cần bù).</param>
/// <param name="ReplenishedQty">Đã nhận bù (phiếu bổ sung đã xác nhận).</param>
public sealed record ShortDeliveryLineDto(
    int RequestLineId, int? MaterialReceiptLineId, int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal ShortfallQty,
    decimal? ReplenishQty, decimal ReplenishedQty);

public sealed record ShortDeliveryListItemDto(
    int RequestId, string RequestCode, string Status, string? Resolution, int ReceiptId, string ReceiptCode,
    string Source, string? PoCode, string? OrderCode, DateTime CreatedAt);

/// <param name="ExpectedDeliveryCount">Tổng số lần giao bù dự kiến (cộng dồn qua các lần Quản đốc chọn giao bù).</param>
/// <param name="ReplenishDeliveryCount">Số lần đã giao bù (phiếu bổ sung đã xác nhận).</param>
/// <param name="ExpectedDeliveryDate">Ngày dự kiến giao bù (giao từ 2 lần: ngày từ).</param>
/// <param name="ExpectedDeliveryDateTo">Giao bù từ 2 lần: ngày đến.</param>
public sealed record ShortDeliveryDto(
    int RequestId, string RequestCode, string Status, string? Resolution, string? ResultNote, int ReceiptId,
    string ReceiptCode, string Source, string? PoCode, string? OrderCode, DateTime CreatedAt,
    IReadOnlyList<ShortDeliveryLineDto> Lines, short? ExpectedDeliveryCount = null, int ReplenishDeliveryCount = 0,
    DateOnly? ExpectedDeliveryDate = null, DateOnly? ExpectedDeliveryDateTo = null,
    IReadOnlyList<DateOnly>? ExpectedDeliveryDates = null);

public sealed record ReplenishInputDto(int RequestLineId, decimal ReplenishQty);

/// <param name="Resolution">REPLENISH (giao bù) hoặc NO_REPLENISH (chốt không bù).</param>
/// <param name="ExpectedDeliveryCount">Giao bù: số lần dự kiến giao bù đợt này (bắt buộc, 1–99).</param>
/// <param name="ExpectedDeliveryDates">Giao bù: ngày dự kiến giao bù, đúng ExpectedDeliveryCount ngày (DEV-ALL-113).</param>
public sealed record ResolveShortDeliveryDto(string Resolution, IReadOnlyList<ReplenishInputDto>? Lines, string? Note,
    short? ExpectedDeliveryCount = null, IReadOnlyList<DateOnly>? ExpectedDeliveryDates = null);

public sealed record ShortDeliveryQuery : PageQuery
{
    /// <summary>true = chỉ yêu cầu đã chọn giao bù và còn đang chờ nhận bù (Thủ kho lập phiếu bổ sung).</summary>
    public bool? AwaitingReplenish { get; init; }

    /// <summary>true = chỉ yêu cầu Quản đốc đã xử lý (giao bù hoặc chốt không bù): phạm vi xem của Thủ kho.</summary>
    public bool? ResolvedOnly { get; init; }
}
