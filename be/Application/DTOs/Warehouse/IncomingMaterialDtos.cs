using NX_MTD.Domain.Constants;
using NX_MTD.Application.Common;
namespace NX_MTD.Application.DTOs;

/// <summary>Bảng NVL của một đơn; Supply là một giá trị của <see cref="OrderMaterialSupplies"/>, ReceiptState của <see cref="OrderMaterialReceiptStates"/>.</summary>
public sealed record OrderMaterialPageQuery : PageQuery
{
    public int OrderId { get; init; }
    public string? Supply { get; init; }
    public string? ReceiptState { get; init; }
}

/// <summary>NVL khách sẽ cấp mà kho chưa nhận đủ (số lần giao dự kiến do Quản đốc khai báo ở "NVL của đơn"; đã nhận = số phiếu nhập đã xác nhận).</summary>
public sealed record IncomingCustomerMaterialDto(
    int OrderMaterialId, int OrderId, string OrderCode, string MaterialCode, string MaterialName, string MaterialColor,
    string Unit, decimal CustomerQty, decimal ReceivedQty, decimal OutstandingQty, short? ExpectedDeliveryCount, int ReceivedCount);

/// <summary>Dòng đơn mua đã đặt hàng mà kho chưa nhận đủ (còn phải nhận = đặt − đã nhận − đã chốt không bù).</summary>
public sealed record IncomingSupplierMaterialDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, string? SupplierName, short? ExpectedDeliveryCount, int ReceivedCount,
    int PurchaseOrderLineId, string MaterialCode, string MaterialName, string MaterialColor, string Unit,
    decimal OrderedQty, decimal ReceivedQty, decimal OutstandingQty, string GoodsStatus);

/// <summary>Dòng đơn mua đã đóng vì giao thiếu (CLOSED_SHORT): phần còn thiếu đã chốt không bù, không nhận tiếp được.</summary>
public sealed record ClosedShortPurchaseDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, string? SupplierName,
    int PurchaseOrderLineId, string MaterialCode, string MaterialName, string MaterialColor, string Unit,
    decimal OrderedQty, decimal ReceivedQty, decimal NotReplenishedQty);

public sealed record IncomingMaterialsDto(
    IReadOnlyList<IncomingCustomerMaterialDto> Customer, IReadOnlyList<IncomingSupplierMaterialDto> Supplier,
    IReadOnlyList<ClosedShortPurchaseDto> ClosedShort);
