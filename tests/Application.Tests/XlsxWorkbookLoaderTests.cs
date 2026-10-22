using System.IO.Compression;
using System.Text;
using FluentAssertions;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>File .xlsx thật của xưởng có logo trỏ ảnh hỏng và công thức tham chiếu file khác; importer vẫn phải đọc được giá trị.</summary>
public class XlsxWorkbookLoaderTests
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Gói xlsx tối thiểu: 1 sheet lương, ô công thức trỏ file ngoài (có giá trị lưu sẵn) và thẻ drawing không có quan hệ.</summary>
    private static MemoryStream BuildBrokenWorkbook()
    {
        const string sheet = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="{Ns}" xmlns:r="{RelNs}"><sheetData>
              <row r="1"><c r="A1" t="inlineStr"><is><t>SLĐ</t></is></c><c r="B1" t="inlineStr"><is><t>Họ Tên</t></is></c><c r="C1" t="inlineStr"><is><t>Thực lĩnh</t></is></c></row>
              <row r="3"><c r="A3"><f>[1]Sheet1!A1</f><v>25001</v></c><c r="B3" t="inlineStr"><is><t>Nguyễn Văn A</t></is></c><c r="C3"><f>[1]Sheet1!B1*2</f><v>5000000</v></c></row>
            </sheetData><drawing r:id="rId9"/></worksheet>
            """;

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            Add("_rels/.rels", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="{RelNs}/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add("xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="{Ns}" xmlns:r="{RelNs}"><sheets><sheet name="Tổ 1" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Add("xl/_rels/workbook.xml.rels", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="{RelNs}/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            Add("xl/worksheets/sheet1.xml", sheet);
        }

        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Parsers_reject_a_non_excel_file_with_a_readable_message()
    {
        var garbage = () => new MemoryStream("đây không phải file excel"u8.ToArray());

        new Action(() => new ClosedXmlPayrollParser().Parse(garbage()))
            .Should().Throw<InvalidDataException>().WithMessage("*file Excel .xlsx hợp lệ*");
        new Action(() => new ClosedXmlOperationParser().Parse(garbage()))
            .Should().Throw<InvalidDataException>().WithMessage("*file Excel .xlsx hợp lệ*");
    }

    [Fact]
    public void Payroll_parser_reads_cached_values_when_file_has_external_formulas_and_broken_drawing()
    {
        var rows = new ClosedXmlPayrollParser().Parse(BuildBrokenWorkbook());

        var row = rows.Should().ContainSingle().Subject;
        row.ParseError.Should().BeNull();
        row.AttendanceCode.Should().Be("25001");
        row.FullName.Should().Be("Nguyễn Văn A");
        row.NetPay.Should().Be(5_000_000m);
    }
}
