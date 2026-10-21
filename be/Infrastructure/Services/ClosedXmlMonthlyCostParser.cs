using System.Globalization;
using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Đọc file Excel chi phí tháng bằng ClosedXML (DEV-MI-12-3 — mẫu file tự chọn, SRS chỉ nói "tải file
/// mẫu", không nêu cột cụ thể). Định dạng: dòng đầu là tiêu đề, 6 cột theo thứ tự
///   Loại chi phí | Kiểu | Mã hàng | Số tiền | Số lượng | Ghi chú
/// Mỗi dòng là một khoản chi phí (MonthlyCost); Mã hàng chỉ bắt buộc khi Kiểu = VARIABLE.
/// </summary>
public sealed class ClosedXmlMonthlyCostParser : IExcelMonthlyCostParser
{
    private static readonly string[] Headers =
        { "Loại chi phí", "Kiểu", "Mã hàng", "Số tiền", "Số lượng", "Ghi chú" };
    private static readonly string[] HeadersEn =
        { "CostCategoryCode", "CostType", "ProductCode", "Amount", "Quantity", "Note" };

    public IReadOnlyList<MonthlyCostExcelRowDto> Parse(Stream stream)
    {
        var rows = new List<MonthlyCostExcelRowDto>();

        // IFormFile.OpenReadStream() không seek được; ClosedXML cần stream seek để đọc package.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        using var workbook = XlsxWorkbookLoader.Open(buffer);
        var sheet = workbook.Worksheets.Count == 0
            ? throw new InvalidDataException("File Excel không có sheet nào.")
            : workbook.Worksheets.First();

        var used = sheet.RangeUsed();
        if (used is null) return rows;

        var headerRow = used.FirstRow();
        if (headerRow is null || !HasValidHeader(headerRow))
            throw new InvalidDataException(
                "File Excel sai cột: cần 'Loại chi phí, Kiểu, Mã hàng, Số tiền, Số lượng, Ghi chú'.");

        foreach (var row in used.RowsUsed().Skip(1))
        {
            var categoryCell = row.Cell(1);
            var typeCell = row.Cell(2);
            var productCodeCell = row.Cell(3);
            var amountCell = row.Cell(4);
            var quantityCell = row.Cell(5);
            var noteCell = row.Cell(6);

            if (categoryCell.IsEmpty() && typeCell.IsEmpty() && productCodeCell.IsEmpty()
                && amountCell.IsEmpty() && quantityCell.IsEmpty() && noteCell.IsEmpty())
                continue;   // bỏ dòng trống hoàn toàn (file có thể thừa dòng cuối)

            rows.Add(ParseRow(row.RowNumber(), categoryCell, typeCell, productCodeCell, amountCell, quantityCell,
                noteCell));
        }

        return rows;
    }

    private static MonthlyCostExcelRowDto ParseRow(int rowNumber, IXLCell categoryCell, IXLCell typeCell,
        IXLCell productCodeCell, IXLCell amountCell, IXLCell quantityCell, IXLCell noteCell)
    {
        var category = Text(categoryCell);
        var costType = Text(typeCell);
        var productCode = Text(productCodeCell);
        var note = Text(noteCell);

        decimal? amount = null;
        if (!amountCell.IsEmpty())
        {
            if (!TryParseDecimal(amountCell, out var a))
                return Error(rowNumber, $"Dòng {rowNumber}: cột Số tiền không phải số.", category, costType,
                    productCode);
            amount = a;
        }

        int? quantity = null;
        if (!quantityCell.IsEmpty())
        {
            if (!TryParseInt(quantityCell, out var q))
                return Error(rowNumber, $"Dòng {rowNumber}: cột Số lượng không phải số nguyên.", category, costType,
                    productCode, amount);
            quantity = q;
        }

        return new MonthlyCostExcelRowDto(rowNumber, category, costType, productCode, amount, quantity, note, null);
    }

    private static MonthlyCostExcelRowDto Error(int rowNumber, string message, string? category = null,
        string? costType = null, string? productCode = null, decimal? amount = null) =>
        new(rowNumber, category, costType, productCode, amount, null, null, message);

    private static bool HasValidHeader(IXLRangeRow headerRow)
    {
        var cells = headerRow.Cells(1, 6).Select(c => Normalize(c.GetString())).ToArray();
        return cells.SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase)
            || cells.SequenceEqual(HeadersEn, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    private static string? Text(IXLCell cell) => cell.IsEmpty() ? null : cell.GetString().Trim();

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

    private static bool TryParseInt(IXLCell cell, out int value)
    {
        value = 0;
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double d))
        {
            value = (int)d;
            return true;
        }
        return int.TryParse(cell.GetString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
