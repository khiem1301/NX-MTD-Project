using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IInventoryService
{
    Task<Result<PagedResult<StockBalanceDto>>> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<StockCardEntryDto>>> GetStockCardAsync(StockCardQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<TeamPieceHoldingDto>>> GetTeamHoldingsAsync(int? sewingTeamId, int? orderId,
        CancellationToken ct);
}
