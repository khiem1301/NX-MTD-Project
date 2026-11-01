using System.Globalization;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>
/// Ghi kho qua thủ tục dbo.usp_Stock_Post của database (25/10): thủ tục khóa dòng tồn, chặn tồn âm và thêm thẻ kho.
/// Service vẫn kiểm trước toàn bộ lô bằng StockBalance để báo đủ mọi mặt hàng thiếu thay vì chỉ mặt hàng đầu tiên.
/// </summary>
public sealed class StockService : IStockService
{
    private readonly IStockRepository _stock;
    private readonly IAppLock _locks;

    public StockService(IStockRepository stock, IAppLock locks)
    {
        _stock = stock; _locks = locks;
    }

    public async Task<Result> EnsureAvailableAsync(IReadOnlyList<StockRequirement> requirements, CancellationToken ct)
    {
        var needed = requirements.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        // Khóa để hai phiếu cùng giữ chỗ một mặt hàng không cùng thấy "còn đủ" (giữ chỗ chưa nằm trong StockBalance).
        await LockAsync(needed.Keys, ct);

        var shortages = new List<string>();
        foreach (var (key, qty) in needed)
        {
            var available = await _stock.GetAvailableAsync(key, ct);
            if (available < qty)
                shortages.Add($"{await _stock.DescribeAsync(key, ct)}: cần {Format(qty)}, khả dụng {Format(available)}");
        }
        return shortages.Count == 0 ? Result.Success() : InsufficientStock(shortages);
    }

    public async Task<Result> PostAsync(IReadOnlyList<StockMovement> movements, CancellationToken ct)
    {
        if (movements.Any(m => m.QtyChange == 0))
            throw new ArgumentException("Bút toán thẻ kho phải có số lượng khác 0.", nameof(movements));

        var running = new Dictionary<StockKey, decimal>();
        var shortages = new List<string>();
        foreach (var move in movements)
        {
            if (!running.TryGetValue(move.Key, out var balance))
                balance = await _stock.GetOnHandAsync(move.Key, ct);
            balance += move.QtyChange;
            running[move.Key] = balance;
            if (balance < 0)
                shortages.Add($"{await _stock.DescribeAsync(move.Key, ct)}: thiếu {Format(-balance)}");
        }
        if (shortages.Count > 0) return InsufficientStock(shortages);

        foreach (var move in movements) await _stock.PostAsync(move, ct);
        return Result.Success();
    }

    public Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct) => _stock.GetAvailableAsync(key, ct);

    /// <summary>Khóa theo thứ tự cố định để hai transaction không chờ nhau vòng tròn (deadlock).</summary>
    private async Task LockAsync(IEnumerable<StockKey> keys, CancellationToken ct)
    {
        foreach (var name in keys.Select(k => k.LockName).Distinct().OrderBy(n => n, StringComparer.Ordinal))
            await _locks.AcquireAsync(name, ct);
    }

    private static Result InsufficientStock(IEnumerable<string> details) =>
        Result.Failure(ErrorCodes.WarehouseInsufficientStock,
            $"{WarehouseMessages.InsufficientStock}: {string.Join("; ", details)}.");

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));
}
