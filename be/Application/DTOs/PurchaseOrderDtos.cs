using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record PurchaseOrderListItemDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, string? SupplierName, string GoodsStatus,
    string PaymentStatus, DateTime CreatedAt, int LineCount);

public sealed record PurchaseOrderLineDto(
    int PurchaseOrderLineId, int? OrderMaterialId, int MaterialId, string MaterialCode, string MaterialName,
    string Unit, string MaterialColor, decimal OrderedQty, decimal UnitPrice, decimal? Amount,
    decimal ReceivedQty, decimal NotReplenishedQty);

public sealed record PurchaseOrderDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, int? SupplierId, string? SupplierName,
    string GoodsStatus, string PaymentStatus, string? Note, DateTime CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

/// <summary>
/// Chọn NVL có sẵn (MaterialId) hoặc nhập NVL mới (NewMaterialName + NewMaterialUnit): NVL mới được thêm vào
/// danh mục (phụ liệu, nhóm OTHER), nếu đã có NVL phụ liệu trùng tên thì dùng lại.
/// </summary>
public sealed record SavePurchaseOrderLineDto(int? MaterialId, string? NewMaterialName, string? NewMaterialUnit,
    string? MaterialColor, decimal OrderedQty, decimal UnitPrice);

/// <summary>OrderId để trống = đơn mua dự trữ (kim, chỉ... dùng chung nhiều đơn), không cần Admin tạo mã đơn hàng.</summary>
public sealed record CreatePurchaseOrderDto(int? OrderId, string? Note, IReadOnlyList<SavePurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderQuery : PageQuery
{
    public string? GoodsStatus { get; init; }

    /// <summary>true = chỉ đơn mua dự trữ (không gắn đơn hàng).</summary>
    public bool? StockOnly { get; init; }

    /// <summary>true = chỉ đơn đã đặt hàng và còn phải nhận (ORDERED / PARTIALLY_RECEIVED): Thủ kho chọn khi lập phiếu nhập.</summary>
    public bool? Receivable { get; init; }
}
