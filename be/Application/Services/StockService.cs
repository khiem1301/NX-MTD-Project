using System.Globalization;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

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

        await LockAsync(movements.Select(m => m.Key), ct);

        var running = new Dictionary<StockKey, decimal>();
        var transactions = new List<StockTransaction>(movements.Count);
        var shortages = new List<string>();
        foreach (var move in movements)
        {
            if (!running.TryGetValue(move.Key, out var balance))
                balance = await _stock.GetOnHandAsync(move.Key, ct);
            balance += move.QtyChange;
            running[move.Key] = balance;

            if (balance < 0)
                shortages.Add($"{await _stock.DescribeAsync(move.Key, ct)}: thiếu {Format(-balance)}");
            else
                transactions.Add(ToTransaction(move, balance));
        }

        if (shortages.Count > 0) return InsufficientStock(shortages);
        foreach (var tx in transactions) await _stock.AddAsync(tx, ct);
        return Result.Success();
    }

    public Task<decimal> GetAvailableAsync(StockKey key, CancellationToken ct) => _stock.GetAvailableAsync(key, ct);

    /// <summary>Khóa theo thứ tự cố định để hai transaction không chờ nhau vòng tròn (deadlock).</summary>
    private async Task LockAsync(IEnumerable<StockKey> keys, CancellationToken ct)
    {
        foreach (var name in keys.Select(k => k.LockName).Distinct().OrderBy(n => n, StringComparer.Ordinal))
            await _locks.AcquireAsync(name, ct);
    }

    private static StockTransaction ToTransaction(StockMovement move, decimal balanceAfter) => new()
    {
        WarehouseType = move.Key.WarehouseType,
        MaterialId = move.Key.MaterialId,
        MaterialColor = move.Key.MaterialColor,
        OrderLineId = move.Key.OrderLineId,
        SourceDocType = move.SourceDocType,
        SourceDocId = move.SourceDocId,
        SourceLineId = move.SourceLineId,
        QtyChange = move.QtyChange,
        BalanceAfter = balanceAfter,
        TransactionDate = move.TransactionDate,
    };

    private static Result InsufficientStock(IEnumerable<string> details) =>
        Result.Failure(ErrorCodes.WarehouseInsufficientStock,
            $"{WarehouseMessages.InsufficientStock}: {string.Join("; ", details)}.");

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));
}
