using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// DEV-ALL-119: đọc bảng mã hàng. Dòng tiêu đề (trong 25 dòng đầu của sheet đang hiển thị) có Mã hàng và Tên mã hàng (hoặc Tên hàng);
/// tùy chọn Mô tả. Bảng phải không có cột Màu / Đơn giá CMT / Nhóm bìa để không nhầm với bảng đơn hàng hay bìa của đơn.
/// </summary>
public sealed class ClosedXmlProductParser : IExcelProductParser
{
    private const int MaxHeaderSearchRows = 25;

    public IReadOnlyList<ProductExcelRow> Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        foreach (var sheet in workbook.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible))
        {
            var header = FindHeader(sheet);
            if (header is not null) return ReadRows(sheet, header.Value.Row, header.Value.Columns);
        }
        throw new InvalidDataException(
            "File Excel sai mẫu: không có bảng có cột 'Mã hàng' và 'Tên mã hàng'. Hãy dùng file mẫu của hệ thống.");
    }

    private static (int Row, Dictionary<string, int> Columns)? FindHeader(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var r = 1; r <= Math.Min(MaxHeaderSearchRows, lastRow); r++)
        {
            var columns = new Dictionary<string, int>();
            for (var c = 1; c <= lastCol; c++)
            {
                var key = ExcelCells.Key(sheet.Cell(r, c));
                if (key.Length > 0) columns.TryAdd(key, c);
            }
            var hasName = columns.ContainsKey("tenmahang") || columns.ContainsKey("tenhang");
            var otherTable = columns.ContainsKey("mau") || columns.ContainsKey("dongiacmt") || columns.ContainsKey("nhombia");
            if (columns.ContainsKey("mahang") && hasName && !otherTable) return (r, columns);
        }
        return null;
    }

    private static List<ProductExcelRow> ReadRows(IXLWorksheet sheet, int headerRow, Dictionary<string, int> columns)
    {
        var nameCol = columns.GetValueOrDefault("tenmahang", columns.GetValueOrDefault("tenhang"));
        var rows = new List<ProductExcelRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var code = ExcelCells.CompactText(sheet.Cell(r, columns["mahang"])).ToUpperInvariant();
            var name = ExcelCells.CompactText(sheet.Cell(r, nameCol));
            var description = columns.TryGetValue("mota", out var d) ? ExcelCells.Text(sheet.Cell(r, d)) : string.Empty;
            if (code.Length == 0 && name.Length == 0 && description.Length == 0) continue;
            rows.Add(new ProductExcelRow(r, code, name, description.Length == 0 ? null : description));
        }
        return rows;
    }
}
