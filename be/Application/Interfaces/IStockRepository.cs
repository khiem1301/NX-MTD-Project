using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface IStockRepository
{
    /// <summary>Tồn thực = StockBalance.OnHandQty (chưa trừ giữ chỗ); mặt hàng chưa có dòng tồn = 0.</summary>
    Task<decimal> GetOnHandAsync(StockKey key, CancellationToken ct);

    /// <summary>Khả dụng = tồn thực − giữ chỗ (vw_StockBalance).</summary>
    Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct);

    /// <summary>Tên dễ đọc của dòng tồn để báo lỗi: "Vải chính A (Đỏ)" / "MH01 Đỏ-M".</summary>
    Task<string> DescribeAsync(StockKey key, CancellationToken ct);

    /// <summary>Ghi một bút toán qua dbo.usp_Stock_Post (thủ tục tự khóa dòng tồn, cập nhật tồn và thêm thẻ kho). Phải nằm trong transaction của caller.</summary>
    Task PostAsync(StockMovement movement, CancellationToken ct);

    /// <summary>Dòng tồn của mặt hàng; chưa có thì tạo mới với tồn 0 (kiểm kê mặt hàng chưa từng nhập). Lưu ngay.</summary>
    Task<int> GetOrCreateBalanceIdAsync(StockKey key, CancellationToken ct);

    Task<StockKey?> GetKeyAsync(int stockBalanceId, CancellationToken ct);

    Task<PagedResult<StockBalanceDto>> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct);
    Task<IReadOnlyList<StockCardEntryDto>> GetStockCardAsync(StockKey key, CancellationToken ct);
    Task<IReadOnlyList<TeamPieceHoldingDto>> GetTeamHoldingsAsync(int? sewingTeamId, int? orderId, CancellationToken ct);
}
