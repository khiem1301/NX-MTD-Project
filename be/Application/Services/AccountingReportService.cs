using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-19 — UC36 "Báo cáo kế toán", CHỈ Kế toán. Ba báo cáo vận hành theo tháng: công + tăng ca
/// (DailyTimesheet), suất ăn + đối soát bếp (DailyMeal/MonthlyCost MEALS), km + xăng theo xe
/// (Vehicle/VehicleTrip — bảng của Thảo, chỉ đọc, chưa có BE/FE nào khác dùng tới). Không có bảng khóa
/// tháng riêng cho báo cáo — đọc trực tiếp theo khoảng ngày suy ra từ periodMonth, không cần
/// AccountingPeriod (báo cáo chỉ xem, không ghi, nên không cần tạo kỳ nếu chưa có).
/// </summary>
public sealed class AccountingReportService : IAccountingReportService
{
    private static readonly Regex PeriodMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IAccountingReportRepository _repo;
    private readonly IAccountingReportExcelWriter _excelWriter;
    private readonly ILogger<AccountingReportService> _logger;

    public AccountingReportService(IAccountingReportRepository repo, IAccountingReportExcelWriter excelWriter,
        ILogger<AccountingReportService> logger)
    {
        _repo = repo;
        _excelWriter = excelWriter;
        _logger = logger;
    }

    public async Task<Result<WorkdayOvertimeReportDto>> GetWorkdayOvertimeReportAsync(
        string periodMonth, CancellationToken ct)
    {
        var validation = ValidateMonth<WorkdayOvertimeReportDto>(periodMonth);
        if (validation is not null) return validation;

        var (start, end) = ComputePeriodDateRange(periodMonth);
        var rows = await _repo.GetTimesheetRowsAsync(start, end, ct);

        var byEmployee = rows
            .GroupBy(r => (r.EmployeeId, r.EmployeeCode, r.FullName, r.TeamName))
            .Select(g => new WorkdayOvertimeRowDto(
                g.Key.EmployeeId, g.Key.EmployeeCode, g.Key.FullName, g.Key.TeamName,
                RegularWorkdays: g.Where(x => !x.IsSunday).Sum(x => x.WorkdayValue),
                SundayWorkdays: g.Where(x => x.IsSunday).Sum(x => x.WorkdayValue),
                TotalWorkdays: g.Sum(x => x.WorkdayValue),
                TotalOvertimeHours: g.Sum(x => x.OvertimeHours)))
            .OrderBy(r => r.EmployeeCode)
            .ToList();

        var dto = new WorkdayOvertimeReportDto(
            periodMonth, byEmployee,
            byEmployee.Sum(r => r.RegularWorkdays), byEmployee.Sum(r => r.SundayWorkdays),
            byEmployee.Sum(r => r.TotalWorkdays), byEmployee.Sum(r => r.TotalOvertimeHours));

        return Result<WorkdayOvertimeReportDto>.Success(dto, byEmployee.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<MealReportDto>> GetMealReportAsync(string periodMonth, CancellationToken ct)
    {
        var validation = ValidateMonth<MealReportDto>(periodMonth);
        if (validation is not null) return validation;

        var (start, end) = ComputePeriodDateRange(periodMonth);
        var (daysWithData, totalMeals, totalAmount) = await _repo.GetMealTotalsAsync(start, end, ct);

        decimal? monthlyCostMealsAmount = null;
        var period = await _repo.FindPeriodByMonthAsync(periodMonth, ct);
        if (period is not null)
            monthlyCostMealsAmount = await _repo.GetMonthlyCostMealsAmountAsync(period.PeriodId, ct);

        var hasDiscrepancy = monthlyCostMealsAmount.HasValue && monthlyCostMealsAmount.Value != totalAmount;

        var dto = new MealReportDto(periodMonth, daysWithData, totalMeals, totalAmount,
            monthlyCostMealsAmount, hasDiscrepancy);
        return Result<MealReportDto>.Success(dto, daysWithData == 0 ? Messages.NoData : null);
    }

    public async Task<Result<VehicleReportDto>> GetVehicleReportAsync(string periodMonth, CancellationToken ct)
    {
        var validation = ValidateMonth<VehicleReportDto>(periodMonth);
        if (validation is not null) return validation;

        var (start, end) = ComputePeriodDateRange(periodMonth);
        var trips = await _repo.GetVehicleTripsAsync(start, end, ct);

        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();
        var vehicles = await _repo.GetVehiclesAsync(vehicleIds, ct);

        var rows = trips
            .GroupBy(t => t.VehicleId)
            .Select(g =>
            {
                var (plateNumber, vehicleType) = vehicles.GetValueOrDefault(g.Key, ("", ""));
                var completed = g.Where(t => t.Status == VehicleTripStatus.Completed).ToList();
                return new VehicleTripReportRowDto(
                    g.Key, plateNumber, vehicleType,
                    CompletedTripCount: completed.Count,
                    TotalKm: completed.Sum(t => (t.OdometerEnd ?? t.OdometerStart) - t.OdometerStart),
                    TotalFuelLiters: completed.Sum(t => t.FuelLiters ?? 0m),
                    TotalFuelCost: completed.Sum(t => t.FuelCost ?? 0m),
                    KmGapWarningCount: g.Count(t => t.KmGapWarning),
                    FuelOverNormWarningCount: g.Count(t => t.FuelOverNormWarning));
            })
            .OrderBy(r => r.PlateNumber)
            .ToList();

        var dto = new VehicleReportDto(
            periodMonth, rows,
            rows.Sum(r => r.TotalKm), rows.Sum(r => r.TotalFuelLiters), rows.Sum(r => r.TotalFuelCost),
            rows.Sum(r => r.KmGapWarningCount), rows.Sum(r => r.FuelOverNormWarningCount));

        return Result<VehicleReportDto>.Success(dto, rows.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct)
    {
        var workdays = await GetWorkdayOvertimeReportAsync(periodMonth, ct);
        if (!workdays.IsSuccess)
            return Result<ExcelFileDto>.Failure(workdays.ErrorCode!, workdays.Message!, workdays.Kind);

        var meals = await GetMealReportAsync(periodMonth, ct);
        if (!meals.IsSuccess)
            return Result<ExcelFileDto>.Failure(meals.ErrorCode!, meals.Message!, meals.Kind);

        var vehicles = await GetVehicleReportAsync(periodMonth, ct);
        if (!vehicles.IsSuccess)
            return Result<ExcelFileDto>.Failure(vehicles.ErrorCode!, vehicles.Message!, vehicles.Kind);

        var content = _excelWriter.WriteReportWorkbook(workdays.Data!, meals.Data!, vehicles.Data!);
        _logger.LogInformation("Đã xuất file Excel báo cáo kế toán tháng {PeriodMonth}", periodMonth);
        return Result<ExcelFileDto>.Success(
            new ExcelFileDto(content, $"BaoCaoKeToan_{periodMonth}.xlsx"), Messages.ExcelExported);
    }

    private static Result<T>? ValidateMonth<T>(string periodMonth) =>
        PeriodMonthPattern.IsMatch(periodMonth)
            ? null
            : Result<T>.Failure(ErrorCodes.AccountingReportPeriodMonthInvalid,
                Messages.AccountingReportPeriodMonthInvalid, ResultKind.Validation);

    private static (DateOnly Start, DateOnly End) ComputePeriodDateRange(string periodMonth)
    {
        var year = int.Parse(periodMonth[..4]);
        var month = int.Parse(periodMonth[5..7]);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return (start, end);
    }
}
