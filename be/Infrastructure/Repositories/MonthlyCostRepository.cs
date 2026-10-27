using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>Chi phí tháng + kỳ kế toán: không có thủ tục DB riêng nên đọc/ghi thẳng bằng EF, cùng kiểu
/// PayrollRepository/MealRepository (module cùng người sở hữu — Minh).</summary>
public sealed class MonthlyCostRepository : IMonthlyCostRepository
{
    private readonly NxMtdDbContext _context;

    public MonthlyCostRepository(NxMtdDbContext context) => _context = context;

    // ---------- Kỳ kế toán ----------

    public Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct) =>
        _context.AccountingPeriods.FirstOrDefaultAsync(p => p.PeriodMonth == periodMonth, ct);

    public async Task<AccountingPeriod> GetOrCreatePeriodAsync(string periodMonth, CancellationToken ct)
    {
        var existing = await FindPeriodByMonthAsync(periodMonth, ct);
        if (existing is not null) return existing;

        var year = int.Parse(periodMonth[..4]);
        var month = int.Parse(periodMonth[5..7]);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var period = new AccountingPeriod
        {
            PeriodMonth = periodMonth, StartDate = start, EndDate = end, Status = RecordStatus.Open,
        };
        _context.AccountingPeriods.Add(period);

        try
        {
            await _context.SaveChangesAsync(ct);
            return period;
        }
        catch (DbUpdateException)
        {
            // Request khác đã tạo trước (UQ_AccountingPeriod); bỏ theo dõi dòng vừa thêm rồi đọc lại.
            _context.Entry(period).State = EntityState.Detached;
            return await _context.AccountingPeriods.FirstAsync(p => p.PeriodMonth == periodMonth, ct);
        }
    }

    // ---------- Chi phí tháng ----------

    public async Task<IReadOnlyList<MonthlyCost>> GetItemsByPeriodIdAsync(int periodId, CancellationToken ct) =>
        await _context.MonthlyCosts.AsNoTracking()
            .Include(m => m.Product)
            .Where(m => m.PeriodId == periodId)
            .OrderBy(m => m.CostCategoryCode).ThenBy(m => m.ProductId)
            .ToListAsync(ct);

    public async Task RemoveItemsByPeriodIdAsync(int periodId, CancellationToken ct)
    {
        var existing = await _context.MonthlyCosts.Where(m => m.PeriodId == periodId).ToListAsync(ct);
        _context.MonthlyCosts.RemoveRange(existing);
    }

    public void AddItems(IEnumerable<MonthlyCost> items) => _context.MonthlyCosts.AddRange(items);

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);

    // ---------- Import Excel: tra cứu mã hàng cho khoản VARIABLE (UC12) ----------

    public async Task<IReadOnlyDictionary<string, (int ProductId, string ProductName)>> GetProductsByCodesAsync(
        IReadOnlyCollection<string> productCodes, CancellationToken ct)
    {
        if (productCodes.Count == 0)
            return new Dictionary<string, (int, string)>();

        var rows = await _context.Products.AsNoTracking()
            .Where(p => productCodes.Contains(p.ProductCode))
            .Select(p => new { p.ProductCode, p.ProductId, p.ProductName })
            .ToListAsync(ct);

        return rows.ToDictionary(p => p.ProductCode, p => (p.ProductId, p.ProductName),
            StringComparer.OrdinalIgnoreCase);
    }

    // ---------- Đối soát suất ăn (UC30/UC12) ----------

    public async Task<decimal> GetMealsTotalAmountAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct) =>
        await _context.VwDailyMealTotals.AsNoTracking()
            .Where(m => m.MealDate >= fromDate && m.MealDate <= toDate)
            .SumAsync(m => (decimal?)m.TotalAmount, ct) ?? 0m;
}
