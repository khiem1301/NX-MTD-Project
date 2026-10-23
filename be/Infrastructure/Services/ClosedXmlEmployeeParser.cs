using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Đọc file Excel nhân viên (KH-22). Dòng đầu là tiêu đề, 10 cột theo thứ tự:
/// Mã NV | Họ tên | Giới tính | Ngày sinh | SĐT | Vai trò | Mã tổ | Hình thức lương | Ngày vào làm | Mã chấm công.
/// Lỗi từng ô được gắn vào dòng (ParseError) để màn xem trước báo đủ; chỉ sai cấu trúc cả file mới ném lỗi.
/// </summary>
public sealed class ClosedXmlEmployeeParser : IExcelEmployeeParser
{
    private const int ColumnCount = 10;
    private static readonly string[] Headers =
        { "Mã NV", "Họ tên", "Giới tính", "Ngày sinh", "SĐT", "Vai trò", "Mã tổ", "Hình thức lương", "Ngày vào làm", "Mã chấm công" };

    public IReadOnlyList<EmployeeExcelRowDto> Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        using var workbook = XlsxWorkbookLoader.Open(buffer);
        var sheet = workbook.Worksheets.Count == 0 ? throw new InvalidDataException("File Excel không có sheet nào.") : workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        var rows = new List<EmployeeExcelRowDto>();
        if (used is null) return rows;
        if (!HasValidHeader(used.FirstRow()))
            throw new InvalidDataException($"File Excel sai cột: cần '{string.Join(", ", Headers)}'.");

        foreach (var row in used.RowsUsed().Skip(1))
        {
            if (Enumerable.Range(1, ColumnCount).All(i => row.Cell(i).IsEmpty())) continue;
            string? error = null;
            var dob = Date(row.Cell(4), "Ngày sinh", ref error);
            var start = Date(row.Cell(9), "Ngày vào làm", ref error);
            int? attendance = null;
            var attText = Text(row.Cell(10));
            if (attText is not null)
            {
                if (int.TryParse(attText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)) attendance = a;
                else error ??= "Mã chấm công phải là số.";
            }
            if (string.IsNullOrWhiteSpace(Text(row.Cell(1)))) error ??= "Thiếu mã nhân viên.";
            rows.Add(new EmployeeExcelRowDto(row.RowNumber(), Text(row.Cell(1)), Text(row.Cell(2)), Text(row.Cell(3)), dob, Text(row.Cell(5)),
                Text(row.Cell(6)), Text(row.Cell(7)), Text(row.Cell(8)), start, attendance, error));
        }
        return rows;
    }

    private static bool HasValidHeader(IXLRangeRow header) =>
        Headers.Select((h, i) => string.Equals(Text(header.Cell(i + 1)), h, StringComparison.OrdinalIgnoreCase)).All(ok => ok);

    private static string? Text(IXLCell cell)
    {
        var s = cell.GetFormattedString().Trim();
        return s.Length == 0 ? null : s;
    }

    private static DateOnly? Date(IXLCell cell, string column, ref string? error)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
        var text = Text(cell);
        if (text is not null && DateOnly.TryParseExact(text, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        error ??= $"{column} không đúng định dạng dd/MM/yyyy.";
        return null;
    }
}
