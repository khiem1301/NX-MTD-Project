using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Phiếu trả NVL / phế phẩm / BTP của tổ cắt, tổ may (DEV-ALL-112). OrderCode / WorkOrderCode có khi phiếu gắn đơn / lệnh cắt.</summary>
public sealed record MaterialReturnListItemDto(
    int MaterialReturnId, string ReturnCode, string WarehouseType, int TeamId, string TeamName, string? OrderCode,
    string? WorkOrderCode, string Status, DateTime CreatedAt, string? ConfirmedByName, DateTime? ConfirmedAt,
    decimal TotalReturnedQty);

/// <summary>Dòng phiếu trả. NVL: ItemCode / ItemName là mã và tên NVL; BTP: mã hàng và "màu/size" (đơn vị bộ).</summary>
public sealed record MaterialReturnLineDto(
    int MaterialReturnLineId, int? MaterialId, int? OrderLineId, string ItemCode, string ItemName, string Unit,
    string ItemColor, bool IsScrap, decimal ReturnedQty, decimal? ConfirmedQty, string? DiscrepancyReason);

public sealed record MaterialReturnDto(
    int MaterialReturnId, string ReturnCode, string WarehouseType, int TeamId, string TeamName, string? OrderCode,
    string? WorkOrderCode, string? Note, string Status, DateTime CreatedAt, string? ConfirmedByName, DateTime? ConfirmedAt,
    string? RejectReason, IReadOnlyList<MaterialReturnLineDto> Lines);

/// <summary>
/// Hàng tổ còn trả được và trần. NVL của tổ cắt: theo lệnh cắt, UsedQty = số mét đã cắt của lệnh (null khi có bàn cắt chưa nhập
/// số mét, chưa tính được). NVL của tổ may: tổng đã nhận ở mọi phiếu cấp. BTP: theo dòng đơn, UsedQty = số đã QC đạt.
/// RemainingQty (trần số trả) = đã nhận − đã dùng (nếu tính được) − đã trả / đang chờ trả.
/// </summary>
public sealed record ReturnableLineDto(
    int? MaterialId, int? OrderLineId, string ItemCode, string ItemName, string Unit, string ItemColor,
    decimal ReceivedQty, decimal? UsedQty, decimal ReturnedQty, decimal RemainingQty);

/// <summary>WarehouseType: MATERIAL hoặc SEMI_FINISHED. Tổ cắt trả NVL kèm WorkOrderId, tổ may trả BTP kèm OrderId.</summary>
public sealed record ReturnableQuery(string WarehouseType, int? WorkOrderId, int? OrderId);

/// <summary>NVL: MaterialId (kể cả phế phẩm); BTP: OrderLineId (cả bộ của dòng đơn).</summary>
public sealed record CreateReturnLineDto(int? MaterialId, int? OrderLineId, decimal ReturnedQty);

public sealed record CreateMaterialReturnDto(string WarehouseType, int? WorkOrderId, int? OrderId, string? Note,
    IReadOnlyList<CreateReturnLineDto> Lines);

public sealed record ConfirmReturnLineDto(int MaterialReturnLineId, decimal ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmMaterialReturnDto(IReadOnlyList<ConfirmReturnLineDto> Lines);

public sealed record MaterialReturnQuery : PageQuery
{
    public string? Status { get; init; }
}
