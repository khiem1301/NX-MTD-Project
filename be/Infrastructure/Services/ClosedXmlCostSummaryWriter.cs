using ClosedXML.Excel;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>Dựng file Excel "Tổng hợp chi phí" (UC12, MI-19) bằng ClosedXML, cùng kiểu
/// ClosedXmlFinanceWriter/ClosedXmlMonthlyCostWriter (MSG27): một khối chi phí cố định theo khoản, một
/// khối chi phí biến đổi theo khoản + mã hàng, rồi dòng tổng (chi phí, lương, tổng cộng).</summary>
public sealed class ClosedXmlCostSummaryWriter : ICostSummaryExcelWriter
{
    public byte[] WriteCostSummarySheet(CostSummaryDto dto)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("TongHopChiPhi");

        sheet.Cell(1, 1).Value = $"Tổng hợp chi phí tháng {dto.PeriodMonth}";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 3;
        sheet.Cell(row, 1).Value = "Chi phí cố định";
        sheet.Row(row).Style.Font.Bold = true;
        row++;
        sheet.Cell(row, 1).Value = "Loại chi phí";
        sheet.Cell(row, 2).Value = "Số tiền";
        sheet.Row(row).Style.Font.Bold = true;
        row++;
        foreach (var item in dto.FixedCosts)
        {
            sheet.Cell(row, 1).Value = item.CostCategoryCode;
            sheet.Cell(row, 2).Value = item.Amount;
            row++;
        }
        sheet.Cell(row, 1).Value = "Tổng chi phí cố định";
        sheet.Cell(row, 2).Value = dto.FixedCostTotal;
        sheet.Row(row).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "Chi phí biến đổi theo mã hàng";
        sheet.Row(row).Style.Font.Bold = true;
        row++;
        sheet.Cell(row, 1).Value = "Mã hàng";
        sheet.Cell(row, 2).Value = "Tên hàng";
        sheet.Cell(row, 3).Value = "Số tiền";
        sheet.Row(row).Style.Font.Bold = true;
        row++;
        foreach (var item in dto.VariableCostsByProduct)
        {
            sheet.Cell(row, 1).Value = item.ProductCode;
            sheet.Cell(row, 2).Value = item.ProductName;
            sheet.Cell(row, 3).Value = item.Amount;
            row++;
        }
        sheet.Cell(row, 1).Value = "Tổng chi phí biến đổi";
        sheet.Cell(row, 3).Value = dto.VariableCostTotal;
        sheet.Row(row).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "Tổng chi phí (MonthlyCost)";
        sheet.Cell(row, 2).Value = dto.MonthlyCostTotal;
        row++;
        sheet.Cell(row, 1).Value = "Lương (bảng lương đã duyệt)";
        if (dto.PayrollApproved) sheet.Cell(row, 2).Value = dto.PayrollTotal!.Value;
        else sheet.Cell(row, 2).Value = "Chưa có bảng lương đã duyệt";
        row++;
        sheet.Cell(row, 1).Value = "Tổng cộng";
        sheet.Cell(row, 2).Value = dto.GrandTotal;
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
