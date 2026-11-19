using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>Kiểm các dòng Mã hàng của file Excel nhiều sheet (popup kiểm tra form mã hàng): mã đã có thì bỏ qua.</summary>
[Collection(ExcelTestCollection.Name)]
public class ProductImportTests : ProductionTestBase
{
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "AO01", ProductName = "Áo", Status = RecordStatus.Active }];

    public ProductImportTests()
    {
        SetupQueryable(_products);
    }

    private ProductImportService Sut() => new(Uow);

    private static MemoryStream Book(params string?[][] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Mã hàng");
        string[] headers = ["Mã hàng", "Tên mã hàng", "Mô tả"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } v) ws.Cell(4 + r, c + 1).Value = v;
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Preview_ClassifiesNewExistsAndErrors()
    {
        using var stream = Book(
            ["ao01", "Áo cũ", null],          // đã có
            ["QU02", "Quần âu", "Vải kaki"],  // mới
            ["qu02", "Quần trùng", null],     // trùng dòng 5
            ["MA HANG", "Có dấu cách", null], // mã sai ký tự
            ["SM03", null, null]);            // thiếu tên

        var rows = new ClosedXmlProductParser().Parse(stream);
        var data = (await Sut().PreviewAsync(rows, default)).Data!;

        data.Rows.Select(r => r.Action).Should().Equal("Exists", "New", "Error", "Error", "Error");
        data.Rows[2].Error.Should().Contain("dòng 5");
        data.NewCount.Should().Be(1);
        data.ExistsCount.Should().Be(1);
        data.DuplicateCount.Should().Be(1);
        data.ErrorCount.Should().Be(2);
    }
}
