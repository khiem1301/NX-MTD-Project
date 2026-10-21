using ClosedXML.Excel;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Dựng file Excel mẫu "chi phí tháng" (UC12) bằng ClosedXML: tiêu đề + 2 dòng ví dụ (một khoản FIXED,
/// một khoản VARIABLE) để Kế toán biết cách điền, khớp cột của ClosedXmlMonthlyCostParser.
/// </summary>
public sealed class ClosedXmlMonthlyCostWriter : IMonthlyCostExcelWriter
{
    private static readonly string[] Headers =
        { "Loại chi phí", "Kiểu", "Mã hàng", "Số tiền", "Số lượng", "Ghi chú" };

    public byte[] WriteTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ChiPhiThang");

        for (var col = 0; col < Headers.Length; col++)
            sheet.Cell(1, col + 1).Value = Headers[col];
        sheet.Row(1).Style.Font.Bold = true;

        // Ví dụ 1: khoản cố định (FIXED) — không điền Mã hàng (UC30: hóa đơn bếp, đối soát Σ DailyMeal).
        sheet.Cell(2, 1).Value = MonthlyCostCategory.Meals;
        sheet.Cell(2, 2).Value = MonthlyCostType.Fixed;
        sheet.Cell(2, 4).Value = 0;
        sheet.Cell(2, 6).Value = "VD: hóa đơn bếp cuối tháng, đối soát với tổng suất ăn trong hệ thống";

        // Ví dụ 2: khoản biến đổi (VARIABLE) — bắt buộc Mã hàng đúng ProductCode đã có trong hệ thống.
        sheet.Cell(3, 1).Value = MonthlyCostCategory.MaterialsBought;
        sheet.Cell(3, 2).Value = MonthlyCostType.Variable;
        sheet.Cell(3, 3).Value = "VD: SP001";
        sheet.Cell(3, 4).Value = 0;
        sheet.Cell(3, 6).Value = "VD: NVL mua ngoài cho mã hàng này";

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
