using ClosedXML.Excel;
using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-118: xem trước dòng hàng của đơn từ Excel dạng ma trận màu × size.</summary>
[Collection(ExcelTestCollection.Name)]
public class OrderImportTests : ProductionTestBase
{
    public OrderImportTests()
    {
        SetupQueryable(new List<Product>
        {
            new() { ProductId = 1, ProductCode = "AO01", ProductName = "Áo", Status = RecordStatus.Active },
            new() { ProductId = 2, ProductCode = "CU02", ProductName = "Cũ", Status = RecordStatus.Discontinued },
        });
    }

    private OrderImportService Sut() => new(Uow, new ClosedXmlOrderLinesParser());

    private static MemoryStream Book(params object?[][] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Đơn hàng");
        ws.Cell(1, 1).Value = "Ghi chú";
        string[] headers = ["Mã hàng", "Màu", "Đơn giá CMT", "S", "M", "L"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = ws.Cell(4 + r, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case double d: cell.Value = d; break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Preview_ValidMatrix_ReadsSizesAndSkipsEmptyCells()
    {
        using var stream = Book(["ao01", "Đen", 25000.0, 100, null, 50], ["AO01", "Vàng", 25000.0, 10, 20, 0]);

        var data = (await Sut().PreviewAsync(stream, default)).Data!;

        data.SizeColumns.Should().Equal("S", "M", "L");
        data.ValidCount.Should().Be(2);
        data.Rows[0].ProductId.Should().Be(1);
        data.Rows[0].Quantities.Select(q => $"{q.SizeCode}:{q.Qty}").Should().Equal("S:100", "L:50");
        data.Rows[1].Color.Should().Be("Vàng");   // màu mới vẫn hợp lệ
    }

    [Fact]
    public async Task Preview_FlagsUnknownInactivePriceMismatchNoQtyAndDuplicate()
    {
        using var stream = Book(
            ["XX99", "Đen", 1000.0, 1, null, null],
            ["CU02", "Đen", 1000.0, 1, null, null],
            ["AO01", "Đen", 25000.0, 1, null, null],
            ["AO01", "Xanh", 26000.0, 1, null, null],
            ["AO01", "Hồng", 25000.0, null, null, null],
            ["AO01", "đen", 25000.0, 2, null, null],
            ["AO01", "Trắng", 25000.0, 1.5, null, null]);

        var data = (await Sut().PreviewAsync(stream, default)).Data!;

        data.Rows.Select(r => r.Error is null).Should().Equal(false, false, true, false, false, false, false);
        data.Rows[0].Error.Should().Be(string.Format(Messages.OrderImportProductUnknown, "XX99"));
        data.Rows[1].Error.Should().Be(string.Format(Messages.OrderImportProductInactive, "CU02"));
        data.Rows[3].Error.Should().Be(string.Format(Messages.OrderImportPriceMismatch, "AO01"));
        data.Rows[4].Error.Should().Be(Messages.OrderImportNoQty);
        data.Rows[5].Error.Should().Contain("dòng 6");
        data.Rows[6].Error.Should().Contain("số nguyên dương");
        data.DuplicateCount.Should().Be(1);
        data.ErrorCount.Should().Be(5);
    }

    [Fact]
    public async Task Preview_WrongTemplate_FailsValidation()
    {
        var wb = new XLWorkbook();
        wb.Worksheets.Add("x").Cell(1, 1).Value = "Không có gì";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var result = await Sut().PreviewAsync(ms, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OrderImportInvalid);
    }
}
