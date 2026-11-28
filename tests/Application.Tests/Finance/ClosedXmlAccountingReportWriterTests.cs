using ClosedXML.Excel;
using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-200: sheet công của báo cáo kế toán chia mỗi tổ một sheet, sản lượng chia theo công đoạn × đơn giá.</summary>
public class ClosedXmlAccountingReportWriterTests
{
    private static WorkdayOvertimeRowDto Row(int id, string code, string? team, params WorkerOperationOutputDto[] ops) =>
        new(id, code, $"Tên {code}", team, "A", 20m, 2m, 22m, 1, ops);

    private static XLWorkbook Write(params WorkdayOvertimeRowDto[] rows)
    {
        var report = new WorkdayOvertimeReportDto("2026-11", rows, 0m, 0m, 0m, rows.Sum(r => r.OutputQty), rows.Sum(r => r.OutputAmount), 0);
        var bytes = new ClosedXmlAccountingReportWriter().WriteReportWorkbook(
            report, new MealReportDto("2026-11", 0, 0, 0m, null, false), new VehicleReportDto("2026-11", [], 0, 0m, 0m, 0, 0));
        return new XLWorkbook(new MemoryStream(bytes));
    }

    [Fact]
    public void WriteReportWorkbook_EachTeamGetsItsOwnSheetAndNoTeamGoesLast()
    {
        using var book = Write(Row(1, "NV1", "MAY2"), Row(2, "NV2", "MAY1"), Row(3, "NV3", null));

        book.Worksheets.Select(s => s.Name).Should().StartWith(["Cong MAY1", "Cong MAY2", "Cong KhongTo"]);
        book.Worksheet("Cong MAY1").Cell(3, 1).GetString().Should().Be("NV2");
        book.Worksheet("Cong MAY1").Cell(4, 1).GetString().Should().Be("Tổng cộng");
    }

    [Fact]
    public void WriteReportWorkbook_OneLinePerOperationAndPriceWithAmountEqualQtyTimesRate()
    {
        using var book = Write(Row(1, "NV1", "MAY1",
            new WorkerOperationOutputDto("CD01", "May vai", 1000m, 80, 80_000m),
            new WorkerOperationOutputDto("CD01", "May vai", 1200m, 40, 48_000m)));

        var sheet = book.Worksheet("Cong MAY1");
        sheet.Cell(3, 8).GetString().Should().Be("CD01 - May vai");
        (sheet.Cell(3, 9).GetValue<int>(), sheet.Cell(3, 10).GetValue<decimal>(), sheet.Cell(3, 11).GetValue<decimal>())
            .Should().Be((80, 1000m, 80_000m));
        (sheet.Cell(4, 10).GetValue<decimal>(), sheet.Cell(4, 11).GetValue<decimal>()).Should().Be((1200m, 48_000m));
        // Thông tin nhân viên gộp ô qua các dòng công đoạn; tổng tiền của nhân viên ở cột cuối.
        sheet.Cell(3, 6).GetValue<decimal>().Should().Be(22m);
        sheet.Cell(4, 1).IsMerged().Should().BeTrue();
        sheet.Cell(3, 1).MergedRange().RangeAddress.RowSpan.Should().Be(2);
        sheet.Cell(3, 12).GetValue<decimal>().Should().Be(128_000m);
        sheet.Cell(5, 11).GetValue<decimal>().Should().Be(128_000m);
        sheet.Cell(5, 12).GetValue<decimal>().Should().Be(128_000m);
        sheet.Cell(3, 11).Style.NumberFormat.Format.Should().Be("#,##0");
    }

    [Fact]
    public void WriteReportWorkbook_TablesHaveBlackThinBordersOnEveryEdge()
    {
        using var book = Write(Row(1, "NV1", "MAY1", new WorkerOperationOutputDto("CD01", "May vai", 1000m, 80, 80_000m)));

        foreach (var cell in new[] { book.Worksheet("Cong MAY1").Cell(2, 1), book.Worksheet("Cong MAY1").Cell(3, 12), book.Worksheet("Cong MAY1").Cell(4, 6) })
        {
            var border = cell.Style.Border;
            new[] { border.TopBorder, border.BottomBorder, border.LeftBorder, border.RightBorder }.Should().OnlyContain(b => b == XLBorderStyleValues.Thin);
            new[] { border.TopBorderColor, border.BottomBorderColor, border.LeftBorderColor, border.RightBorderColor }.Should().OnlyContain(c => c == XLColor.Black);
        }
        book.Worksheet("Cong MAY1").Cell(1, 1).Style.Border.BottomBorder.Should().Be(XLBorderStyleValues.None);
    }

    [Fact]
    public void WriteReportWorkbook_NoRows_StillWritesAnEmptyWorkSheet()
    {
        using var book = Write();

        book.Worksheets.Select(s => s.Name).Should().Contain("Cong KhongTo");
    }
}
