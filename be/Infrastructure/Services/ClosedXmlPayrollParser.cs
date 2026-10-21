using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Đọc file "Bảng thanh toán lương" thật của xưởng bằng ClosedXML (DEV-MI-09-4).
/// <list type="bullet">
/// <item>Duyệt mọi sheet; sheet nào có ô "SLĐ" ở 25 dòng đầu thì là sheet lương (mỗi tổ một sheet).</item>
/// <item>Tiêu đề 2 dòng (nhóm + cột con): tên cột = dòng con nếu có, không thì dòng nhóm. Cột nhận theo TÊN,
/// quy đổi qua <see cref="PayslipLayout"/>; cột mã hàng (nằm giữa "Thưởng PL" và "Tổng các mã") nhận động.</item>
/// <item>Chỉ đọc các cột bên trái cột "Thực lĩnh"/"Còn lĩnh"; phần "Bảng phụ" bên phải bị bỏ qua.</item>
/// <item>Dòng nhân viên: có Họ tên và SLĐ (SLĐ = mã chấm công). Dừng ở dòng "Tổng cộng" để không đọc nhầm
/// bảng nhỏ bên dưới. Dòng số thứ tự cột, "Tổng cán bộ" tự bị bỏ qua.</item>
/// <item>Dòng "ĐVT: 1000 Đ" → mọi cột tiền nhân 1000 sang đồng.</item>
/// <item>Đọc giá trị đã tính sẵn trong ô (hệ thống không tính lương, GBR-12). Ô lỗi công thức (#REF!...) làm dòng đó lỗi.</item>
/// </list>
/// </summary>
public sealed class ClosedXmlPayrollParser : IExcelPayrollParser
{
    private const int MaxHeaderSearchRows = 25;

    private sealed record ColumnDef(int Index, string Label, PayslipColumn? Known, bool IsProductCode);

    private sealed record SheetLayout(
        int HeaderRow, int FirstDataRow, int EmployeeCodeCol, int FullNameCol, int NetPayCol,
        decimal MoneyUnit, IReadOnlyList<ColumnDef> Columns);

    public IReadOnlyList<PayrollExcelRowDto> Parse(Stream stream)
    {
        // IFormFile.OpenReadStream() không seek được; ClosedXML cần stream seek để đọc package.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        using var workbook = new XLWorkbook(buffer);

        var rows = new List<PayrollExcelRowDto>();
        var foundPayrollSheet = false;
        foreach (var sheet in workbook.Worksheets)
        {
            var layout = FindLayout(sheet);
            if (layout is null) continue;   // sheet phụ không có cột SLĐ (ghi chú, bảng tính nháp...)

            foundPayrollSheet = true;
            ReadSheet(sheet, layout, rows);
        }

        if (!foundPayrollSheet)
            throw new InvalidDataException(
                "File Excel sai mẫu: không có sheet nào chứa cột 'SLĐ' và cột 'Thực lĩnh' (hoặc 'Còn lĩnh').");

        return rows;
    }

    // ---------- Nhận diện bố cục sheet ----------

    private static SheetLayout? FindLayout(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastCol == 0) return null;

        var headerRow = 0;
        var codeCol = 0;
        for (var r = 1; r <= Math.Min(MaxHeaderSearchRows, lastRow) && headerRow == 0; r++)
        {
            for (var c = 1; c <= lastCol; c++)
            {
                if (PayslipLayout.NormalizeKey(CellText(sheet.Cell(r, c))) != PayslipLayout.EmployeeCodeKey) continue;
                headerRow = r;
                codeCol = c;
                break;
            }
        }
        if (headerRow == 0) return null;

        var subRow = headerRow + 1;
        var netCol = 0;
        var nameCol = 0;
        for (var c = 1; c <= lastCol; c++)
        {
            var key = HeaderKey(sheet, headerRow, subRow, c);
            if (netCol == 0 && key is PayslipLayout.NetPayKey or PayslipLayout.NetPayAltKey) netCol = c;
            if (nameCol == 0 && key is PayslipLayout.FullNameKey or "hovaten") nameCol = c;
        }
        if (netCol == 0)
            throw new InvalidDataException(
                $"Sheet '{sheet.Name}' có cột 'SLĐ' nhưng thiếu cột 'Thực lĩnh' hoặc 'Còn lĩnh'.");

