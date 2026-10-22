using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Mở file .xlsx của người dùng để đọc SỐ LIỆU (các importer không tính lại công thức, GBR-12). File xuất từ Excel/WPS
/// của xưởng gây hai lỗi cho ClosedXML nên gói được làm sạch trước khi nạp:
/// <list type="bullet">
/// <item>Logo/ghi chú trỏ tới ảnh hỏng (<c>Target="NULL"</c>) làm ClosedXML ném <see cref="ArgumentOutOfRangeException"/>
/// ở bước nạp hình → bỏ hình, ghi chú khỏi sheet.</item>
/// <item>Công thức tham chiếu file khác (<c>[1]Sheet!A1</c>) làm ClosedXML ném <see cref="NotImplementedException"/> khi
/// đọc ô vì nó cố tính lại → bỏ công thức, giữ giá trị đã tính sẵn (<c>&lt;v&gt;</c>) mà Excel lưu cùng ô.</item>
/// </list>
/// Ô công thức chưa từng được Excel tính (không có giá trị lưu sẵn) sẽ đọc ra trống.
/// </summary>
internal static partial class XlsxWorkbookLoader
{
    public static XLWorkbook Open(MemoryStream buffer)
    {
        using var cleaned = Clean(buffer);
        return new XLWorkbook(cleaned);
    }

    private static MemoryStream Clean(MemoryStream source)
    {
        var copy = new MemoryStream();
        source.Position = 0;
        source.CopyTo(copy);

        using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var targets = zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                            && (e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                || e.FullName.EndsWith(".xml.rels", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var entry in targets)
            {
                string text;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    text = reader.ReadToEnd();

                var cleaned = entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
                    ? DrawingRelationship().Replace(text, string.Empty)
                    : Formula().Replace(FormulaSelfClosing().Replace(DrawingElement().Replace(text, string.Empty), string.Empty), string.Empty);
                if (cleaned == text) continue;

                var name = entry.FullName;
                entry.Delete();
                var rewritten = zip.CreateEntry(name);
                using var writer = new StreamWriter(rewritten.Open(), new UTF8Encoding(false));
                writer.Write(cleaned);
            }
        }

        copy.Position = 0;
        return copy;
    }

    [GeneratedRegex(@"<(?:drawing|legacyDrawing|legacyDrawingHF|picture)\b[^>]*/>", RegexOptions.IgnoreCase)]
    private static partial Regex DrawingElement();

    [GeneratedRegex(@"<Relationship\b[^>]*Type=""[^""]*/(?:drawing|vmlDrawing|image|comments)""[^>]*/>", RegexOptions.IgnoreCase)]
    private static partial Regex DrawingRelationship();

    [GeneratedRegex(@"<f\b[^>]*/>")]
    private static partial Regex FormulaSelfClosing();

    [GeneratedRegex(@"<f\b[^>]*>.*?</f>", RegexOptions.Singleline)]
    private static partial Regex Formula();
}
