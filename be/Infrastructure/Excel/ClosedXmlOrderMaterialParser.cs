using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// DEV-ALL-118: đọc bảng NVL của đơn. Dòng tiêu đề (trong 25 dòng đầu của sheet đang hiển thị) có Mã NVL, Số cần; tùy chọn Khách cấp,
/// Ghi chú (ô trống = 0 / không ghi chú). Chỉ kiểm định dạng ô; đối chiếu NVL, đơn do OrderMaterialService làm.
/// </summary>
public sealed class ClosedXmlOrderMaterialParser : IExcelOrderMaterialParser
{
    private const int MaxHeaderSearchRows = 25;

    public IReadOnlyList<OrderMaterialExcelRow> Parse(Stream stream)
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
            "File Excel sai mẫu: không có bảng có cột 'Mã NVL' và 'Số cần'. Hãy dùng file mẫu của hệ thống.");
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
            if (columns.ContainsKey("manvl") && columns.ContainsKey("socan")) return (r, columns);
        }
        return null;
    }

    private static List<OrderMaterialExcelRow> ReadRows(IXLWorksheet sheet, int headerRow, Dictionary<string, int> columns)
    {
        IXLCell? At(int row, string key) => columns.TryGetValue(key, out var c) ? sheet.Cell(row, c) : null;
        var rows = new List<OrderMaterialExcelRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var code = ExcelCells.CompactText(At(r, "manvl")!).ToUpperInvariant();
            var requiredCell = At(r, "socan")!;
            var customerCell = At(r, "khachcap");
            var note = At(r, "ghichu") is { } n ? ExcelCells.CompactText(n) : string.Empty;
            if (code.Length == 0 && ExcelCells.Text(requiredCell).Length == 0
                && (customerCell is null || ExcelCells.Text(customerCell).Length == 0))
                continue;

            string? error = code.Length == 0 ? Messages.Required("mã NVL") : null;
            var required = ReadQty(requiredCell, "Số cần", ref error);
            var customer = customerCell is null ? 0m : ReadQty(customerCell, "Khách cấp", ref error);
            rows.Add(new OrderMaterialExcelRow(r, code, required, customer, note.Length == 0 ? null : note, error));
        }
        return rows;
    }

    /// <summary>Ô trống = 0; chữ hoặc lỗi công thức thì ghi lỗi đầu tiên và trả null.</summary>
    private static decimal? ReadQty(IXLCell cell, string label, ref string? error)
    {
        if (ExcelCells.TryReadNumber(cell, out var value, out var problem)) return value;
        error ??= $"Cột {label} {problem}.";
        return null;
    }
}
