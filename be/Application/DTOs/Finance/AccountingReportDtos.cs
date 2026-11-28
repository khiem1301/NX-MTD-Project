namespace NX_MTD.Application.DTOs;

// ---------- MI-19 — UC36 "Báo cáo kế toán" (CHỈ Kế toán). Dữ liệu vận hành (giờ công, suất ăn, sổ xe) —
// KHÔNG chứa đơn giá CMT/chi phí/lương (GBR-02, xem thêm CostSummaryDto cho màn "mật" của Admin). ----------

// ----- 1. Công + tăng ca (DailyTimesheet) -----

/// <summary>Sản lượng một công đoạn của một nhân viên trong tháng ở một mức đơn giá: mỗi lần báo sản lượng đã gắn mức giá có hiệu lực lúc
/// báo (OutputRecord.PieceRateId) nên giá đổi giữa tháng thì công đoạn đó có nhiều dòng, mỗi dòng một mức giá.</summary>
public sealed record WorkerOperationOutputDto(
    string OperationCode, string OperationName, decimal UnitRate, int OutputQty, decimal Amount);

/// <summary>Công, bậc thợ, số lượng lỗi và sản lượng theo công đoạn (số lượng × đơn giá) của một nhân viên trong tháng, tách công Chủ nhật
/// (IsSunday, SUNDAY_PAY_MULTIPLIER) khỏi công ngày thường (DEV-ALL-199, 200). Số lượng lỗi = Σ số lượng các phiếu hàng hỏng chưa hủy mà
/// nhân viên chịu trách nhiệm.</summary>
public sealed record WorkdayOvertimeRowDto(
    int EmployeeId, string EmployeeCode, string FullName, string? TeamCode, string? GradeCode,
    decimal RegularWorkdays, decimal SundayWorkdays, decimal TotalWorkdays, int DefectQty,
    IReadOnlyList<WorkerOperationOutputDto> Operations)
{
    public int OutputQty => Operations.Sum(o => o.OutputQty);
    public decimal OutputAmount => Operations.Sum(o => o.Amount);
}

/// <summary>Báo cáo công, bậc thợ, sản lượng, lỗi toàn xưởng trong một tháng — UC36. File Excel chia mỗi tổ một sheet.</summary>
public sealed record WorkdayOvertimeReportDto(
    string PeriodMonth, IReadOnlyList<WorkdayOvertimeRowDto> Rows,
    decimal TotalRegularWorkdays, decimal TotalSundayWorkdays, decimal TotalWorkdays,
    int TotalOutputQty, decimal TotalOutputAmount, int TotalDefectQty);

// ----- 2. Suất ăn + đối soát bếp (DailyMeal) -----

/// <summary>Suất ăn phục vụ trong tháng theo hệ thống (Σ DailyMeal.TotalMeals/TotalAmount) so với hóa đơn
/// bếp đã nhập ở chi phí tháng (MonthlyCost, khoản MEALS) nếu đã nhập — cùng khái niệm đối soát MEALS đã có
/// ở MonthlyCostService (MI-12), nhìn từ phía suất ăn thay vì phía chi phí.</summary>
public sealed record MealReportDto(
    string PeriodMonth, int DaysWithData, int TotalMealsServed, decimal TotalMealAmountSystem,
    decimal? MonthlyCostMealsAmount, bool HasDiscrepancy);

// ----- 3. Sổ xe + xăng (Vehicle/VehicleTrip, Thảo — chỉ đọc) -----

/// <summary>Tổng hợp một xe trong tháng: SL chuyến hoàn thành, tổng km, tổng xăng, số chuyến bị cảnh báo
/// (MSG30 — km không liền chuyến trước hoặc vượt định mức xăng).</summary>
public sealed record VehicleTripReportRowDto(
    int VehicleId, string PlateNumber, string VehicleType,
    int CompletedTripCount, int TotalKm, decimal TotalFuelLiters, decimal TotalFuelCost,
    int KmGapWarningCount, int FuelOverNormWarningCount);

/// <summary>Báo cáo km + xăng toàn bộ xe trong một tháng — UC36.</summary>
public sealed record VehicleReportDto(
    string PeriodMonth, IReadOnlyList<VehicleTripReportRowDto> Vehicles,
    int TotalKm, decimal TotalFuelLiters, decimal TotalFuelCost,
    int TotalKmGapWarnings, int TotalFuelOverNormWarnings);
