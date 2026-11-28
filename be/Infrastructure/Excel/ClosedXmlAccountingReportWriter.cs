using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>Dựng file Excel "Báo cáo kế toán" (UC36, MI-19) bằng ClosedXML — một workbook, 3 sheet (công +
/// tăng ca, suất ăn, sổ xe), cùng kiểu ClosedXmlFinanceWriter/ClosedXmlMonthlyCostWriter (MSG27).</summary>
public sealed class ClosedXmlAccountingReportWriter : IAccountingReportExcelWriter
{
    private static readonly string[] WorkdayHeaders =
    {
        "Mã NV", "Họ tên", "Bậc thợ", "Công ngày thường", "Công Chủ nhật", "Tổng công", "Số lượng lỗi",
        "Công đoạn", "Sản lượng", "Đơn giá", "Thành tiền", "Tổng tiền",
    };

    private const string WholeMoneyFormat = "#,##0";
    private const string CentMoneyFormat = "#,##0.00";
    private const int EmployeeColumns = 7;
    private const int TotalAmountColumn = 12;
    private const string SheetPrefix = "Cong ";
    private const string NoTeamName = "KhongTo";
    private const int MaxSheetNameLength = 31;
    private const string InvalidSheetChars = ":\\/?*[]";

    private static readonly string[] VehicleHeaders =
    {
        "Biển số", "Loại xe", "SL chuyến hoàn thành", "Tổng km", "Tổng xăng (lít)", "Tổng tiền xăng",
        "SL chuyến cảnh báo km", "SL chuyến vượt định mức xăng",
    };

    public byte[] WriteReportWorkbook(
        WorkdayOvertimeReportDto workdays, MealReportDto meals, VehicleReportDto vehicles)
    {
        using var workbook = new XLWorkbook();

        WriteWorkdaySheets(workbook, workdays);
        WriteMealSheet(workbook, meals);
        WriteVehicleSheet(workbook, vehicles);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Mỗi tổ một sheet (tên "Cong {mã tổ}"); nhân viên chưa có tổ vào sheet "Cong KhongTo"; không có dòng nào thì một sheet trống.</summary>
    private static void WriteWorkdaySheets(XLWorkbook workbook, WorkdayOvertimeReportDto dto)
    {
        var teams = dto.Rows.GroupBy(r => r.TeamCode ?? string.Empty)
            .OrderBy(g => g.Key.Length == 0).ThenBy(g => g.Key).ToList();
        if (teams.Count == 0)
        {
            WriteTeamSheet(workbook.Worksheets.Add(SheetPrefix + NoTeamName), dto.PeriodMonth, string.Empty, []);
            return;
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in teams)
            WriteTeamSheet(workbook.Worksheets.Add(UniqueSheetName(team.Key, used)), dto.PeriodMonth, team.Key, team.ToList());
    }

    private static string UniqueSheetName(string teamCode, HashSet<string> used)
    {
        var clean = new string((teamCode.Length == 0 ? NoTeamName : teamCode).Where(c => !InvalidSheetChars.Contains(c)).ToArray());
        var name = (SheetPrefix + clean)[..Math.Min(MaxSheetNameLength, SheetPrefix.Length + clean.Length)];
        for (var n = 2; !used.Add(name); n++)
        {
            var suffix = $" ({n})";
            name = name[..Math.Min(name.Length, MaxSheetNameLength - suffix.Length)] + suffix;
        }
        return name;
    }

    private static void WriteTeamSheet(IXLWorksheet sheet, string periodMonth, string teamCode, IReadOnlyList<WorkdayOvertimeRowDto> rows)
    {
        sheet.Cell(1, 1).Value = $"Công, sản lượng theo công đoạn, lỗi tháng {periodMonth} - Tổ {(teamCode.Length == 0 ? "chưa có tổ" : teamCode)}";
        sheet.Row(1).Style.Font.Bold = true;
        WriteHeader(sheet, 2, WorkdayHeaders);

        var r = 3;
        foreach (var row in rows) r = WriteEmployeeBlock(sheet, r, row);
        WriteTotalRow(sheet, r, rows);
        DrawGrid(sheet.Range(2, 1, r, WorkdayHeaders.Length));

        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// Một nhân viên: mỗi công đoạn (và mức giá) một dòng; thông tin của nhân viên (mã, tên, bậc, công, lỗi, tổng tiền) gộp ô theo chiều dọc
    /// qua các dòng đó để cộng cột không bị tính lặp. Trả về dòng kế tiếp.
    /// </summary>
    private static int WriteEmployeeBlock(IXLWorksheet sheet, int first, WorkdayOvertimeRowDto row)
    {
        var count = Math.Max(1, row.Operations.Count);
        var last = first + count - 1;
        for (var i = 0; i < row.Operations.Count; i++)
        {
            var op = row.Operations[i];
            var r = first + i;
            sheet.Cell(r, 8).Value = $"{op.OperationCode} - {op.OperationName}";
            sheet.Cell(r, 9).Value = op.OutputQty;
            SetMoney(sheet.Cell(r, 10), op.UnitRate);
            SetMoney(sheet.Cell(r, 11), op.Amount);
        }

        sheet.Cell(first, 1).Value = row.EmployeeCode;
        sheet.Cell(first, 2).Value = row.FullName;
        sheet.Cell(first, 3).Value = row.GradeCode ?? "";
        sheet.Cell(first, 4).Value = row.RegularWorkdays;
        sheet.Cell(first, 5).Value = row.SundayWorkdays;
        sheet.Cell(first, 6).Value = row.TotalWorkdays;
        sheet.Cell(first, 7).Value = row.DefectQty;
        SetMoney(sheet.Cell(first, TotalAmountColumn), row.OutputAmount);
        foreach (var column in Enumerable.Range(1, EmployeeColumns).Append(TotalAmountColumn))
        {
            var cells = sheet.Range(first, column, last, column);
            if (count > 1) cells.Merge();
            cells.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        return last + 1;
    }

    private static void WriteTotalRow(IXLWorksheet sheet, int r, IReadOnlyList<WorkdayOvertimeRowDto> rows)
    {
        sheet.Cell(r, 1).Value = "Tổng cộng";
        sheet.Cell(r, 4).Value = rows.Sum(x => x.RegularWorkdays);
        sheet.Cell(r, 5).Value = rows.Sum(x => x.SundayWorkdays);
        sheet.Cell(r, 6).Value = rows.Sum(x => x.TotalWorkdays);
        sheet.Cell(r, 7).Value = rows.Sum(x => x.DefectQty);
        sheet.Cell(r, 9).Value = rows.Sum(x => x.OutputQty);
        var total = rows.Sum(x => x.OutputAmount);
        SetMoney(sheet.Cell(r, 11), total);
        SetMoney(sheet.Cell(r, TotalAmountColumn), total);
        sheet.Row(r).Style.Font.Bold = true;
    }

    /// <summary>Ghi số tiền: số nguyên hiển thị không có phần lẻ, có lẻ thì hai chữ số lẻ (tránh dấu phẩy thừa sau số).</summary>
    private static void SetMoney(IXLCell cell, decimal amount)
    {
        cell.Value = amount;
        cell.Style.NumberFormat.Format = amount == decimal.Truncate(amount) ? WholeMoneyFormat : CentMoneyFormat;
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
        DrawGrid(sheet.Range(3, 1, r, 2));

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
        DrawGrid(sheet.Range(2, 1, r, VehicleHeaders.Length));

        sheet.Columns().AdjustToContents();
    }

    /// <summary>Viền đen mảnh quanh bảng và giữa các ô.</summary>
    private static void DrawGrid(IXLRange table)
    {
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorderColor = XLColor.Black;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorderColor = XLColor.Black;
    }

    private static void WriteHeader(IXLWorksheet sheet, int rowIndex, IReadOnlyList<string> headers)
    {
        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(rowIndex, col + 1).Value = headers[col];
        sheet.Row(rowIndex).Style.Font.Bold = true;
    }
}
