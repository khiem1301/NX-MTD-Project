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
    IReadOnlyList<PurchaseOrderLineDto> Lines, short? ExpectedDeliveryCount = null, int ReceivedCount = 0);

/// <summary>
/// Chọn NVL có sẵn (MaterialId) hoặc nhập NVL mới (NewMaterialName + NewMaterialUnit): NVL mới được thêm vào
/// danh mục (phụ liệu, nhóm OTHER, màu NewMaterialColor), nếu đã có NVL phụ liệu trùng tên + màu thì dùng lại.
/// NVL có sẵn đã gồm màu (26/10) nên không nhập màu riêng.
/// </summary>
public sealed record SavePurchaseOrderLineDto(int? MaterialId, string? NewMaterialName, string? NewMaterialUnit,
    string? NewMaterialColor, decimal OrderedQty, decimal UnitPrice);

/// <summary>OrderId để trống = đơn mua dự trữ (kim, chỉ... dùng chung nhiều đơn). Bắt buộc có nhà cung cấp đang hợp tác; một đơn mua không cần mua hết NVL của đơn hàng.</summary>
/// <summary>Tạo đơn mua = đặt hàng luôn: đã có nhà cung cấp, số lượng, đơn giá, ngày đặt và số lần giao dự kiến, đơn vào trạng thái đã đặt.</summary>
public sealed record CreatePurchaseOrderDto(int? OrderId, int SupplierId, DateOnly OrderDate, short ExpectedDeliveryCount,
    string? Note, IReadOnlyList<SavePurchaseOrderLineDto> Lines, DateOnly? ExpectedDeliveryDate = null,
    DateOnly? ExpectedDeliveryDateTo = null);

public sealed record PurchaseOrderQuery : PageQuery
{
    public string? GoodsStatus { get; init; }

    /// <summary>true = chỉ đơn mua dự trữ (không gắn đơn hàng).</summary>
    public bool? StockOnly { get; init; }

    /// <summary>true = chỉ đơn đã đặt hàng và còn phải nhận (ORDERED / PARTIALLY_RECEIVED): Thủ kho chọn khi lập phiếu nhập.</summary>
    public bool? Receivable { get; init; }
}
