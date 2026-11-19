using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Excel;

/// <summary>
/// Ghi file mẫu nhập Excel (ClosedXML). Dòng dữ liệu thật: chữ thường. Dòng ví dụ (bảng chưa có dữ liệu): chữ xám nghiêng.
/// Màu tiêu đề: xanh đậm = cột bắt buộc, xanh nhạt = tùy chọn, xanh lá = cột size / tùy chọn mới.
/// </summary>
public sealed class ClosedXmlImportTemplateWriter : IImportTemplateWriter
{
    private static readonly XLColor Dark = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor Light = XLColor.FromHtml("#DDEBF7");
    private static readonly XLColor Green = XLColor.FromHtml("#E2EFDA");
    private static readonly XLColor DemoText = XLColor.FromHtml("#8C8C8C");

    public byte[] Write(ImportTemplateModel model)
    {
        using var wb = new XLWorkbook();
        WriteGuide(wb, model);
        foreach (var table in model.Tables) WriteTable(wb, table);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteGuide(XLWorkbook wb, ImportTemplateModel model)
    {
        var ws = wb.AddWorksheet("Hướng dẫn");
        ws.Cell(1, 1).Value = model.GuideTitle;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        var r = 3;
        foreach (var row in model.Guide)
        {
            ws.Cell(r, 1).Value = row.Key;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Cell(r, 2).Value = row.Value;
            ws.Cell(r, 2).Style.Alignment.WrapText = true;
            ws.Cell(r, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            r++;
        }
        ws.Column(1).Width = 26;
        ws.Column(2).Width = 110;
    }

    private static void WriteTable(XLWorkbook wb, TemplateTable table)
    {
        var ws = wb.AddWorksheet(table.Sheet);
        ws.Cell(1, 1).Value = table.Title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 12;

        var note = ws.Range(2, 1, 2, Math.Max(table.Columns.Count, 4)).Merge();
        note.Value = table.Note;
        note.Style.Alignment.WrapText = true;
        note.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        note.Style.Font.FontColor = XLColor.FromHtml("#595959");
        ws.Row(2).Height = 78;

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var col = table.Columns[c];
            var cell = ws.Cell(3, c + 1);
            cell.Value = col.Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = col.Kind switch { 'R' => Dark, 'O' => Light, _ => Green };
            cell.Style.Font.FontColor = col.Kind == 'R' ? XLColor.White : XLColor.Black;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Column(c + 1).Width = col.Width;
        }

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var values = table.Rows[i];
            for (var c = 0; c < values.Count; c++)
            {
                var cell = ws.Cell(4 + i, c + 1);
                switch (values[c])
                {
                    case null: continue;
                    case int n: cell.Value = n; break;
                    case double d: cell.Value = d; break;
                    case decimal m: cell.Value = m; break;
                    default: cell.Value = values[c]!.ToString(); break;
                }
                if (table.IsDemo)
                {
                    cell.Style.Font.FontColor = DemoText;
                    cell.Style.Font.Italic = true;
                }
            }
        }
        ws.SheetView.FreezeRows(3);
    }
}
