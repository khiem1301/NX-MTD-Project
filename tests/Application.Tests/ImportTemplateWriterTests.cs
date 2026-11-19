using ClosedXML.Excel;
using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

public class ImportTemplateWriterTests
{
    [Fact]
    public void Write_ColumnWithOptions_CreatesQuotedDropdownListOnDataRows()
    {
        var table = new TemplateTable("Bìa", "T", "N", [new("Mã", 'R', 10), new("Trạng thái", 'O', 14, ["Đang dùng", "Ngừng dùng"])],
            [["x", "Đang dùng"]], true);

        var bytes = new ClosedXmlImportTemplateWriter().Write(new ImportTemplateModel("f.xlsx", "G", [], [table]));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var validation = wb.Worksheet("Bìa").Cell(4, 2).GetDataValidation();
        validation.AllowedValues.Should().Be(XLAllowedValues.List);
        validation.InCellDropdown.Should().BeTrue();
        validation.MinValue.Should().Be("\"Đang dùng,Ngừng dùng\"");   // thiếu dấu ngoặc kép thì Excel không hiện danh sách
        wb.Worksheet("Bìa").Cell(4, 1).GetDataValidation().AllowedValues.Should().NotBe(XLAllowedValues.List);
    }
}
