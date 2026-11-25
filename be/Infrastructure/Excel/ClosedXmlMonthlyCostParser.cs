using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// Đọc file Excel chi phí tháng bằng ClosedXML (DEV-MI-12-3). Cột được nhận theo TÊN (không phụ thuộc thứ tự), tiêu đề có thể nằm ở
/// 10 dòng đầu, chỉ đọc sheet đang hiện (sheet ẩn bị bỏ qua), tên cột không phân biệt hoa thường / dấu:
///   Loại chi phí | Kiểu | Mã hàng | Số tiền | Số lượng | Ghi chú
/// Bắt buộc có cột Loại chi phí, Kiểu, Số tiền; Mã hàng, Số lượng, Ghi chú tùy chọn. Mỗi dòng là một khoản chi phí (MonthlyCost); Mã
/// hàng chỉ bắt buộc khi Kiểu = VARIABLE. DEV-ALL-187: báo rõ file sai mẫu (thiếu cột nào) thay vì lỗi chung.
/// </summary>
public sealed class ClosedXmlMonthlyCostParser : IExcelMonthlyCostParser
{
    /// <summary>Số dòng dữ liệu tối đa của một file (10 khoản cố định + khoản theo mã hàng, dư rất nhiều).</summary>
    public const int MaxRows = 500;

    private const int MaxHeaderSearchRows = 10;

    private enum Col { Category, Type, Product, Amount, Quantity, Note }

    private static readonly Dictionary<string, Col> Aliases = new()
    {
        ["loaichiphi"] = Col.Category, ["khoanchiphi"] = Col.Category, ["costcategorycode"] = Col.Category, ["costcategory"] = Col.Category,
        ["kieu"] = Col.Type, ["kieuchiphi"] = Col.Type, ["costtype"] = Col.Type,
        ["mahang"] = Col.Product, ["productcode"] = Col.Product,
        ["sotien"] = Col.Amount, ["amount"] = Col.Amount,
        ["soluong"] = Col.Quantity, ["quantity"] = Col.Quantity,
        ["ghichu"] = Col.Note, ["note"] = Col.Note,
    };

    private static readonly Col[] Required = [Col.Category, Col.Type, Col.Amount];

    private static readonly Dictionary<Col, string> Labels = new()
    {
        [Col.Category] = "Loại chi phí", [Col.Type] = "Kiểu", [Col.Product] = "Mã hàng",
        [Col.Amount] = "Số tiền", [Col.Quantity] = "Số lượng", [Col.Note] = "Ghi chú",
    };

    private sealed record Layout(IXLWorksheet Sheet, int HeaderRow, Dictionary<Col, int> Columns);

    public IReadOnlyList<MonthlyCostExcelRowDto> Parse(Stream stream)
    {
        // IFormFile.OpenReadStream() không seek được; ClosedXML cần stream seek để đọc package.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (buffer.Length == 0) throw new InvalidDataException(XlsxWorkbookLoader.NotExcelMessage);
        buffer.Position = 0;
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        var sheets = workbook.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible).ToList();
        if (sheets.Count == 0) throw new InvalidDataException("File Excel không có sheet nào đang hiện.");

        var layout = FindLayout(sheets);
        var rows = new List<MonthlyCostExcelRowDto>();
        var lastRow = layout.Sheet.LastRowUsed()?.RowNumber() ?? layout.HeaderRow;
        for (var r = layout.HeaderRow + 1; r <= lastRow; r++)
        {
            var cells = layout.Columns.ToDictionary(c => c.Key, c => layout.Sheet.Cell(r, c.Value));
            if (cells.Values.All(c => c.IsEmpty())) continue;   // bỏ dòng trống hoàn toàn (file có thể thừa dòng cuối)
            rows.Add(ParseRow(r, cells));
        }

        if (rows.Count > MaxRows)
            throw new InvalidDataException($"File có {rows.Count} dòng, vượt giới hạn {MaxRows} dòng. Hãy tách thành nhiều lần nhập.");
        return rows;
    }

    /// <summary>Sheet đầu tiên (đang hiện) có đủ cột bắt buộc; không có thì báo thiếu cột của sheet khớp nhiều cột nhất.</summary>
    private static Layout FindLayout(List<IXLWorksheet> sheets)
    {
        Layout? best = null;
        foreach (var sheet in sheets)
        {
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (var r = 1; r <= Math.Min(MaxHeaderSearchRows, lastRow); r++)
            {
                var columns = new Dictionary<Col, int>();
                for (var c = 1; c <= lastCol; c++)
                    if (Aliases.TryGetValue(PayslipLayout.NormalizeKey(sheet.Cell(r, c).GetString()), out var col))
                        columns.TryAdd(col, c);
                if (Required.All(columns.ContainsKey)) return new Layout(sheet, r, columns);
                if (columns.Count > (best?.Columns.Count ?? 0)) best = new Layout(sheet, r, columns);
            }
        }

        var needed = string.Join(", ", Required.Select(c => Labels[c]));
        if (best is null)
            throw new InvalidDataException(
                $"File Excel sai mẫu: không thấy bảng chi phí. Cần dòng tiêu đề có các cột {needed} (và tùy chọn Mã hàng, Số lượng, Ghi chú).");
        var missing = string.Join(", ", Required.Where(c => !best.Columns.ContainsKey(c)).Select(c => Labels[c]));
        throw new InvalidDataException($"File Excel sai mẫu: thiếu cột {missing} ở sheet '{best.Sheet.Name}'. Cần các cột: {needed}.");
    }

    private static MonthlyCostExcelRowDto ParseRow(int rowNumber, Dictionary<Col, IXLCell> cells)
    {
        string? category = Text(cells, Col.Category), costType = Text(cells, Col.Type);
        string? productCode = Text(cells, Col.Product), note = Text(cells, Col.Note);

        decimal? amount = null;
        if (cells.TryGetValue(Col.Amount, out var amountCell) && !amountCell.IsEmpty())
        {
            if (!TryParseDecimal(amountCell, out var a))
                return Error(rowNumber, "Cột Số tiền không phải số.", category, costType, productCode, null, note);
            amount = a;
        }

        int? quantity = null;
        if (cells.TryGetValue(Col.Quantity, out var qtyCell) && !qtyCell.IsEmpty())
        {
            if (!TryParseInt(qtyCell, out var q))
                return Error(rowNumber, "Cột Số lượng không phải số nguyên.", category, costType, productCode, amount, note);
            quantity = q;
        }

        return new MonthlyCostExcelRowDto(rowNumber, category, costType, productCode, amount, quantity, note, null);
    }

    private static MonthlyCostExcelRowDto Error(int rowNumber, string message, string? category, string? costType,
        string? productCode, decimal? amount, string? note) =>
        new(rowNumber, category, costType, productCode, amount, null, note, message);

    private static string? Text(Dictionary<Col, IXLCell> cells, Col col) =>
        cells.TryGetValue(col, out var cell) && !cell.IsEmpty() ? cell.GetString().Trim() : null;

    private static bool TryParseDecimal(IXLCell cell, out decimal value)
    {
        value = 0;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double d))
        {
            value = (decimal)d;
            return true;
        }
        return decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Số nguyên; ô số có phần lẻ (2,5) là lỗi chứ không cắt về 2.</summary>
    private static bool TryParseInt(IXLCell cell, out int value)
    {
        value = 0;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double d))
        {
            if (d != Math.Floor(d) || d is < int.MinValue or > int.MaxValue) return false;
            value = (int)d;
            return true;
        }
        return int.TryParse(cell.GetString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
