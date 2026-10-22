using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-19 — UC36 "Báo cáo kế toán": đọc 3 nguồn dữ liệu khác bảng của Minh/người khác chỉ để đọc —
/// DailyTimesheet (bảng công, Minh), DailyMeal (suất ăn, Minh), MonthlyCost (chi phí tháng, Minh, chỉ đọc
/// khoản MEALS để đối soát) và Vehicle/VehicleTrip (sổ xe, Thảo — read-only, chưa có BE/FE nào khác đụng
/// tới). Dedicated repository thay vì IUnitOfWork, cùng kiểu FinanceRepository/MonthlyCostRepository: nhiều
/// truy vấn cross-table chỉ phục vụ báo cáo, không có nghiệp vụ ghi.</summary>
public interface IAccountingReportRepository
{
    Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct);

    // ---------- 1. Công + tăng ca ----------

    /// <summary>Từng dòng DailyTimesheet trong khoảng ngày của kỳ, kèm tên nhân viên/tổ — Service tự gộp
    /// theo nhân viên (regular vs Sunday theo IsSunday).</summary>
    Task<IReadOnlyList<(int EmployeeId, string EmployeeCode, string FullName, string? TeamName,
            decimal WorkdayValue, decimal OvertimeHours, bool IsSunday)>>
        GetTimesheetRowsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    // ---------- 2. Suất ăn + đối soát bếp ----------

    /// <summary>Số ngày có dữ liệu suất ăn, tổng TotalMeals, tổng TotalAmount trong khoảng ngày.</summary>
    Task<(int DaysWithData, int TotalMeals, decimal TotalAmount)> GetMealTotalsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    /// <summary>Số tiền dòng MEALS trong MonthlyCost của kỳ (nếu Kế toán đã nhập chi phí tháng) — đối soát
    /// từ phía báo cáo suất ăn, cùng khái niệm MonthlyCostService.BuildMealsReconciliationAsync (MI-12)
    /// nhưng đọc trực tiếp một dòng, không cần toàn bộ danh sách khoản chi phí.</summary>
    Task<decimal?> GetMonthlyCostMealsAmountAsync(int periodId, CancellationToken ct);

    // ---------- 3. Sổ xe + xăng (Vehicle/VehicleTrip, Thảo — chỉ đọc) ----------

    /// <summary>Mọi chuyến xe (mọi Status) có TripDate trong khoảng ngày của kỳ, kèm thông tin xe — Service
    /// tự gộp theo xe (km/xăng chỉ tính chuyến COMPLETED, cảnh báo đếm trên mọi chuyến trong kỳ).</summary>
    Task<IReadOnlyList<VehicleTrip>> GetVehicleTripsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);

    Task<IReadOnlyDictionary<int, (string PlateNumber, string VehicleType)>> GetVehiclesAsync(
        IReadOnlyCollection<int> vehicleIds, CancellationToken ct);
}
