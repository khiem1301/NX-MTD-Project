using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// Đọc file Excel định mức mã hàng (DEV-ALL-115). Mỗi bảng nhận theo TÊN cột trên dòng tiêu đề (trong 25 dòng đầu của sheet đang
/// hiển thị), không theo tên sheet, nên người dùng được đổi tên sheet hay chèn dòng ghi chú phía trên:
/// <list type="bullet">
/// <item>Thành phần: Tên chi tiết, Loại, Số chi tiết / áo.</item>
/// <item>Định mức NVL: Màu áo, Tên chi tiết, Mã NVL, Ghi chú quy cách (tùy chọn).</item>
/// <item>Nhóm bìa: Nhóm bìa, Chi tiết trên nhóm, Màu áo dùng nhóm (nhiều giá trị cách nhau dấu ;).</item>
/// <item>Bìa của đơn: Nhóm bìa, Mã bìa, Dài (m), Khổ (m), Mã hàng / Trạng thái (tùy chọn); mọi cột khác là cột size.</item>
/// </list>
/// Chỉ kiểm định dạng ô; đối chiếu màu, NVL, chi tiết với hệ thống do SpecImportService làm.
/// </summary>
public sealed class ClosedXmlSpecWorkbookParser : IExcelSpecWorkbookParser
{
    private const int MaxHeaderSearchRows = 25;
    private const int MaxSizeCodeLength = 10;

    private enum Table { Components, Norms, MarkerGroups, OrderMarkers }

    private sealed record Header(int Row, Dictionary<string, int> Columns, IReadOnlyList<(int Col, string Size)> Sizes);

    private static readonly HashSet<string> MarkerKnownKeys = ["stt", "mahang", "nhombia", "mabia", "trangthai", "ghichu"];

