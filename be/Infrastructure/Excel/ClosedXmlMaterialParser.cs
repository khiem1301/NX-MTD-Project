using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// DEV-ALL-192: đọc sheet "NVL" — thông tin đầy đủ của NVL. Dòng tiêu đề (trong 25 dòng đầu của sheet đang hiển thị) có Mã NVL, Tên NVL,
/// Loại; tùy chọn Đơn vị, Màu, Khổ vải (m). Chỉ kiểm định dạng ô; đối chiếu danh mục NVL do ExcelBundleService làm.
/// </summary>
public sealed class ClosedXmlMaterialParser : IExcelMaterialParser
{
    private const int MaxHeaderSearchRows = 25;

    private static readonly Dictionary<string, string> TypeAliases = new()
    {
        ["fabric"] = MaterialTypes.Fabric, ["vai"] = MaterialTypes.Fabric, ["vaichinh"] = MaterialTypes.Fabric,
        ["trim"] = MaterialTypes.Trim, ["phulieu"] = MaterialTypes.Trim,
        ["scrap"] = MaterialTypes.Scrap, ["phepham"] = MaterialTypes.Scrap,
    };

    public IReadOnlyList<MaterialExcelRow> Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        foreach (var sheet in workbook.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible))
        {
            var header = FindHeader(sheet);
            if (header is not null) return ReadRows(sheet, header.Value.Row, header.Value.Columns);
        }
        throw new ExcelTableMissingException(Messages.MaterialSheetMissing);
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
            // "Tên NVL" + "Loại" phân biệt sheet NVL với bảng NVL của đơn (Mã NVL, Số cần) và định mức (Màu áo, Tên chi tiết, Mã NVL).
            if (columns.ContainsKey("manvl") && columns.ContainsKey("tennvl") && columns.ContainsKey("loai")
                && !columns.ContainsKey("socan"))
                return (r, columns);
        }
        return null;
    }

    private static List<MaterialExcelRow> ReadRows(IXLWorksheet sheet, int headerRow, Dictionary<string, int> columns)
    {
        IXLCell? At(int row, string key) => columns.TryGetValue(key, out var c) ? sheet.Cell(row, c) : null;
        string TextAt(int row, string key) => At(row, key) is { } cell ? ExcelCells.CompactText(cell) : string.Empty;
        var widthColumn = columns.Where(c => c.Key.StartsWith("kho", StringComparison.Ordinal)).Select(c => (int?)c.Value).FirstOrDefault();

        var rows = new List<MaterialExcelRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var code = TextAt(r, "manvl").ToUpperInvariant();
            var name = TextAt(r, "tennvl");
            var typeText = TextAt(r, "loai");
            var unit = TextAt(r, "donvi");
            var color = TextAt(r, "mau");
            var widthCell = widthColumn is { } w ? sheet.Cell(r, w) : null;
            if (code.Length == 0 && name.Length == 0 && typeText.Length == 0 && unit.Length == 0 && color.Length == 0
                && (widthCell is null || ExcelCells.Text(widthCell).Length == 0))
                continue;

            string? error = code.Length == 0 ? Messages.Required("mã NVL") : null;
            string? type = null;
            if (typeText.Length > 0 && !TypeAliases.TryGetValue(PayslipLayout.NormalizeKey(typeText), out type))
                error ??= Messages.MaterialSheetTypeInvalid;
            decimal? width = null;
            if (widthCell is not null)
            {
                if (ExcelCells.TryReadNumber(widthCell, out var value, out var problem))
                    width = ExcelCells.Text(widthCell).Length == 0 ? null : value;
                else error ??= $"Cột Khổ vải {problem}.";
            }
            rows.Add(new MaterialExcelRow(r, code, name.Length == 0 ? null : name, type, unit.Length == 0 ? null : unit,
                color.Length == 0 ? null : color, width, error));
        }
        return rows;
    }
}
