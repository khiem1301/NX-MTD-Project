using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface IStockRepository
{
    /// <summary>Tồn thực = Σ QtyChange trên thẻ kho (chưa trừ giữ chỗ).</summary>
    Task<decimal> GetOnHandAsync(StockKey key, CancellationToken ct);

    /// <summary>Khả dụng = tồn thực − giữ chỗ (vw_StockBalance).</summary>
    Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct);

    /// <summary>Tên dễ đọc của dòng tồn để báo lỗi: "Vải chính A (Đỏ)" / "MH01 Đỏ-M".</summary>
    Task<string> DescribeAsync(StockKey key, CancellationToken ct);

    Task AddAsync(StockTransaction transaction, CancellationToken ct);

    Task<PagedResult<StockBalanceDto>> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct);
    Task<IReadOnlyList<StockCardEntryDto>> GetStockCardAsync(StockKey key, CancellationToken ct);
    Task<IReadOnlyList<TeamPieceHoldingDto>> GetTeamHoldingsAsync(int? sewingTeamId, int? orderId, CancellationToken ct);
}
