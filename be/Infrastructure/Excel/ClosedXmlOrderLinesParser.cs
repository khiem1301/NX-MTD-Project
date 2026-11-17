using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// DEV-ALL-117: đọc bảng dòng hàng của đơn dạng ma trận. Dòng tiêu đề (trong 25 dòng đầu của sheet đang hiển thị) có
/// Mã hàng, Màu, Đơn giá CMT; mọi cột có tiêu đề khác là một size (tên cột = mã size). Mỗi dòng là một mã hàng × màu, ô size là
/// số lượng (trống / 0 = không đặt size đó). Chỉ kiểm định dạng ô; đối chiếu mã hàng do OrderImportService làm.
/// </summary>
public sealed class ClosedXmlOrderLinesParser : IExcelOrderLinesParser
{
    private const int MaxHeaderSearchRows = 25;
    private const int MaxSizeCodeLength = 10;
    private static readonly HashSet<string> KnownKeys = ["stt", "mahang", "mau", "dongiacmt", "tenhang", "ghichu"];

    private sealed record Layout(int HeaderRow, int ProductCol, int ColorCol, int PriceCol, IReadOnlyList<(int Col, string Size)> Sizes);

    public OrderImportExcelDto Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        foreach (var sheet in workbook.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible))
        {
            var layout = FindLayout(sheet);
            if (layout is not null) return new OrderImportExcelDto(layout.Sizes.Select(s => s.Size).ToList(), ReadRows(sheet, layout));
        }
        throw new InvalidDataException(
            "File Excel sai mẫu: không có bảng có cột 'Mã hàng', 'Màu', 'Đơn giá CMT' và các cột size. Hãy dùng file mẫu của hệ thống.");
    }

    private static Layout? FindLayout(IXLWorksheet sheet)
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
            if (!columns.TryGetValue("mahang", out var product) || !columns.TryGetValue("mau", out var color)
                || !columns.TryGetValue("dongiacmt", out var price))
                continue;

            var sizes = new List<(int, string)>();
            for (var c = 1; c <= lastCol; c++)
            {
                var key = ExcelCells.Key(sheet.Cell(r, c));
                var size = ExcelCells.Text(sheet.Cell(r, c)).ToUpperInvariant();
                if (key.Length > 0 && !KnownKeys.Contains(key) && size.Length <= MaxSizeCodeLength) sizes.Add((c, size));
            }
            return new Layout(r, product, color, price, sizes);
        }
        return null;
    }

    private static List<OrderImportExcelRow> ReadRows(IXLWorksheet sheet, Layout layout)
    {
        var rows = new List<OrderImportExcelRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = layout.HeaderRow + 1; r <= lastRow; r++)
        {
            var product = ExcelCells.CompactText(sheet.Cell(r, layout.ProductCol)).ToUpperInvariant();
            var color = ExcelCells.CompactText(sheet.Cell(r, layout.ColorCol));
            var priceCell = sheet.Cell(r, layout.PriceCol);
            var anyQty = layout.Sizes.Any(s => ExcelCells.Text(sheet.Cell(r, s.Col)).Length > 0);
            if (product.Length == 0 && color.Length == 0 && ExcelCells.Text(priceCell).Length == 0 && !anyQty) continue;

            string? error = product.Length == 0 ? Messages.Required("mã hàng")
                : color.Length == 0 ? Messages.Required("màu") : null;
            decimal? price = null;
            if (ExcelCells.Text(priceCell).Length == 0) error ??= Messages.Required("đơn giá CMT");
            else if (ExcelCells.TryReadNumber(priceCell, out var p, out var problem)) price = p;
            else error ??= $"Cột Đơn giá CMT {problem}.";

            var quantities = new List<OrderImportQtyDto>();
            foreach (var (col, size) in layout.Sizes)
            {
                if (!ExcelCells.TryReadNumber(sheet.Cell(r, col), out var qty, out var problem))
                {
                    error ??= $"Cột size {size} {problem}.";
                    continue;
                }
                if (qty == 0) continue;
                if (qty < 0 || qty != decimal.Truncate(qty) || qty > int.MaxValue)
                {
                    error ??= $"Cột size {size} phải là số nguyên dương.";
                    continue;
                }
                quantities.Add(new OrderImportQtyDto(size, (int)qty));
            }
            rows.Add(new OrderImportExcelRow(r, product, color, price, quantities, error));
        }
        return rows;
    }
}
