using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>UC36 "Báo cáo kế toán": đọc DailyTimesheet/DailyMeal/MonthlyCost (bảng của Minh) +
/// Vehicle/VehicleTrip (bảng của Thảo, chỉ đọc) bằng EF thẳng, không có thủ tục DB riêng cho báo cáo này.
/// Cùng kiểu dedicated repository như FinanceRepository/MonthlyCostRepository.</summary>
public sealed class AccountingReportRepository : IAccountingReportRepository
{
    private readonly NxMtdDbContext _context;

    public AccountingReportRepository(NxMtdDbContext context) => _context = context;

    public Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct) =>
        _context.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PeriodMonth == periodMonth, ct);

    // ---------- 1. Công + tăng ca ----------

    public async Task<IReadOnlyList<(int EmployeeId, string EmployeeCode, string FullName, string? TeamCode, string? GradeCode,
            decimal WorkdayValue, bool IsSunday)>>
        GetTimesheetRowsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var rows = await _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate >= fromDate && t.WorkDate <= toDate)
            .Select(t => new
            {
                t.EmployeeId, t.Employee.EmployeeCode, t.Employee.FullName,
                TeamCode = t.Employee.Team == null ? null : t.Employee.Team.TeamCode,
                GradeCode = t.Employee.CurrentGradeCode,
                t.WorkdayValue, IsSunday = t.IsSunday ?? false,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => (r.EmployeeId, r.EmployeeCode, r.FullName, r.TeamCode, r.GradeCode, r.WorkdayValue, r.IsSunday))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, EmployeeProduction>> GetProductionTotalsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        // Mỗi lần báo sản lượng đã gắn mức giá có hiệu lực lúc báo (PieceRateId): giá đổi giữa tháng thì tách dòng theo mức giá.
        var output = await _context.OutputRecords.AsNoTracking()
            .Where(o => o.WorkDate >= fromDate && o.WorkDate <= toDate)
            .GroupBy(o => new { o.WorkerId, o.Operation.OperationCode, o.Operation.OperationName, o.PieceRate.Rate })
            .Select(g => new { g.Key.WorkerId, g.Key.OperationCode, g.Key.OperationName, g.Key.Rate, Qty = g.Sum(o => o.Quantity) })
            .ToListAsync(ct);
        var defects = await _context.DefectRecords.AsNoTracking()
            .Where(d => d.WorkDate >= fromDate && d.WorkDate <= toDate && d.Status != DefectStatuses.Cancelled)
            .GroupBy(d => d.ResponsibleWorkerId)
            .Select(g => new { WorkerId = g.Key, Qty = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.WorkerId, x => x.Qty, ct);

        var lines = output.ToLookup(x => x.WorkerId);
        return lines.Select(g => g.Key).Union(defects.Keys).ToDictionary(
            id => id,
            id => new EmployeeProduction(
                lines[id].OrderBy(x => x.OperationCode).ThenBy(x => x.Rate)
                    .Select(x => new OperationOutputLine(x.OperationCode, x.OperationName, x.Rate, x.Qty)).ToList(),
                defects.GetValueOrDefault(id)));
    }

    // ---------- 2. Suất ăn + đối soát bếp ----------

    public async Task<(int DaysWithData, int TotalMeals, decimal TotalAmount)> GetMealTotalsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var summary = await _context.VwDailyMealTotals.AsNoTracking()
            .Where(m => m.MealDate >= fromDate && m.MealDate <= toDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Days = g.Count(),
                TotalMeals = g.Sum(m => m.TotalMeals ?? 0),
                TotalAmount = g.Sum(m => m.TotalAmount ?? 0m),
            })
            .FirstOrDefaultAsync(ct);

        return summary is null ? (0, 0, 0m) : (summary.Days, summary.TotalMeals, summary.TotalAmount);
    }

    public Task<decimal?> GetMonthlyCostMealsAmountAsync(int periodId, CancellationToken ct) =>
        _context.MonthlyCosts.AsNoTracking()
            .Where(m => m.PeriodId == periodId && m.CostCategoryCode == MonthlyCostCategory.Meals)
            .Select(m => (decimal?)m.Amount)
            .FirstOrDefaultAsync(ct);

    // ---------- 3. Sổ xe + xăng (Vehicle/VehicleTrip, Thảo — chỉ đọc) ----------

    public async Task<IReadOnlyList<VehicleTrip>> GetVehicleTripsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct) =>
        await _context.VehicleTrips.AsNoTracking()
            .Where(t => t.TripDate >= fromDate && t.TripDate <= toDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, (string PlateNumber, string VehicleType)>> GetVehiclesAsync(
        IReadOnlyCollection<int> vehicleIds, CancellationToken ct)
    {
        if (vehicleIds.Count == 0)
            return new Dictionary<int, (string, string)>();

        var rows = await _context.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.VehicleId))
            .Select(v => new { v.VehicleId, v.PlateNumber, v.VehicleType })
            .ToListAsync(ct);

        return rows.ToDictionary(v => v.VehicleId, v => (v.PlateNumber, v.VehicleType));
    }
}
