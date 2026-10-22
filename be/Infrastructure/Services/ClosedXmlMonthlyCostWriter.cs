using ClosedXML.Excel;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Dựng file Excel mẫu "chi phí tháng" (UC12) bằng ClosedXML: sheet nhập chỉ có tiêu đề, sheet HuongDan có 2 dòng ví dụ (một khoản FIXED,
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
        sheet.Columns().AdjustToContents();

        // Ví dụ nằm ở sheet riêng: parser chỉ đọc sheet đầu, nên dòng ví dụ không bao giờ bị nhập nhầm thành khoản chi 0 đồng.
        var guide = workbook.Worksheets.Add("HuongDan");
        for (var col = 0; col < Headers.Length; col++)
            guide.Cell(1, col + 1).Value = Headers[col];
        guide.Row(1).Style.Font.Bold = true;

        // Ví dụ 1: khoản cố định (FIXED) — không điền Mã hàng (UC30: hóa đơn bếp, đối soát Σ DailyMeal).
        guide.Cell(2, 1).Value = MonthlyCostCategory.Meals;
        guide.Cell(2, 2).Value = MonthlyCostType.Fixed;
        guide.Cell(2, 4).Value = 18_000_000;
        guide.Cell(2, 6).Value = "VD: hóa đơn bếp cuối tháng, đối soát với tổng suất ăn trong hệ thống";

        // Ví dụ 2: khoản biến đổi (VARIABLE) — bắt buộc Mã hàng đúng ProductCode đã có trong hệ thống.
        guide.Cell(3, 1).Value = MonthlyCostCategory.MaterialsBought;
        guide.Cell(3, 2).Value = MonthlyCostType.Variable;
        guide.Cell(3, 3).Value = "VD: SP001";
        guide.Cell(3, 4).Value = 8_000_000;
        guide.Cell(3, 6).Value = "VD: NVL mua ngoài cho mã hàng này";
        guide.Cell(5, 1).Value = "Chỉ điền dữ liệu ở sheet ChiPhiThang. Sheet này chỉ để tham khảo cách điền.";
        guide.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