    public SpecWorkbookDto Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        var found = new Dictionary<Table, (IXLWorksheet Sheet, Header Header)>();
        foreach (var sheet in workbook.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible))
        {
            var match = FindTable(sheet);
            if (match is { } m && !found.ContainsKey(m.Table)) found[m.Table] = (sheet, m.Header);
        }
        if (found.Count == 0)
            throw new InvalidDataException(
                "File Excel sai mẫu: không có bảng Thành phần, Định mức NVL, Nhóm bìa hoặc Bìa của đơn. Hãy dùng file mẫu của hệ thống.");

        List<T> Read<T>(Table table, Func<IXLWorksheet, Header, int, T?> row) where T : class =>
            found.TryGetValue(table, out var t) ? ReadRows(t.Sheet, t.Header, row) : [];

        return new SpecWorkbookDto(
            found.ContainsKey(Table.Components), Read<SpecComponentExcelRow>(Table.Components, ComponentRow),
            found.ContainsKey(Table.Norms), Read<SpecNormExcelRow>(Table.Norms, NormRow),
            found.ContainsKey(Table.MarkerGroups), Read<SpecMarkerGroupExcelRow>(Table.MarkerGroups, MarkerGroupRow),
            found.ContainsKey(Table.OrderMarkers), Read<SpecOrderMarkerExcelRow>(Table.OrderMarkers, OrderMarkerRow),
            found.TryGetValue(Table.OrderMarkers, out var om) ? om.Header.Sizes.Select(s => s.Size).ToList() : []);
    }

    // ---------- Nhận bảng ----------

    private static (Table Table, Header Header)? FindTable(IXLWorksheet sheet)
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
            bool Has(params string[] keys) => keys.All(columns.ContainsKey);

            if (Has("nhombia", "mabia") && Starts(columns, "dai") is not null && Starts(columns, "kho") is not null)
                return (Table.OrderMarkers, new Header(r, columns, SizeColumns(sheet, r, lastCol)));
            if (Has("nhombia", "chitiettrennhom", "mauaodungnhom")) return (Table.MarkerGroups, new Header(r, columns, []));
            if (Has("mauao", "tenchitiet", "manvl")) return (Table.Norms, new Header(r, columns, []));
            if (Has("tenchitiet", "loai") && Starts(columns, "sochitiet") is not null)
                return (Table.Components, new Header(r, columns, []));
        }
        return null;
    }

    private static int? Starts(Dictionary<string, int> columns, string prefix) =>
        columns.Where(c => c.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(c => (int?)c.Value).FirstOrDefault();

    /// <summary>Bảng bìa của đơn: cột có tiêu đề mà không phải cột đã biết là cột size (tên cột = mã size, viết hoa).</summary>
    private static List<(int Col, string Size)> SizeColumns(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        var sizes = new List<(int, string)>();
        for (var c = 1; c <= lastCol; c++)
        {
            var key = ExcelCells.Key(sheet.Cell(headerRow, c));
            if (key.Length == 0 || MarkerKnownKeys.Contains(key) || key.StartsWith("dai", StringComparison.Ordinal)
                || key.StartsWith("kho", StringComparison.Ordinal))
                continue;
            var size = ExcelCells.Text(sheet.Cell(headerRow, c)).ToUpperInvariant();
            if (size.Length <= MaxSizeCodeLength) sizes.Add((c, size));
        }
        return sizes;
    }

    private static List<T> ReadRows<T>(IXLWorksheet sheet, Header header, Func<IXLWorksheet, Header, int, T?> read) where T : class
    {
        var rows = new List<T>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = header.Row + 1; r <= lastRow; r++)
        {
            var row = read(sheet, header, r);
            if (row is not null) rows.Add(row);
        }
        return rows;
    }

    private static string Text(IXLWorksheet sheet, int row, Header header, string key) =>
        header.Columns.TryGetValue(key, out var c) ? ExcelCells.CompactText(sheet.Cell(row, c)) : string.Empty;

    private static IXLCell? At(IXLWorksheet sheet, int row, int? col) => col is { } c ? sheet.Cell(row, c) : null;

    // ---------- Đọc từng loại dòng (null = dòng trống, bỏ qua) ----------

    private static SpecComponentExcelRow? ComponentRow(IXLWorksheet sheet, Header h, int r)
    {
        var name = Text(sheet, r, h, "tenchitiet");
        var typeText = Text(sheet, r, h, "loai");
        var piecesCell = At(sheet, r, Starts(h.Columns, "sochitiet"));
        var piecesText = piecesCell is null ? string.Empty : ExcelCells.Text(piecesCell);
        if (name.Length == 0 && typeText.Length == 0 && piecesText.Length == 0) return null;

        string? error = name.Length == 0 ? Messages.Required("tên chi tiết") : null;
        var type = ComponentTypeOf(typeText);
        if (type is null) error ??= "Loại phải là PANEL (vải cắt) hoặc TRIM (phụ liệu).";
        var pieces = ReadWhole(piecesCell, "Số chi tiết / áo", 1, short.MaxValue, ref error);
        return new SpecComponentExcelRow(r, name, type, pieces is null ? (short?)null : (short)(pieces == 0 ? 1 : pieces.Value), error);
    }

    private static string? ComponentTypeOf(string text) => PayslipLayout.NormalizeKey(text) switch
    {
        "panel" or "vaicat" or "chitietcat" => ComponentTypes.Panel,
        "trim" or "phulieu" => ComponentTypes.Trim,
        _ => null,
    };

    private static SpecNormExcelRow? NormRow(IXLWorksheet sheet, Header h, int r)
    {
        var color = Text(sheet, r, h, "mauao");
        var component = Text(sheet, r, h, "tenchitiet");
        var code = Text(sheet, r, h, "manvl").ToUpperInvariant();
        var note = Text(sheet, r, h, "ghichuquycach");
        if (color.Length == 0 && component.Length == 0 && code.Length == 0) return null;

        var error = color.Length == 0 ? Messages.Required("màu áo")
            : component.Length == 0 ? Messages.Required("tên chi tiết")
            : code.Length == 0 ? Messages.Required("mã NVL") : null;
        return new SpecNormExcelRow(r, color, component, code, note.Length == 0 ? null : note, error);
    }

    private static SpecMarkerGroupExcelRow? MarkerGroupRow(IXLWorksheet sheet, Header h, int r)
    {
        var noCell = At(sheet, r, h.Columns["nhombia"])!;
        var components = ExcelCells.SplitList(Text(sheet, r, h, "chitiettrennhom"));
        var colors = ExcelCells.SplitList(Text(sheet, r, h, "mauaodungnhom"));
        if (ExcelCells.Text(noCell).Length == 0 && components.Count == 0 && colors.Count == 0) return null;

        string? error = null;
        var no = ReadWhole(noCell, "Nhóm bìa", 1, byte.MaxValue, ref error);
        if (no == 0) error ??= Messages.Required("nhóm bìa");
        if (components.Count == 0) error ??= Messages.Required("chi tiết trên nhóm");
        if (colors.Count == 0) error ??= Messages.Required("màu áo dùng nhóm");
        return new SpecMarkerGroupExcelRow(r, no is null or 0 ? null : no, components, colors, error);
    }

    private static SpecOrderMarkerExcelRow? OrderMarkerRow(IXLWorksheet sheet, Header h, int r)
    {
        var noCell = At(sheet, r, h.Columns["nhombia"])!;
        var code = Text(sheet, r, h, "mabia").ToUpperInvariant();
        var lengthCell = At(sheet, r, Starts(h.Columns, "dai"))!;
        var widthCell = At(sheet, r, Starts(h.Columns, "kho"))!;
        if (ExcelCells.Text(noCell).Length == 0 && code.Length == 0 && ExcelCells.Text(lengthCell).Length == 0) return null;

        string? error = null;
        var no = ReadWhole(noCell, "Nhóm bìa", 1, byte.MaxValue, ref error);
        if (no == 0) error ??= Messages.Required("nhóm bìa");
        if (code.Length == 0) error ??= Messages.Required("mã bìa");
        var length = ReadDecimal(lengthCell, "Dài (m)", ref error);
        var width = ReadDecimal(widthCell, "Khổ (m)", ref error);
        var status = StatusOf(Text(sheet, r, h, "trangthai"));
        if (status is null) error ??= "Trạng thái phải là ACTIVE (đang dùng) hoặc DISCONTINUED (ngừng dùng).";

        var ratios = new List<MarkerRatioDto>();
        foreach (var (col, size) in h.Sizes)
        {
            var pieces = ReadWhole(sheet.Cell(r, col), $"Cột size {size}", 0, short.MaxValue, ref error);
            if (pieces is > 0) ratios.Add(new MarkerRatioDto(size, (short)pieces.Value));
        }
        var product = Text(sheet, r, h, "mahang");
        return new SpecOrderMarkerExcelRow(r, product.Length == 0 ? null : product.ToUpperInvariant(), no is null or 0 ? null : no,
            code, length, width, status ?? RecordStatus.Active, ratios, error);
    }

    private static string? StatusOf(string text) => PayslipLayout.NormalizeKey(text) switch
    {
        "" or "active" or "dangdung" => RecordStatus.Active,
        "discontinued" or "ngungdung" => RecordStatus.Discontinued,
        _ => null,
    };

    // ---------- Đọc số ----------

    /// <summary>Số nguyên trong [min, max]; ô trống trả 0. Sai thì ghi lỗi đầu tiên và trả null.</summary>
    private static int? ReadWhole(IXLCell? cell, string label, int min, int max, ref string? error)
    {
        if (cell is null) return 0;
        if (!ExcelCells.TryReadNumber(cell, out var value, out var problem))
        {
            error ??= $"Cột {label} {problem}.";
            return null;
        }
        if (value == 0) return 0;
        if (value != decimal.Truncate(value) || value < min || value > max)
        {
            error ??= $"Cột {label} phải là số nguyên từ {min} đến {max}.";
            return null;
        }
        return (int)value;
    }

    /// <summary>Số thập phân; ô trống trả null (bước đối chiếu báo thiếu).</summary>
    private static decimal? ReadDecimal(IXLCell cell, string label, ref string? error)
    {
        if (ExcelCells.Text(cell).Length == 0) return null;
        if (ExcelCells.TryReadNumber(cell, out var value, out var problem)) return value;
        error ??= $"Cột {label} {problem}.";
        return null;
    }
}
