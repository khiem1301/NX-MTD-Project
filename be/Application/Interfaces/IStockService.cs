using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Cổng duy nhất ghi thẻ kho cho 3 kho. Mọi hàm ghi phải chạy trong transaction của IUnitOfWork;
/// hàm tự khóa theo dòng tồn để hai người thao tác cùng lúc không làm tồn âm.
/// </summary>
public interface IStockService
{
    /// <summary>Khóa các dòng tồn rồi kiểm tra đủ số khả dụng (dùng khi lập phiếu giữ chỗ).</summary>
    Task<Result> EnsureAvailableAsync(IReadOnlyList<StockRequirement> requirements, CancellationToken ct);

    /// <summary>Ghi bút toán thẻ kho, tính BalanceAfter; tồn sau ghi âm → Failure, không ghi gì.</summary>
    Task<Result> PostAsync(IReadOnlyList<StockMovement> movements, CancellationToken ct);

    Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct);
}
