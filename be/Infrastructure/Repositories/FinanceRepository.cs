using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>Doanh thu (vw_MonthlyRevenue, chỉ đọc) + hòa vốn (BreakEvenResult). Đọc chéo OutputRecord/
/// OrderLine (Kiều/Thái) và DailyTimesheet/Payroll/MonthlyCost/AccountingPeriod (Minh) cho công thức
/// LABOUR_HOURS — chỉ đọc (AsNoTracking), không ghi vào bảng của người khác.</summary>
public sealed class FinanceRepository : IFinanceRepository
{
    private readonly NxMtdDbContext _context;

    public FinanceRepository(NxMtdDbContext context) => _context = context;

    // ---------- Doanh thu ----------

    public async Task<IReadOnlyList<VwMonthlyRevenue>> GetRevenueRowsAsync(
        string? periodMonth, int? orderId, int? productId, CancellationToken ct)
    {
        var query = _context.VwMonthlyRevenues.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(periodMonth)) query = query.Where(r => r.PeriodMonth == periodMonth);
        if (orderId.HasValue) query = query.Where(r => r.OrderId == orderId.Value);
        if (productId.HasValue) query = query.Where(r => r.ProductId == productId.Value);

        return await query
            .OrderByDescending(r => r.PeriodMonth).ThenBy(r => r.OrderId).ThenBy(r => r.ProductId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetOrderCodesAsync(
        IReadOnlyCollection<int> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return new Dictionary<int, string>();

        return await _context.CustomerOrders.AsNoTracking()
            .Where(o => orderIds.Contains(o.OrderId))
            .ToDictionaryAsync(o => o.OrderId, o => o.OrderCode, ct);
    }

    public async Task<IReadOnlyDictionary<int, (string ProductCode, string ProductName)>> GetProductsAsync(
        IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0) return new Dictionary<int, (string, string)>();

        var rows = await _context.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName })
            .ToListAsync(ct);

        return rows.ToDictionary(p => p.ProductId, p => (p.ProductCode, p.ProductName));
    }

    // ---------- Cổng điều kiện tính hòa vốn (GBR-17) ----------

    public Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct) =>
        _context.AccountingPeriods.FirstOrDefaultAsync(p => p.PeriodMonth == periodMonth, ct);

    public Task<bool> IsPayrollApprovedAsync(int periodId, CancellationToken ct) =>
        _context.Payrolls.AsNoTracking()
            .AnyAsync(p => p.PeriodId == periodId && p.Status == PayrollStatus.Approved, ct);

    public Task<bool> HasMonthlyCostAsync(int periodId, CancellationToken ct) =>
        _context.MonthlyCosts.AsNoTracking().AnyAsync(m => m.PeriodId == periodId, ct);

    // ---------- Chi phí tháng ----------

    public async Task<IReadOnlyList<MonthlyCost>> GetMonthlyCostsAsync(int periodId, CancellationToken ct) =>
        await _context.MonthlyCosts.AsNoTracking().Where(m => m.PeriodId == periodId).ToListAsync(ct);

    // ---------- Phân bổ LABOUR_HOURS (DEV-MI-13-1) ----------

    public async Task<IReadOnlyList<(int WorkerId, DateOnly WorkDate, int ProductId, int Quantity)>>
        GetOutputForLabourAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var rows = await _context.OutputRecords.AsNoTracking()
            .Where(o => o.WorkDate >= fromDate && o.WorkDate <= toDate)
            .GroupBy(o => new { o.WorkerId, o.WorkDate, o.OrderLine.ProductId })
            .Select(g => new { g.Key.WorkerId, g.Key.WorkDate, g.Key.ProductId, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        return rows.Select(r => (r.WorkerId, r.WorkDate, r.ProductId, r.Quantity)).ToList();
    }

    public async Task<IReadOnlyDictionary<(int WorkerId, DateOnly WorkDate), decimal>> GetWorkedHoursAsync(
        IReadOnlyCollection<int> workerIds, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        if (workerIds.Count == 0) return new Dictionary<(int, DateOnly), decimal>();

        var rows = await _context.DailyTimesheets.AsNoTracking()
            .Where(t => workerIds.Contains(t.EmployeeId) && t.WorkDate >= fromDate && t.WorkDate <= toDate)
            .Select(t => new { t.EmployeeId, t.WorkDate, t.WorkedHours })
            .ToListAsync(ct);

        return rows.ToDictionary(t => (t.EmployeeId, t.WorkDate), t => t.WorkedHours);
    }

    // ---------- Kết quả hòa vốn ----------

    public void AddBreakEvenResults(IEnumerable<BreakEvenResult> results) =>
        _context.BreakEvenResults.AddRange(results);

    public async Task<IReadOnlyList<BreakEvenResult>> GetResultsAsync(
        int periodId, int? productId, CancellationToken ct)
    {
        var query = _context.BreakEvenResults.AsNoTracking()
            .Include(r => r.Product)
            .Where(r => r.PeriodId == periodId);
        if (productId.HasValue) query = query.Where(r => r.ProductId == productId.Value);

        return await query.OrderByDescending(r => r.CalculatedAt).ThenBy(r => r.Product.ProductCode).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
