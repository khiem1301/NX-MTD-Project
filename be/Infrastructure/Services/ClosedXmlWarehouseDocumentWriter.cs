using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// TH-16: dựng một trang chứng từ kho in được bằng ClosedXML — tiêu đề giữa trang, khối thông tin chung, bảng dòng hàng có viền,
/// hàng chữ ký cuối trang; khổ A4 dọc, co vừa chiều ngang.
/// </summary>
public sealed class ClosedXmlWarehouseDocumentWriter : IWarehouseDocumentExcelWriter
{
    public byte[] Write(PrintableDocument document)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ChungTu");
        var columns = Math.Max(document.Columns.Count, 2);

        var title = sheet.Range(1, 1, 1, columns).Merge();
        title.Value = document.Title;
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 16;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 3;
        foreach (var (label, value) in document.Info)
        {
            sheet.Cell(row, 1).Value = label + ":";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            var cell = sheet.Range(row, 2, row, columns).Merge();
            cell.Value = value;
            cell.Style.Alignment.WrapText = true;
            row++;
        }

        row++;
        var headerRow = row;
        for (var c = 0; c < document.Columns.Count; c++)
            sheet.Cell(headerRow, c + 1).Value = document.Columns[c];
        var header = sheet.Range(headerRow, 1, headerRow, document.Columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        foreach (var line in document.Rows)
        {
            row++;
            for (var c = 0; c < line.Count; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                switch (line[c])
                {
                    case null: break;
                    case decimal d:   // số nguyên không để dấu chấm thừa ("95.")
                        cell.Value = d;
                        cell.Style.NumberFormat.Format = d == decimal.Truncate(d) ? "#,##0" : "#,##0.###";
                        break;
                    case int n: cell.Value = n; break;
                    default: cell.Value = line[c]!.ToString(); break;
                }
            }
        }

        var table = sheet.Range(headerRow, 1, row, document.Columns.Count);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (document.Signers.Count > 0)
        {
            var signRow = row + 3;
            var span = Math.Max(1, columns / document.Signers.Count);
            for (var s = 0; s < document.Signers.Count; s++)
            {
                var start = s * span + 1;
                var end = s == document.Signers.Count - 1 ? columns : start + span - 1;
                var cell = end > start ? sheet.Range(signRow, start, signRow, end).Merge() : sheet.Range(signRow, start, signRow, start);
                cell.Value = document.Signers[s];
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                var hint = end > start ? sheet.Range(signRow + 1, start, signRow + 1, end).Merge() : sheet.Range(signRow + 1, start, signRow + 1, start);
                hint.Value = "(Ký, ghi rõ họ tên)";
                hint.Style.Font.Italic = true;
                hint.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        sheet.Columns().AdjustToContents(1, 30);
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 18);
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
