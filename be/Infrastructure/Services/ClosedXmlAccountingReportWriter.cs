using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>Dựng file Excel "Báo cáo kế toán" (UC36, MI-19) bằng ClosedXML — một workbook, 3 sheet (công +
/// tăng ca, suất ăn, sổ xe), cùng kiểu ClosedXmlFinanceWriter/ClosedXmlMonthlyCostWriter (MSG27).</summary>
public sealed class ClosedXmlAccountingReportWriter : IAccountingReportExcelWriter
{
    private static readonly string[] WorkdayHeaders =
        { "Mã NV", "Họ tên", "Tổ", "Công ngày thường", "Công Chủ nhật", "Tổng công", "Giờ tăng ca" };

    private static readonly string[] VehicleHeaders =
    {
        "Biển số", "Loại xe", "SL chuyến hoàn thành", "Tổng km", "Tổng xăng (lít)", "Tổng tiền xăng",
        "SL chuyến cảnh báo km", "SL chuyến vượt định mức xăng",
    };

    public byte[] WriteReportWorkbook(
        WorkdayOvertimeReportDto workdays, MealReportDto meals, VehicleReportDto vehicles)
    {
        using var workbook = new XLWorkbook();

        WriteWorkdaySheet(workbook, workdays);
        WriteMealSheet(workbook, meals);
        WriteVehicleSheet(workbook, vehicles);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteWorkdaySheet(XLWorkbook workbook, WorkdayOvertimeReportDto dto)
    {
        var sheet = workbook.Worksheets.Add("CongTangCa");
        sheet.Cell(1, 1).Value = $"Công + tăng ca tháng {dto.PeriodMonth}";
        sheet.Row(1).Style.Font.Bold = true;
        WriteHeader(sheet, 2, WorkdayHeaders);

        var r = 3;
        foreach (var row in dto.Rows)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = row.EmployeeCode;
            sheet.Cell(r, c++).Value = row.FullName;
            sheet.Cell(r, c++).Value = row.TeamName ?? "";
            sheet.Cell(r, c++).Value = row.RegularWorkdays;
            sheet.Cell(r, c++).Value = row.SundayWorkdays;
            sheet.Cell(r, c++).Value = row.TotalWorkdays;
            sheet.Cell(r, c).Value = row.TotalOvertimeHours;
            r++;
        }
        sheet.Cell(r, 1).Value = "Tổng cộng";
        sheet.Cell(r, 4).Value = dto.TotalRegularWorkdays;
        sheet.Cell(r, 5).Value = dto.TotalSundayWorkdays;
        sheet.Cell(r, 6).Value = dto.TotalWorkdays;
        sheet.Cell(r, 7).Value = dto.TotalOvertimeHours;
        sheet.Row(r).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();
    }

    private static void WriteMealSheet(XLWorkbook workbook, MealReportDto dto)
    {
        var sheet = workbook.Worksheets.Add("SuatAn");
        sheet.Cell(1, 1).Value = $"Suất ăn tháng {dto.PeriodMonth}";
        sheet.Row(1).Style.Font.Bold = true;

        var r = 3;
        sheet.Cell(r, 1).Value = "Số ngày có dữ liệu";
        sheet.Cell(r, 2).Value = dto.DaysWithData;
        r++;
        sheet.Cell(r, 1).Value = "Tổng suất ăn (hệ thống)";
        sheet.Cell(r, 2).Value = dto.TotalMealsServed;
        r++;
        sheet.Cell(r, 1).Value = "Tổng tiền suất ăn (hệ thống)";
        sheet.Cell(r, 2).Value = dto.TotalMealAmountSystem;
        r++;
        sheet.Cell(r, 1).Value = "Hóa đơn bếp đã nhập (chi phí tháng, MEALS)";
        if (dto.MonthlyCostMealsAmount.HasValue) sheet.Cell(r, 2).Value = dto.MonthlyCostMealsAmount.Value;
        else sheet.Cell(r, 2).Value = "Chưa nhập";
        r++;
        sheet.Cell(r, 1).Value = "Có chênh lệch";
        sheet.Cell(r, 2).Value = dto.HasDiscrepancy ? "Có" : "Không";

        sheet.Columns().AdjustToContents();
    }

    private static void WriteVehicleSheet(XLWorkbook workbook, VehicleReportDto dto)
    {
        var sheet = workbook.Worksheets.Add("SoXe");
        sheet.Cell(1, 1).Value = $"Sổ xe tháng {dto.PeriodMonth}";
        sheet.Row(1).Style.Font.Bold = true;
        WriteHeader(sheet, 2, VehicleHeaders);

        var r = 3;
        foreach (var row in dto.Vehicles)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = row.PlateNumber;
            sheet.Cell(r, c++).Value = row.VehicleType;
            sheet.Cell(r, c++).Value = row.CompletedTripCount;
            sheet.Cell(r, c++).Value = row.TotalKm;
            sheet.Cell(r, c++).Value = row.TotalFuelLiters;
            sheet.Cell(r, c++).Value = row.TotalFuelCost;
            sheet.Cell(r, c++).Value = row.KmGapWarningCount;
            sheet.Cell(r, c).Value = row.FuelOverNormWarningCount;
            r++;
        }
        sheet.Cell(r, 1).Value = "Tổng cộng";
        sheet.Cell(r, 4).Value = dto.TotalKm;
        sheet.Cell(r, 5).Value = dto.TotalFuelLiters;
        sheet.Cell(r, 6).Value = dto.TotalFuelCost;
        sheet.Cell(r, 7).Value = dto.TotalKmGapWarnings;
        sheet.Cell(r, 8).Value = dto.TotalFuelOverNormWarnings;
        sheet.Row(r).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();
    }

    private static void WriteHeader(IXLWorksheet sheet, int rowIndex, IReadOnlyList<string> headers)
    {
        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(rowIndex, col + 1).Value = headers[col];
        sheet.Row(rowIndex).Style.Font.Bold = true;
    }
}
