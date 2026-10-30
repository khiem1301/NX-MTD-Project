namespace NX_MTD.Application.DTOs;

/// <summary>
/// PurchaseQty, Source là cột tính của DB (chỉ đọc). ReceivedQty = tổng số đạt của các phiếu nhập đã xác nhận.
/// ExpectedDeliveryCount = số lần khách dự kiến giao (bắt buộc khi khách cấp), ReceivedCount = số phiếu nhập đã xác nhận.
/// MaterialColor = Material.Color (26/10: màu nằm trong NVL, chọn NVL là đã chọn màu).
/// </summary>
public sealed record OrderMaterialDto(
    int OrderMaterialId, int OrderId, int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit, string MaterialColor, decimal RequiredQty, decimal CustomerQty, short? ExpectedDeliveryCount,
    int ReceivedCount, decimal? PurchaseQty, string? Source, string? Note, decimal ReceivedQty);

/// <summary>Phần chung của tạo / sửa NVL của đơn (dùng chung validator).</summary>
public interface IOrderMaterialInput
{
    int MaterialId { get; }
    decimal RequiredQty { get; }
    decimal CustomerQty { get; }
    short? ExpectedDeliveryCount { get; }
    string? Note { get; }
}

public sealed record CreateOrderMaterialDto(
    int OrderId, int MaterialId, decimal RequiredQty, decimal CustomerQty,
    short? ExpectedDeliveryCount, string? Note) : IOrderMaterialInput;

public sealed record UpdateOrderMaterialDto(
    int MaterialId, decimal RequiredQty, decimal CustomerQty, short? ExpectedDeliveryCount,
    string? Note) : IOrderMaterialInput;
