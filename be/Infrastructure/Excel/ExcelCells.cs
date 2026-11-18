using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.Common;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>Đọc giá trị ô Excel an toàn cho các importer: không ném lỗi khi ô hỏng, chữ đã cắt khoảng trắng.</summary>
internal static class ExcelCells
{
    /// <summary>Chữ trong ô (đã Trim), ô trống hoặc không đọc được thì trả chuỗi rỗng.</summary>
    public static string Text(IXLCell cell)
    {
        try
        {
            var raw = cell.Value;
            return raw.IsBlank ? string.Empty : raw.ToString().Trim();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return string.Empty;
        }
    }

    /// <summary>Chữ trong ô, gộp nhiều khoảng trắng liên tiếp thành một.</summary>
    public static string CompactText(IXLCell cell) =>
        string.Join(' ', Text(cell).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Tên cột đã chuẩn hóa (bỏ dấu, chữ thường, chỉ chữ và số), vd "Dài (m)" → "daim".</summary>
    public static string Key(IXLCell cell) => PayslipLayout.NormalizeKey(Text(cell));

    /// <summary>
    /// Đọc số: ô trống hoặc "-" là 0 và hợp lệ. Trả false kèm mô tả lỗi (dạng "không phải số ('abc')") khi ô có chữ hoặc lỗi công thức.
    /// </summary>
    public static bool TryReadNumber(IXLCell cell, out decimal value, out string error)
    {
        value = 0m;
        error = string.Empty;
        try
        {
            var raw = cell.Value;
            if (raw.IsBlank) return true;
            if (raw.IsNumber)
            {
                value = (decimal)raw.GetNumber();
                return true;
            }
            if (raw.IsError)
            {
                error = $"bị lỗi công thức ({raw})";
                return false;
            }

            var text = raw.ToString().Trim();
            if (text.Length == 0 || text == "-") return true;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return true;

            error = $"không phải số ('{text}')";
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = "không đọc được giá trị";
            return false;
        }
    }

    /// <summary>Tách ô nhiều giá trị theo dấu ";" (bỏ phần rỗng, cắt khoảng trắng, gộp khoảng trắng giữa chữ).</summary>
    public static IReadOnlyList<string> SplitList(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(s => s.Length > 0)
            .ToList();
}
