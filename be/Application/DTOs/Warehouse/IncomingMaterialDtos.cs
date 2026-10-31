namespace NX_MTD.Application.DTOs;

/// <summary>
/// Đơn hàng còn việc về NVL cho Quản đốc / Admin: đơn đang chạy mà NVL chưa về đủ. MaterialLines = 0 nghĩa là chưa ai khai báo
/// "NVL của đơn". ToBuyQty = Σ số phải mua đã khai báo; CustomerQty = Σ số khách cấp đã khai báo.
/// </summary>
public sealed record PendingMaterialOrderDto(
    int OrderId, string OrderCode, string CustomerName, DateOnly DueDate, string Status, string MaterialStatus,
    int MaterialLines, decimal CustomerQty, decimal ToBuyQty);

/// <summary>NVL khách sẽ cấp mà kho chưa nhận đủ (số lần giao dự kiến do Quản đốc khai báo ở "NVL của đơn"; đã nhận = số phiếu nhập đã xác nhận).</summary>
public sealed record IncomingCustomerMaterialDto(
    int OrderMaterialId, int OrderId, string OrderCode, string MaterialCode, string MaterialName, string MaterialColor,
    string Unit, decimal CustomerQty, decimal ReceivedQty, decimal OutstandingQty, short? ExpectedDeliveryCount, int ReceivedCount);

/// <summary>Dòng đơn mua đã đặt hàng mà kho chưa nhận đủ (còn phải nhận = đặt − đã nhận − đã chốt không bù).</summary>
public sealed record IncomingSupplierMaterialDto(
    int PurchaseOrderId, string PoCode, int? OrderId, string? OrderCode, string? SupplierName, short? ExpectedDeliveryCount, int ReceivedCount,
    int PurchaseOrderLineId, string MaterialCode, string MaterialName, string MaterialColor, string Unit,
    decimal OrderedQty, decimal ReceivedQty, decimal OutstandingQty);

public sealed record IncomingMaterialsDto(
    IReadOnlyList<IncomingCustomerMaterialDto> Customer, IReadOnlyList<IncomingSupplierMaterialDto> Supplier);
