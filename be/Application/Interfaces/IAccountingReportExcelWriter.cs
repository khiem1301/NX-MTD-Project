using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC13 (xuất Excel) cho màn "Báo cáo kế toán" (UC36, MI-19) — MSG27. Một workbook, 3 sheet
/// (công/tăng ca, suất ăn, sổ xe), cùng kiểu một-writer-một-màn-hình đã dùng cho các báo cáo trước.</summary>
public interface IAccountingReportExcelWriter
{
    byte[] WriteReportWorkbook(
        WorkdayOvertimeReportDto workdays, MealReportDto meals, VehicleReportDto vehicles);
}
