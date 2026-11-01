using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>MI-19 — UC36 "Báo cáo kế toán", CHỈ Kế toán. Ba báo cáo vận hành theo tháng, không chứa chi
/// phí/lương (GBR-02 — xem <see cref="ICostSummaryService"/> cho màn "mật" riêng của Admin).</summary>
public interface IAccountingReportService
{
    /// <summary>Công + tăng ca theo nhân viên trong tháng (DailyTimesheet).</summary>
    Task<Result<WorkdayOvertimeReportDto>> GetWorkdayOvertimeReportAsync(string periodMonth, CancellationToken ct);

    /// <summary>Suất ăn phục vụ trong tháng + đối soát với hóa đơn bếp đã nhập ở chi phí tháng (nếu có).</summary>
    Task<Result<MealReportDto>> GetMealReportAsync(string periodMonth, CancellationToken ct);

    /// <summary>Km + xăng theo xe trong tháng (Vehicle/VehicleTrip, đọc, không ghi).</summary>
    Task<Result<VehicleReportDto>> GetVehicleReportAsync(string periodMonth, CancellationToken ct);

    /// <summary>Xuất cả 3 báo cáo ra một file Excel 3 sheet (MSG27).</summary>
    Task<Result<ExcelFileDto>> ExportAsync(string periodMonth, CancellationToken ct);
}
