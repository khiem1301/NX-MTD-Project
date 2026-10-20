using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IInventoryService
{
    Task<Result<PagedResult<StockBalanceDto>>> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<StockCardEntryDto>>> GetStockCardAsync(StockCardQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<TeamPieceHoldingDto>>> GetTeamHoldingsAsync(int? sewingTeamId, int? orderId,
        CancellationToken ct);

    /// <summary>
    /// BTP tổ may đang giữ của một dòng đơn (đã nhận từ kho − đã giao CN + đã thu hồi).
    /// Dùng cho trần sản lượng của luồng may (KI-07, Kiều).
    /// </summary>
    Task<int> GetTeamHeldQtyAsync(int sewingTeamId, int orderLineId, CancellationToken ct);
}
