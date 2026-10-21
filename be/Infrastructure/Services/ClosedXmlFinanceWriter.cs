using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>Dựng file Excel "Doanh thu" (UC27) và "Hòa vốn + giá trị thặng dư" (UC11) bằng ClosedXML,
/// cùng kiểu ClosedXmlMonthlyCostWriter/ClosedXmlPayrollWriter (MSG27).</summary>
public sealed class ClosedXmlFinanceWriter : IFinanceExcelWriter
{
    private static readonly string[] RevenueHeaders =
        { "Tháng", "Mã đơn hàng", "Mã hàng", "Tên hàng", "SL hoàn thiện", "Doanh thu" };

    private static readonly string[] BreakEvenHeaders =
    {
        "Tháng", "Mã hàng", "Tên hàng", "Cách phân bổ định phí", "SL hoàn thiện", "Đơn giá CMT", "Doanh thu",
        "Biến phí", "Định phí phân bổ", "SL hòa vốn", "% đạt hòa vốn", "Giá trị thặng dư", "Nguy cơ lỗ", "Tính lúc",
    };

    public byte[] WriteRevenueSheet(IReadOnlyList<RevenueSummaryDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DoanhThu");

        WriteHeader(sheet, RevenueHeaders);

        var r = 2;
        foreach (var row in rows)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = row.PeriodMonth;
            sheet.Cell(r, c++).Value = row.OrderCode;
            sheet.Cell(r, c++).Value = row.ProductCode;
            sheet.Cell(r, c++).Value = row.ProductName;
            sheet.Cell(r, c++).Value = row.FinishedQty;
            sheet.Cell(r, c).Value = row.Revenue;
            r++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public byte[] WriteBreakEvenSheet(IReadOnlyList<BreakEvenResultDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("HoaVon");

        WriteHeader(sheet, BreakEvenHeaders);

        var r = 2;
        foreach (var row in rows)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = row.PeriodMonth;
            sheet.Cell(r, c++).Value = row.ProductCode;
            sheet.Cell(r, c++).Value = row.ProductName;
            sheet.Cell(r, c++).Value = row.AllocationBasis;
            sheet.Cell(r, c++).Value = row.FinishedQty;
            sheet.Cell(r, c++).Value = row.CmtUnitPrice;
            sheet.Cell(r, c++).Value = row.Revenue;
            sheet.Cell(r, c++).Value = row.VariableCost;
            sheet.Cell(r, c++).Value = row.AllocatedFixedCost;
            if (row.BreakEvenQty.HasValue) sheet.Cell(r, c).Value = row.BreakEvenQty.Value; else sheet.Cell(r, c).Value = "";
            c++;
            if (row.PercentReached.HasValue) sheet.Cell(r, c).Value = row.PercentReached.Value; else sheet.Cell(r, c).Value = "";
            c++;
            sheet.Cell(r, c++).Value = row.SurplusValue;
            sheet.Cell(r, c++).Value = row.IsAtRisk ? "Có" : "Không";
            sheet.Cell(r, c).Value = row.CalculatedAt;
            r++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(1, col + 1).Value = headers[col];
        sheet.Row(1).Style.Font.Bold = true;
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
