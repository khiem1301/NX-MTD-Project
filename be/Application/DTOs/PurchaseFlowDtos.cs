using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ---------- Nhà cung cấp (Admin quản lý, Quản đốc chọn khi đặt hàng) ----------

public sealed record SupplierDto(
    int SupplierId, string SupplierName, string? Phone, string? Email, string? Address, string? MaterialTypes,
    string? Note, string Status, DateTime CreatedAt, int PurchaseOrderCount);

public sealed record SupplierDetailDto(SupplierDto Supplier, IReadOnlyList<AuditLogDto> History);

public sealed record SaveSupplierDto(
    string SupplierName, string? Phone, string? Email, string? Address, string? MaterialTypes, string? Note);

public sealed record SupplierQuery : PageQuery
{
    /// <summary>"Active" | "Discontinued"; trống = tất cả.</summary>
    public string? Status { get; init; }
}

/// <summary>Ô chọn nhà cung cấp: chỉ nhà cung cấp đang hợp tác.</summary>
public sealed record SupplierOptionDto(int SupplierId, string SupplierName);

// ---------- Đơn mua: đặt hàng (Quản đốc) và thanh toán (Kế toán) ----------

public sealed record PurchaseOrderFlowQuery : PageQuery
{
    /// <summary>PascalCase, vd. "PendingFinalisation".</summary>
    public string? GoodsStatus { get; init; }

    /// <summary>"Unpaid" | "Paid" | "Returned".</summary>
    public string? PaymentStatus { get; init; }

    /// <summary>Server đặt true cho Kế toán: chỉ đơn đã đặt hàng với nhà cung cấp mới đến lượt thanh toán.</summary>
    public bool PayableOnly { get; init; }
}

public sealed record PurchaseOrderFlowListItemDto(
    int PurchaseOrderId, string PoCode, string? OrderCode, string? SupplierName, string GoodsStatus,
    string PaymentStatus, DateOnly? OrderDate, decimal TotalAmount, decimal? PaidAmount, DateTime CreatedAt);

/// <summary>Nút nào hiện được ở màn chi tiết: do server tính theo vai trò + trạng thái đơn (FE không tự suy).</summary>
public sealed record PurchaseOrderPermissionsDto(
    bool CanPlace, bool CanCancel, bool CanReprice, bool CanConfirmPayment, bool CanReturnPayment);

public sealed record PurchaseOrderFlowDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, int? SupplierId, string? SupplierName,
    string GoodsStatus, string PaymentStatus, DateOnly? OrderDate, DateOnly? ExpectedDeliveryDate, string? Note,
    string? RejectReason, string? PaymentReturnReason, DateOnly? PaymentDate, decimal? PaidAmount,
    string? PaymentMethod, string? PaymentNote, DateTime? PaidAt, decimal TotalAmount, DateTime CreatedAt,
    byte[] RowVer, IReadOnlyList<PurchaseOrderLineDto> Lines, PurchaseOrderPermissionsDto Permissions,
    IReadOnlyList<AuditLogDto> History);

/// <summary>Đặt hàng phải gửi đủ mọi dòng của đơn: số lượng và đơn giá chốt lần cuối (sau đó khóa — GBR-18).</summary>
public sealed record PlacePurchaseLineDto(int PurchaseOrderLineId, decimal OrderedQty, decimal UnitPrice);

public sealed record PlacePurchaseOrderDto(
    int SupplierId, DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, string? Note,
    IReadOnlyList<PlacePurchaseLineDto> Lines, byte[] RowVer);

public sealed record CancelPurchaseOrderDto(string Reason, byte[] RowVer);

/// <summary>Sau khi Kế toán trả lại: Quản đốc chỉ sửa đơn giá (không đổi số lượng, nhà cung cấp).</summary>
public sealed record RepricePurchaseLineDto(int PurchaseOrderLineId, decimal UnitPrice);

public sealed record RepricePurchaseOrderDto(string? Note, IReadOnlyList<RepricePurchaseLineDto> Lines, byte[] RowVer);

/// <summary>PaymentMethod nhận PascalCase ("BankTransfer" | "Cash"). Ảnh chứng từ tải lên riêng qua Attachment.</summary>
public sealed record ConfirmPaymentDto(
    DateOnly PaymentDate, decimal PaidAmount, string PaymentMethod, string? PaymentNote, byte[] RowVer);

public sealed record ReturnPaymentDto(string Reason, byte[] RowVer);
