namespace NX_MTD.Application.DTOs;

public sealed record OrderMaterialNormDto(
    int OrderMaterialNormId, int ProductId, string ProductCode, string ComponentName, string? GarmentColor,
    string? SizeCode, decimal QtyPerPiece, decimal WastagePercent);

/// <summary>
/// PurchaseQty, Source là cột tính của DB (chỉ đọc). NormRequiredQty = số cần tính từ định mức × số đặt,
/// để đối chiếu với RequiredQty người dùng nhập. ReceivedQty = tổng số đạt của các phiếu nhập đã xác nhận.
/// </summary>
public sealed record OrderMaterialDto(
    int OrderMaterialId, int OrderId, int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit, string MaterialColor, decimal RequiredQty, decimal CustomerQty, DateOnly? CustomerExpectedDate,
    decimal? PurchaseQty, string? Source, string? Note, decimal NormRequiredQty, decimal ReceivedQty,
    IReadOnlyList<OrderMaterialNormDto> Norms);

public sealed record SaveNormDto(int ProductId, string? ComponentName, string? GarmentColor, string? SizeCode,
    decimal QtyPerPiece, decimal WastagePercent);

/// <summary>Phần chung của tạo / sửa NVL của đơn (dùng chung validator).</summary>
public interface IOrderMaterialInput
{
    int MaterialId { get; }
    string? MaterialColor { get; }
    decimal RequiredQty { get; }
    decimal CustomerQty { get; }
    DateOnly? CustomerExpectedDate { get; }
    string? Note { get; }
    IReadOnlyList<SaveNormDto>? Norms { get; }
}

public sealed record CreateOrderMaterialDto(
    int OrderId, int MaterialId, string? MaterialColor, decimal RequiredQty, decimal CustomerQty,
    DateOnly? CustomerExpectedDate, string? Note, IReadOnlyList<SaveNormDto>? Norms) : IOrderMaterialInput;

public sealed record UpdateOrderMaterialDto(
    int MaterialId, string? MaterialColor, decimal RequiredQty, decimal CustomerQty, DateOnly? CustomerExpectedDate,
    string? Note, IReadOnlyList<SaveNormDto>? Norms) : IOrderMaterialInput;
