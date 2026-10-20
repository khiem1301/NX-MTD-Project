using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Đọc file Excel chấm công dự phòng bằng ClosedXML (đọc trong bộ nhớ, không ghi file).
/// Định dạng: dòng đầu là tiêu đề, 4 cột theo thứ tự
///   Mã chấm công | Ngày | Giờ vào | Giờ ra
/// Mã chấm công là số nguyên; ngày ở dạng Date; giờ vào/ra ở dạng Time (có thể để trống).
/// </summary>
public sealed class ClosedXmlAttendanceParser : IExcelAttendanceParser
{
    private static readonly string[] Headers = { "Mã chấm công", "Ngày", "Giờ vào", "Giờ ra" };
    private static readonly string[] HeadersEn = { "AttendanceCode", "WorkDate", "CheckIn", "CheckOut" };

    public IReadOnlyList<AttendanceExcelRowDto> Parse(Stream stream)
    {
        var rows = new List<AttendanceExcelRowDto>();

        // IFormFile.OpenReadStream() không seek được; ClosedXML cần stream seek để đọc package.
        // Attendance Excel nhỏ nên copy vào bộ nhớ (đúng ý "xem trước trong bộ nhớ") và không đụng stream gọi vào.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        using var workbook = new XLWorkbook(buffer);
        var sheet = workbook.Worksheets.Count == 0
            ? throw new InvalidDataException("File Excel không có sheet nào.")
            : workbook.Worksheets.First();

        var used = sheet.RangeUsed();
        if (used is null) return rows;

        var headerRow = used.FirstRow();
        if (headerRow is null || !HasValidHeader(headerRow))
            throw new InvalidDataException("File Excel sai cột: cần 'Mã chấm công, Ngày, Giờ vào, Giờ ra'.");

        foreach (var row in used.RowsUsed().Skip(1))
        {
            var codeCell = row.Cell(1);
            var dateCell = row.Cell(2);
            var inCell = row.Cell(3);
            var outCell = row.Cell(4);

            // Bỏ dòng trống hoàn toàn (file có thể thừa dòng cuối).
            if (codeCell.IsEmpty() && dateCell.IsEmpty() && inCell.IsEmpty() && outCell.IsEmpty())
                continue;

            if (!TryParseCode(codeCell, out var code))
                throw new InvalidDataException($"Dòng {row.RowNumber()}: mã chấm công không hợp lệ.");

            if (!TryParseDate(dateCell, out var workDate))
                throw new InvalidDataException($"Dòng {row.RowNumber()}: ngày không hợp lệ.");

            var checkIn = TryParseTime(inCell, out var tin) ? tin : (TimeOnly?)null;
            var checkOut = TryParseTime(outCell, out var tout) ? tout : (TimeOnly?)null;

            if (checkIn is null && checkOut is null)
                throw new InvalidDataException($"Dòng {row.RowNumber()}: thiếu cả giờ vào và giờ ra.");

            rows.Add(new AttendanceExcelRowDto(code, workDate, checkIn, checkOut));
        }

        return rows;
    }

    private static bool HasValidHeader(IXLRangeRow headerRow)
    {
        var cells = headerRow.Cells(1, 4).Select(c => Normalize(c.GetString())).ToArray();
        return cells.SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase)
            || cells.SequenceEqual(HeadersEn, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    private static bool TryParseCode(IXLCell cell, out int code)
    {
        code = 0;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.Number)
        {
            if (!cell.TryGetValue(out double d) || d < int.MinValue || d > int.MaxValue || d != Math.Floor(d))
                return false;
            code = (int)d;
            return true;
        }
        return int.TryParse(cell.GetString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out code);
    }

    private static bool TryParseDate(IXLCell cell, out DateOnly workDate)
    {
        workDate = default;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.DateTime)
        {
            if (!cell.TryGetValue(out DateTime dt)) return false;
            workDate = DateOnly.FromDateTime(dt);
            return true;
        }

        var text = cell.GetString().Trim();
        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out workDate);
    }

    private static bool TryParseTime(IXLCell cell, out TimeOnly time)
    {
        time = default;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.TimeSpan)
        {
            if (!cell.TryGetValue(out TimeSpan ts)) return false;
            time = TimeOnly.FromTimeSpan(ts);
            return true;
        }
        if (cell.DataType == XLDataType.DateTime)
        {
            if (!cell.TryGetValue(out DateTime dt)) return false;
            time = TimeOnly.FromDateTime(dt);
            return true;
        }

        var text = cell.GetString().Trim();
        return TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