        var columns = BuildColumns(sheet, headerRow, subRow, netCol);
        return new SheetLayout(headerRow, subRow + 1, codeCol, nameCol == 0 ? 2 : nameCol, netCol,
            ReadMoneyUnit(sheet, headerRow), columns);
    }

    private static List<ColumnDef> BuildColumns(IXLWorksheet sheet, int headerRow, int subRow, int netCol)
    {
        var columns = new List<ColumnDef>();
        for (var c = 1; c < netCol; c++)
        {
            var label = HeaderLabel(sheet, headerRow, subRow, c);
            columns.Add(new ColumnDef(c, label, PayslipLayout.FindColumn(label), false));
        }

        // Cột mã hàng: nằm giữa "Thưởng PL" và "Tổng các mã", tên không có trong danh mục (VD WES SHORT, 8698).
        var bonusCol = columns.FirstOrDefault(c => PayslipLayout.NormalizeKey(c.Label) is PayslipLayout.PieceBonusKey
            or "thuongphanloai")?.Index ?? 0;
        var subtotalCol = columns.FirstOrDefault(c =>
            PayslipLayout.NormalizeKey(c.Label) == PayslipLayout.PieceSubtotalKey)?.Index ?? 0;
        if (bonusCol == 0 || subtotalCol == 0 || bonusCol >= subtotalCol) return columns;

        return columns.Select(c =>
            c.Index > bonusCol && c.Index < subtotalCol && c.Known is null && c.Label.Length > 0
                ? c with { IsProductCode = true }
                : c).ToList();
    }

    /// <summary>"ĐVT: 1000 Đ" ở phía trên tiêu đề → 1000, ngược lại 1.</summary>
    private static decimal ReadMoneyUnit(IXLWorksheet sheet, int headerRow)
    {
        for (var r = 1; r < headerRow; r++)
        {
            foreach (var cell in sheet.Row(r).CellsUsed())
            {
                var key = PayslipLayout.NormalizeKey(CellText(cell));
                if (key.Contains("dvt", StringComparison.Ordinal) && key.Contains("1000", StringComparison.Ordinal))
                    return 1000m;
            }
        }
        return 1m;
    }

    private static string HeaderLabel(IXLWorksheet sheet, int headerRow, int subRow, int col)
    {
        var sub = CollapseSpaces(CellText(sheet.Cell(subRow, col)));
        return sub.Length > 0 ? sub : CollapseSpaces(CellText(sheet.Cell(headerRow, col)));
    }

    private static string HeaderKey(IXLWorksheet sheet, int headerRow, int subRow, int col) =>
        PayslipLayout.NormalizeKey(HeaderLabel(sheet, headerRow, subRow, col));

    // ---------- Đọc dòng nhân viên ----------

    private static void ReadSheet(IXLWorksheet sheet, SheetLayout layout, List<PayrollExcelRowDto> rows)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = layout.FirstDataRow; r <= lastRow; r++)
        {
            var first = PayslipLayout.NormalizeKey(CellText(sheet.Cell(r, 1)));
            var second = PayslipLayout.NormalizeKey(CellText(sheet.Cell(r, 2)));
            // Hết bảng nhân viên: dòng "Tổng cộng" (hoặc một bảng mới bắt đầu bằng "STT").
            if (first.StartsWith("tongcong", StringComparison.Ordinal)
                || second.StartsWith("tongcong", StringComparison.Ordinal) || first == "stt")
                break;

            var codeCell = sheet.Cell(r, layout.EmployeeCodeCol);
            var fullName = CollapseSpaces(CellText(sheet.Cell(r, layout.FullNameCol)));
            // Dòng số thứ tự cột (1,2,3...) và "Tổng cán bộ": thiếu SLĐ hoặc Họ tên không có chữ → không phải nhân viên.
            if (codeCell.IsEmpty() || !fullName.Any(char.IsLetter)) continue;

            rows.Add(ReadEmployee(sheet, layout, r, fullName));
        }
    }

    private static PayrollExcelRowDto ReadEmployee(IXLWorksheet sheet, SheetLayout layout, int row, string fullName)
    {
        PayrollExcelRowDto Error(string message, string? code = null) =>
            new(sheet.Name, row, code, fullName, null, null, null, [], message);

        if (!TryReadAttendanceCode(sheet.Cell(row, layout.EmployeeCodeCol), out var code))
            return Error($"Sheet '{sheet.Name}' dòng {row}: SLĐ không phải số nguyên.");

        if (!TryReadNumber(sheet.Cell(row, layout.NetPayCol), out var netPay, out var netError))
            return Error($"Sheet '{sheet.Name}' dòng {row}: cột Thực lĩnh {netError}.", code);
        netPay *= layout.MoneyUnit;

        var items = new List<PayrollExcelItemDto>();
        decimal? workDays = null;
        string? grade = null;

        foreach (var column in layout.Columns)
        {
            var known = column.Known;
            if (known is { Group: null }) continue;   // số tổng, hệ số, cột nhận dạng...
            if (known is null && !column.IsProductCode) continue;   // cột lạ: để phép đối chiếu tổng báo cảnh báo

            var cell = sheet.Cell(row, column.Index);

            if (known is { Kind: PayslipValueKind.Text })
            {
                var text = CollapseSpaces(CellText(cell)).ToUpperInvariant();
                if (text.Length == 0) continue;
                grade = text;
                items.Add(new PayrollExcelItemDto(known.Group!, $"{known.DisplayName}: {text}", 0m));
                continue;
            }

            if (!TryReadNumber(cell, out var value, out var error))
                return Error($"Sheet '{sheet.Name}' dòng {row}: cột '{column.Label}' {error}.", code);

            if (known?.CountsAsWorkDay == true) workDays = (workDays ?? 0m) + value;
            if (value == 0m) continue;   // ô trống / "-" : không tạo khoản

            var isMoney = known is null || known.Kind == PayslipValueKind.Money;
            items.Add(new PayrollExcelItemDto(
                known?.Group ?? PayslipItemGroup.Addition,
                known?.DisplayName ?? column.Label,
                isMoney ? value * layout.MoneyUnit : value));
        }

        return new PayrollExcelRowDto(sheet.Name, row, code, fullName, grade, workDays, netPay, items, null);
    }

    // ---------- Đọc ô ----------

    private static bool TryReadAttendanceCode(IXLCell cell, out string code)
    {
        code = string.Empty;
        if (!TryReadNumber(cell, out var number, out _) || number != decimal.Truncate(number) || number <= 0)
            return false;

        code = ((long)number).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Số trong ô; ô trống hoặc "-" là 0. Ô lỗi công thức (#REF!, #ERROR!...) hay chữ khác trả false.</summary>
    private static bool TryReadNumber(IXLCell cell, out decimal value, out string error)
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
            error = "không đọc được giá trị (công thức lỗi)";
            return false;
        }
    }

    private static string CellText(IXLCell cell)
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

    private static string CollapseSpaces(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
