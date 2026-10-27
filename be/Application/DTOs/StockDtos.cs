using NX_MTD.Application.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.DTOs;

/// <summary>Khóa một dòng tồn: kho NVL theo (NVL, màu); kho BTP / thành phẩm theo dòng đơn.</summary>
/// <param name="ProductComponentMaterialId">Chỉ kho BTP: null = bộ đủ (cắt xong cả bộ); có giá trị = chi tiết × vải (cắt bù), tồn tách riêng.</param>
public sealed record StockKey(string WarehouseType, int? MaterialId, string? MaterialColor, int? OrderLineId,
    int? ProductComponentMaterialId = null)
{
    public static StockKey Material(int materialId, string color) => new(WarehouseTypes.Material, materialId, color, null);
    public static StockKey SemiFinished(int orderLineId, int? productComponentMaterialId = null) =>
        new(WarehouseTypes.SemiFinished, null, null, orderLineId, productComponentMaterialId);
    public static StockKey Finished(int orderLineId) => new(WarehouseTypes.Finished, null, null, orderLineId);

    public string LockName => $"stock:{WarehouseType}:{MaterialId}:{MaterialColor}:{OrderLineId}:{ProductComponentMaterialId}";
}

/// <summary>Một bút toán thẻ kho cần ghi; QtyChange dương = nhập, âm = xuất.</summary>
public sealed record StockMovement(StockKey Key, decimal QtyChange, string SourceDocType, int SourceDocId,
    int? SourceLineId, DateOnly TransactionDate);

public sealed record StockRequirement(StockKey Key, decimal Qty);

public sealed record StockBalanceQuery : PageQuery
{
    public string WarehouseType { get; init; } = WarehouseTypes.Material;
    public int? OrderId { get; init; }
    public string? Color { get; init; }
    public string? SizeCode { get; init; }
}

public sealed record StockBalanceDto(
    string WarehouseType, int? MaterialId, string? MaterialCode, string? MaterialName, string? MaterialType,
    string? Unit, string? MaterialColor, int? OrderLineId, int? OrderId, string? OrderCode, string? ProductCode,
    string? ProductName, string? Color, string? SizeCode, decimal OnHandQty, decimal ReservedQty, decimal AvailableQty,
    int? ProductComponentMaterialId, string? ComponentName);

/// <param name="ProductComponentMaterialId">Chỉ kho BTP: để trống = bộ đủ; có giá trị = chi tiết × vải (hàng cắt bù).</param>
public sealed record StockCardQuery(string WarehouseType, int? MaterialId, string? MaterialColor, int? OrderLineId,
    int? ProductComponentMaterialId = null);

public sealed record StockCardEntryDto(
    long StockTransactionId, DateOnly TransactionDate, DateTime CreatedAt, string SourceDocType, int SourceDocId,
    string? SourceDocCode, decimal QtyChange, decimal BalanceAfter);

public sealed record TeamPieceHoldingDto(
    int SewingTeamId, string TeamName, int OrderLineId, string OrderCode, string ProductCode, string Color,
    string SizeCode, int ReceivedFromWarehouse, int HandedToWorkers, int RetrievedFromWorkers, int HeldByTeam);
