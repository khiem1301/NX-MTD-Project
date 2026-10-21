using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Dựng file Excel "dữ liệu tính lương" (UC31) bằng ClosedXML: mỗi dòng một nhân viên, các cột lấy từ
/// vw_PayrollExportData + 2 cột cảnh báo (ngày thiếu giờ, ngày chưa nhập sản lượng — DEV-MI-09-1).
/// Kế toán tải file này làm dữ liệu đầu vào, tính lương trên bảng thanh toán của xưởng ngoài hệ thống;
/// file import là bảng thanh toán đó (xem ClosedXmlPayrollParser, DEV-MI-09-6), không phải file xuất này.
/// </summary>
public sealed class ClosedXmlPayrollWriter : IPayrollExcelWriter
{
    private static readonly string[] Headers =
    {
        "Mã nhân viên", "Họ tên", "Vai trò", "Tổ", "Kiểu lương",
        "Công", "Công Chủ nhật", "Giờ tăng ca", "Giờ tăng ca Chủ nhật",
        "Tiền sản phẩm", "Tiền sản phẩm Chủ nhật x2", "Trừ hàng hỏng (Scrap)",
        "Bậc thợ", "Tiền bậc", "Số ngày thiếu giờ", "Số ngày chưa nhập sản lượng",
    };

    public byte[] WriteExportSheet(string periodMonth, IReadOnlyList<PayrollExportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add($"DuLieuTinhLuong_{periodMonth}");

        for (var col = 0; col < Headers.Length; col++)
            sheet.Cell(1, col + 1).Value = Headers[col];
        sheet.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var row in rows)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = row.EmployeeCode;
            sheet.Cell(r, c++).Value = row.FullName;
            sheet.Cell(r, c++).Value = row.RoleName;
            sheet.Cell(r, c++).Value = row.TeamName ?? "";
            sheet.Cell(r, c++).Value = row.PayType;
            sheet.Cell(r, c++).Value = row.WorkDays;
            sheet.Cell(r, c++).Value = row.SundayWorkDays;
            sheet.Cell(r, c++).Value = row.OvertimeHours;
            sheet.Cell(r, c++).Value = row.SundayOvertimeHours;
            sheet.Cell(r, c++).Value = row.PieceRateAmount;
            sheet.Cell(r, c++).Value = row.SundayPieceRateBonus;
            sheet.Cell(r, c++).Value = row.ScrapDeduction;
            sheet.Cell(r, c++).Value = row.GradeCode ?? "";
            sheet.Cell(r, c++).Value = row.GradeAllowance ?? 0m;
            sheet.Cell(r, c++).Value = row.MissingPunchDays;
            sheet.Cell(r, c).Value = row.DaysWithoutOutput;
            r++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
