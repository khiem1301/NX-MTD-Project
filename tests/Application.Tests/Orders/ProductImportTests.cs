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
    private readonly List<Product> _products = [new() { ProductId = 1, ProductCode = "AO01", Status = RecordStatus.Active }];

    public ProductImportTests()
    {
        SetupQueryable(_products);
    }

    private ProductImportService Sut() => new(Uow);

    private static MemoryStream Book(params string?[][] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Mã hàng");
        string[] headers = ["Mã hàng", "Mô tả"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } v) ws.Cell(4 + r, c + 1).Value = v;
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Theory]
    [InlineData("ao01", "Exists")]   // đã có
    [InlineData("QU02", "New")]      // mới
    [InlineData("MA HANG", "Error")] // mã sai ký tự
    public async Task Preview_SingleRow_ClassifiesNewExistsAndErrors(string code, string expected)
    {
        using var stream = Book([code, null]);

        var rows = new ClosedXmlProductParser().Parse(stream);
        var data = (await Sut().PreviewAsync(rows, default)).Data!;

        data.Rows.Single().Action.Should().Be(expected);
    }

    [Fact]
    public async Task Preview_SecondRowOnward_ErrorSingleProductOnly()
    {
        using var stream = Book(
            ["QU02", "Vải kaki"],   // mã hàng đang nhập
            ["QU03", null],         // hợp lệ nhưng là mã thứ hai
            ["MA HANG", null]);     // lỗi riêng của dòng vẫn được báo trước

        var rows = new ClosedXmlProductParser().Parse(stream);
        var data = (await Sut().PreviewAsync(rows, default)).Data!;

        data.Rows.Select(r => r.Action).Should().Equal("New", "Error", "Error");
        data.Rows[1].Error.Should().Be(NX_MTD.Application.Common.Messages.ProductImportSingleOnly);
        data.Rows[2].Error.Should().NotBe(NX_MTD.Application.Common.Messages.ProductImportSingleOnly);
        data.NewCount.Should().Be(1);
    }
}
