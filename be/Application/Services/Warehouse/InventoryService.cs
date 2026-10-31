using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>UC24: xem tồn 3 kho, thẻ kho và BTP tổ may đã nhận / còn trên chuyền.</summary>
public sealed class InventoryService : IInventoryService
{
    private readonly IStockRepository _stock;

    public InventoryService(IStockRepository stock) => _stock = stock;

    public async Task<Result<PagedResult<StockBalanceDto>>> GetBalancesAsync(StockBalanceQuery query,
        CancellationToken ct) =>
        Result<PagedResult<StockBalanceDto>>.Success(await _stock.GetBalancesAsync(query, ct));

    public async Task<Result<IReadOnlyList<StockCardEntryDto>>> GetStockCardAsync(StockCardQuery query,
        CancellationToken ct)
    {
        var key = query.WarehouseType == WarehouseTypes.Material
            ? StockKey.Material(query.MaterialId!.Value)
            : new StockKey(query.WarehouseType, null, query.OrderLineId!.Value, query.ProductComponentMaterialId);
        return Result<IReadOnlyList<StockCardEntryDto>>.Success(await _stock.GetStockCardAsync(key, ct));
    }

    public async Task<Result<IReadOnlyList<TeamPieceHoldingDto>>> GetTeamHoldingsAsync(int? sewingTeamId,
        int? orderId, CancellationToken ct) =>
        Result<IReadOnlyList<TeamPieceHoldingDto>>.Success(await _stock.GetTeamHoldingsAsync(sewingTeamId, orderId, ct));
}
