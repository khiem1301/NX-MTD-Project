using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record StocktakeListItemDto(
    int StocktakeId, string StocktakeCode, string WarehouseType, string? Scope, DateOnly StocktakeDate, string Status,
    DateTime CreatedAt, DateTime? ApprovedAt, int LineCount, int DifferenceLineCount);

/// <summary>CurrentQty = tồn thực lúc xem, chỉ có khi phiếu đang chờ duyệt: để người duyệt thấy tồn đã đổi so với số sổ lúc lập.</summary>
public sealed record StocktakeLineDto(
    int StocktakeLineId, int? MaterialId, string? MaterialCode, string? MaterialName, string? Unit, string? MaterialColor,
    int? OrderLineId, int? ProductComponentMaterialId, string? ComponentName, string? OrderCode, string? ProductCode, string? Color,
    string? SizeCode, decimal BookQty,
    decimal? CountedQty, decimal DifferenceQty, string? Note, decimal? CurrentQty);

public sealed record StocktakeDto(
    int StocktakeId, string StocktakeCode, string WarehouseType, string? Scope, DateOnly StocktakeDate, string Status,
    string? ReturnReason, DateTime CreatedAt, DateTime? ApprovedAt, IReadOnlyList<StocktakeLineDto> Lines);

/// <summary>Kho NVL xác định dòng bằng MaterialId (đã gồm màu); kho BTP / thành phẩm bằng OrderLineId
/// (kho BTP thêm ProductComponentMaterialId cho chi tiết lẻ). CountedQty để trống = khớp sổ.</summary>
public sealed record SaveStocktakeLineDto(int? MaterialId, int? OrderLineId, int? ProductComponentMaterialId,
    decimal? CountedQty, string? Note);

public sealed record SaveStocktakeDto(string WarehouseType, string? Scope, DateOnly StocktakeDate,
    IReadOnlyList<SaveStocktakeLineDto> Lines);

public sealed record StocktakeQuery : PageQuery
{
    public string? Status { get; init; }
    public string? WarehouseType { get; init; }
}
