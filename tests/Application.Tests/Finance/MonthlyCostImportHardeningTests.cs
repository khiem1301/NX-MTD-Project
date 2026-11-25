using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-187: parser chi phí tháng nhận cột theo tên, báo rõ file sai mẫu; kiểm từng ô và nhập từ bảng đã sửa.</summary>
[Collection(ExcelTestCollection.Name)]
public class MonthlyCostImportHardeningTests
{
    private static readonly string[] Header = ["Loại chi phí", "Kiểu", "Mã hàng", "Số tiền", "Số lượng", "Ghi chú"];

    private static MemoryStream Book(Action<XLWorkbook> build)
    {
        using var wb = new XLWorkbook();
        build(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void Write(IXLWorksheet ws, int headerRow, string[] header, params object?[][] rows)
    {
        for (var c = 0; c < header.Length; c++) ws.Cell(headerRow, c + 1).Value = header[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } v) ws.Cell(headerRow + 1 + r, c + 1).Value = XLCellValue.FromObject(v);
    }

    private static IReadOnlyList<MonthlyCostExcelRowDto> Parse(Stream stream) => new ClosedXmlMonthlyCostParser().Parse(stream);

    [Fact]
    public void Header_in_any_order_and_below_the_first_row_is_found_by_name()
    {
        using var file = Book(wb =>
        {
            var ws = wb.AddWorksheet("Bất kỳ");
            ws.Cell(1, 1).Value = "BẢNG CHI PHÍ THÁNG 10";
            Write(ws, 3, ["số tiền", "LOẠI CHI PHI", "Kiểu"], [18_000_000, "UTILITIES", "FIXED"]);
        });

        var row = Parse(file).Single();

        (row.RowNumber, row.CostCategoryCode, row.CostType, row.Amount).Should().Be((4, "UTILITIES", "FIXED", 18_000_000m));
    }

    [Fact]
    public void Hidden_first_sheet_is_skipped()
    {
        using var file = Book(wb =>
        {
            var hidden = wb.AddWorksheet("Nháp cũ");
            Write(hidden, 1, Header, ["RAC", "FIXED", null, 1]);
            hidden.Visibility = XLWorksheetVisibility.Hidden;
            Write(wb.AddWorksheet("Thật"), 1, Header, ["FUEL", "FIXED", null, 500_000]);
        });

        Parse(file).Single().CostCategoryCode.Should().Be("FUEL");
    }

    [Fact]
    public void Missing_required_columns_names_the_missing_ones()
    {
        using var file = Book(wb => Write(wb.AddWorksheet("S"), 1, ["Loại chi phí", "Ghi chú"], ["FUEL", "x"]));

        var act = () => Parse(file);

        act.Should().Throw<InvalidDataException>().Which.Message.Should().Contain("thiếu cột Kiểu, Số tiền");
    }

    [Fact]
    public void A_sheet_without_any_cost_columns_is_reported_as_wrong_template()
    {
        using var file = Book(wb => Write(wb.AddWorksheet("S"), 1, ["Tên", "Tuổi"], ["An", 20]));

        var act = () => Parse(file);

        act.Should().Throw<InvalidDataException>().Which.Message.Should().Contain("sai mẫu");
    }

    [Theory]
    [InlineData(0)]            // file rỗng
    [InlineData(1)]            // văn bản / csv đổi đuôi
    public void Empty_or_non_excel_content_gets_the_friendly_message(int kind)
    {
        var bytes = kind == 0 ? [] : "Loại chi phí,Kiểu,Số tiền\r\nFUEL,FIXED,1000"u8.ToArray();

        var act = () => Parse(new MemoryStream(bytes));

        act.Should().Throw<InvalidDataException>().Which.Message.Should().Contain(".xlsx");
    }

    [Fact]
    public void Too_many_rows_is_refused_with_the_limit()
    {
        using var file = Book(wb =>
        {
            var ws = wb.AddWorksheet("S");
            Write(ws, 1, Header);
            for (var r = 2; r <= ClosedXmlMonthlyCostParser.MaxRows + 2; r++)
            {
                ws.Cell(r, 1).Value = "OTHER";
                ws.Cell(r, 2).Value = "FIXED";
                ws.Cell(r, 4).Value = 1000;
            }
        });

        var act = () => Parse(file);

        act.Should().Throw<InvalidDataException>().Which.Message.Should().Contain("500");
    }

    [Fact]
    public void Fractional_quantity_and_text_amount_are_cell_errors_not_silently_cut()
    {
        using var file = Book(wb => Write(wb.AddWorksheet("S"), 1, Header,
            ["OTHER", "FIXED", null, 1000, 2.5],
            ["OTHER", "FIXED", null, "abc"]));

        var rows = Parse(file);

        rows[0].ParseError.Should().Contain("Số lượng");
        rows[1].ParseError.Should().Contain("Số tiền");
    }

    // ---------- Kiểm từng ô ở máy chủ + nhập từ bảng đã sửa ----------

    private readonly IMonthlyCostRepository _repo = Substitute.For<IMonthlyCostRepository>();

    private MonthlyCostService Sut()
    {
        _repo.GetProductsByCodesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["SP001"] = 1 });
        return new MonthlyCostService(_repo, Substitute.For<IExcelMonthlyCostParser>(), Substitute.For<IMonthlyCostExcelWriter>(),
            Substitute.For<IAuditLogService>(), Substitute.For<IClock>(), NullLogger<MonthlyCostService>.Instance);
    }

    private static MonthlyCostExcelRowDto Row(decimal? amount, int? qty = null, string? note = null, string? product = null, string type = "FIXED") =>
        new(2, "OTHER", type, product, amount, qty, note, null);

    [Theory]
    [InlineData(200_000_000_000, null, null, "tối đa 100.000.000.000")]
    [InlineData(1000.123, null, null, "2 số lẻ")]
    [InlineData(1000, -1, null, "không được âm")]
    [InlineData(1000, 2_000_000, null, "tối đa 1.000.000")]
    public async Task Preview_rows_flags_out_of_range_cells(double amount, int? qty, string? note, string expected)
    {
        var result = await Sut().PreviewRowsAsync(new MonthlyCostRowsDto("2026-10", [Row((decimal)amount, qty, note)]), default);

        result.Data!.Rows.Single().ErrorMessage.Should().Contain(expected);
    }

    [Fact]
    public async Task Preview_rows_flags_a_note_longer_than_the_column()
    {
        var result = await Sut().PreviewRowsAsync(new MonthlyCostRowsDto("2026-10", [Row(1000, note: new string('x', 301))]), default);

        result.Data!.Rows.Single().ErrorMessage.Should().Contain("300");
    }

    [Fact]
    public async Task Preview_rows_accepts_an_empty_table_and_rejects_a_bad_month_or_too_many_rows()
    {
        (await Sut().PreviewRowsAsync(new MonthlyCostRowsDto("2026-10", []), default)).Data!.ValidCount.Should().Be(0);
        (await Sut().PreviewRowsAsync(new MonthlyCostRowsDto("2026-13", []), default)).ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodMonthInvalid);
        var many = Enumerable.Range(1, 501).Select(_ => Row(1000)).ToList();
        (await Sut().PreviewRowsAsync(new MonthlyCostRowsDto("2026-10", many), default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Import_rows_with_no_valid_row_is_refused_and_a_locked_month_is_blocked()
    {
        (await Sut().ImportRowsAsync(new MonthlyCostRowsDto("2026-10", [Row(-5)]), default)).ErrorCode
            .Should().Be(ErrorCodes.MonthlyCostExcelEmpty);

        _repo.GetOrCreatePeriodAsync("2026-10", Arg.Any<CancellationToken>())
            .Returns(new AccountingPeriod { PeriodId = 1, PeriodMonth = "2026-10", Status = RecordStatus.Locked });
        (await Sut().ImportRowsAsync(new MonthlyCostRowsDto("2026-10", [Row(1000)]), default)).ErrorCode
            .Should().Be(ErrorCodes.MonthlyCostPeriodLocked);
    }
}
