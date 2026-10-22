using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Đọc file Excel quy trình công nghệ của mã hàng (KI-01). Mỗi sheet đang hiển thị có dòng tiêu đề chứa
/// "BƯỚC CÔNG VIỆC" và "TGCT" (trong 25 dòng đầu) là một bảng công đoạn:
/// <list type="bullet">
/// <item>Cột nhận theo TÊN: STT, BƯỚC CÔNG VIỆC, BẬC, TGCT (thời gian chuẩn, giây), THIẾT BỊ. Cột TIỀN / TGQĐ không đọc.</item>
/// <item>Dòng công đoạn: có STT là số nguyên dương và có tên. Dòng chỉ có STT (chỗ trống) bị bỏ qua; dòng không có STT
/// (tiêu đề nhóm "LÀ", "CHUYÊN DÙNG", dòng tổng...) cũng bỏ qua, và dòng tên "Dự phòng" cũng vậy.</item>
/// <item>Đọc giá trị đã tính sẵn trong ô (bỏ công thức khi mở file, xem <see cref="XlsxWorkbookLoader"/>).</item>
/// </list>
/// </summary>
public sealed class ClosedXmlOperationParser : IExcelOperationParser
{
    private const int MaxHeaderSearchRows = 25;
    private const int MaxNameLength = 200;

    private sealed record Layout(
        int HeaderRow, int SttCol, int NameCol, int SkillCol, int TimeCol, int EquipmentCol, int RateCol);

    public IReadOnlyList<OperationExcelSheetDto> Parse(Stream stream)
    {
        // IFormFile.OpenReadStream() không seek được; ClosedXML cần stream seek để đọc package.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var workbook = XlsxWorkbookLoader.Open(buffer);

        var sheets = new List<OperationExcelSheetDto>();
        foreach (var sheet in workbook.Worksheets)
        {
            if (sheet.Visibility != XLWorksheetVisibility.Visible) continue;   // sheet ẩn là bản nháp cũ
            var layout = FindLayout(sheet);
            if (layout is null) continue;
            sheets.Add(new OperationExcelSheetDto(
                sheet.Name, FindProductText(sheet, layout.HeaderRow), layout.RateCol != 0, ReadRows(sheet, layout)));
        }

        if (sheets.Count == 0)
            throw new InvalidDataException(
                "File Excel sai mẫu: không có sheet nào chứa bảng có cột 'BƯỚC CÔNG VIỆC' và 'TGCT'.");
        return sheets;
    }

    private static Layout? FindLayout(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var r = 1; r <= Math.Min(MaxHeaderSearchRows, lastRow); r++)
        {
            int stt = 0, name = 0, skill = 0, time = 0, equipment = 0, rate = 0;
            for (var c = 1; c <= lastCol; c++)
            {
                switch (PayslipLayout.NormalizeKey(CellText(sheet.Cell(r, c))))
                {
                    case "stt": stt = stt == 0 ? c : stt; break;
                    case "buoccongviec": name = name == 0 ? c : name; break;
                    case "bac": skill = skill == 0 ? c : skill; break;
                    case "tgct": time = time == 0 ? c : time; break;
                    case "thietbi": equipment = equipment == 0 ? c : equipment; break;
                    case "tien": rate = rate == 0 ? c : rate; break;
                }
            }
            if (name != 0 && time != 0) return new Layout(r, stt == 0 ? 1 : stt, name, skill, time, equipment, rate);
        }
        return null;
    }

    /// <summary>Dòng phía trên tiêu đề kiểu "MÃ : LUNA(màu trơn)- KHÁCH HÀNG : H&amp;M" để hiện cho người dùng đối chiếu.</summary>
    private static string? FindProductText(IXLWorksheet sheet, int headerRow)
    {
        for (var r = 1; r < headerRow; r++)
        {
            foreach (var cell in sheet.Row(r).CellsUsed())
            {
                var text = CellText(cell);
                if (PayslipLayout.NormalizeKey(text).StartsWith("ma", StringComparison.Ordinal) && text.Contains(':'))
                    return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }
        return null;
    }

    private static List<OperationExcelRowDto> ReadRows(IXLWorksheet sheet, Layout layout)
    {
        var rows = new List<OperationExcelRowDto>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = layout.HeaderRow + 1; r <= lastRow; r++)
        {
            if (!TryReadNumber(sheet.Cell(r, layout.SttCol), out var sttValue, out _) || sttValue <= 0
                || sttValue != decimal.Truncate(sttValue) || sttValue > short.MaxValue)
                continue;   // không có STT hợp lệ: tiêu đề nhóm, dòng tổng, ghi chú...

            var name = string.Join(' ', CellText(sheet.Cell(r, layout.NameCol)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (name.Length == 0) continue;   // chỗ trống đã đánh số sẵn
            if (PayslipLayout.NormalizeKey(name) == "duphong") continue;   // dòng "Dự phòng": file không tính vào tổng TGCT

            var stt = (int)sttValue;
            var error = ValidateName(name);
            var skill = ReadSkill(sheet, r, layout, ref error);
            var time = ReadTime(sheet, r, layout, ref error);
            var equipment = layout.EquipmentCol == 0 ? null : NullIfEmpty(CellText(sheet.Cell(r, layout.EquipmentCol)));
            var rate = ReadRate(sheet, r, layout, ref error);
            rows.Add(new OperationExcelRowDto(r, stt, name, skill, time, equipment, rate, error));
        }
        return rows;
    }

    private static string? ValidateName(string name) =>
        name.Length > MaxNameLength ? $"Tên công đoạn dài quá {MaxNameLength} ký tự." : null;

    private static byte? ReadSkill(IXLWorksheet sheet, int row, Layout layout, ref string? error)
    {
        if (layout.SkillCol == 0) return null;
        if (!TryReadNumber(sheet.Cell(row, layout.SkillCol), out var value, out var problem))
        {
            error ??= $"Cột BẬC {problem}.";
            return null;
        }
        if (value == 0) return null;
        if (value != decimal.Truncate(value) || value is < 1 or > 9)
        {
            error ??= "Bậc phải là số nguyên từ 1 đến 9.";
            return null;
        }
        return (byte)value;
    }

    private static decimal? ReadTime(IXLWorksheet sheet, int row, Layout layout, ref string? error)
    {
        if (!TryReadNumber(sheet.Cell(row, layout.TimeCol), out var value, out var problem))
        {
            error ??= $"Cột TGCT {problem}.";
            return null;
        }
        if (value < 0)
        {
            error ??= "TGCT không được âm.";
            return null;
        }
        return value == 0 ? null : Math.Round(value, 2);   // DB: DECIMAL(9,2), phải lớn hơn 0 hoặc để trống
    }

    /// <summary>Cột TIỀN: đơn giá công đoạn (đồng/sản phẩm). Trống hoặc 0 là chưa có đơn giá. Làm tròn 2 số như cột DB.</summary>
    private static decimal? ReadRate(IXLWorksheet sheet, int row, Layout layout, ref string? error)
    {
        if (layout.RateCol == 0) return null;
        if (!TryReadNumber(sheet.Cell(row, layout.RateCol), out var value, out var problem))
        {
            error ??= $"Cột TIỀN {problem}.";
            return null;
        }
        if (value < 0)
        {
            error ??= "TIỀN không được âm.";
            return null;
        }
        var rounded = Math.Round(value, 2);
        return rounded == 0 ? null : rounded;
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

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
            error = "không đọc được giá trị";
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
}
