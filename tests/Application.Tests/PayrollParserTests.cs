using ClosedXML.Excel;
using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>Parser file "Bảng thanh toán lương" thật (DEV-MI-09-4) và danh mục cột PayslipLayout.</summary>
[Collection(ExcelTestCollection.Name)]
public class PayrollParserTests
{
    private readonly ClosedXmlPayrollParser _sut = new();

    // Cột của sheet mẫu, mô phỏng sheet "Tổ may 1": tiêu đề 2 dòng (5 = nhóm, 6 = cột con), dòng 7 là số thứ tự cột.
    private static readonly (string Group, string Sub)[] Headers =
    [
        ("STT", ""), ("Họ Tên", ""), ("SLĐ", ""), ("LCB", ""),
        ("Ngày công, giờ công", "Ngày công SP"), ("", "Số giờ làm thêm ngày thường"),
        ("Phân loại", ""), ("Hệ số quy đổi", ""), ("Thưởng PL", ""),
        ("WES SHORT", ""), ("", ""), ("Tổng các mã", ""), ("Tiểu tác thừa", ""), ("Tổng lương sản phẩm", ""),
        ("Chuyên cần + Xăng xe ", ""), ("Tiền LCB/ Giờ công", ""),
        ("Lương tăng giờ", "Tăng giờ ngày thường"), ("", "Tiền phụ nữ"), ("", "TN"),
        ("Các khoản trừ", "BHXH 10.5%"), ("", "Thuế TNCN"), ("Tổng cộng", ""), ("Thực lĩnh", ""),
        ("Bồi dưỡng tiền ăn", ""),
    ];

    private const int ProductHeaderCol = 11;   // cột mã hàng chỉ có tiêu đề là số: 8698
    private const int NetPayCol = 23;

    private static IXLWorksheet AddPayrollSheet(XLWorkbook workbook, string name, bool thousandUnit = true)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.Cell(1, 1).Value = "BẢNG THANH TOÁN LƯƠNG THÁNG 7 NĂM 2026";
        sheet.Cell(2, 1).Value = $"Đơn vị: {name}";
        if (thousandUnit) sheet.Cell(3, 1).Value = "ĐVT: 1000 Đ";

        for (var c = 0; c < Headers.Length; c++)
        {
            if (Headers[c].Group.Length > 0) sheet.Cell(5, c + 1).Value = Headers[c].Group;
            if (Headers[c].Sub.Length > 0) sheet.Cell(6, c + 1).Value = Headers[c].Sub;
        }
        sheet.Cell(5, ProductHeaderCol).Value = 8698;   // mã hàng đặt tên bằng số
        for (var c = 1; c <= Headers.Length; c++) sheet.Cell(7, c).Value = c;   // dòng số thứ tự cột
        return sheet;
    }

    /// <summary>Dòng nhân viên của Tổ may mẫu; số tiền tính bằng nghìn đồng như file thật.</summary>
    private static void AddEmployee(IXLWorksheet sheet, int row, int stt, string name, int code, decimal netPayThousand)
    {
        sheet.Cell(row, 1).Value = stt;
        sheet.Cell(row, 2).Value = name;
        sheet.Cell(row, 3).Value = code;
        sheet.Cell(row, 4).Value = 6400;
        sheet.Cell(row, 5).Value = 26;
        sheet.Cell(row, 6).Value = 35;
        sheet.Cell(row, 7).Value = "a";
        sheet.Cell(row, 8).Value = 0.3;
        sheet.Cell(row, 9).Value = 1471.154;       // Thưởng PL
        sheet.Cell(row, 10).Value = 12308.204;     // WES SHORT
        sheet.Cell(row, 12).Value = 12308.204;     // Tổng các mã (số tổng, không được cộng lại)
        sheet.Cell(row, 14).Value = 13779.358;     // Tổng lương sản phẩm (số tổng)
        sheet.Cell(row, 15).Value = 1000;          // Chuyên cần + Xăng xe
        sheet.Cell(row, 16).Value = 30.769;        // Tiền LCB / giờ công
        sheet.Cell(row, 17).Value = 1075.933;      // Tăng giờ ngày thường
        sheet.Cell(row, 18).Value = 46.154;        // Tiền phụ nữ
        sheet.Cell(row, 19).Value = 300;           // TN → Thâm niên
        sheet.Cell(row, 20).Value = 672;           // BHXH 10.5%
        sheet.Cell(row, 21).Value = "-";           // Thuế TNCN: "-" là 0
        sheet.Cell(row, 22).Value = 16201.445;     // Tổng cộng (số tổng)
        sheet.Cell(row, NetPayCol).Value = netPayThousand;
        sheet.Cell(row, 24).Value = 480;           // vùng "Bảng phụ" bên phải Thực lĩnh: phải bị bỏ qua
    }

    private IReadOnlyList<PayrollExcelRowDto> Parse(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return _sut.Parse(stream);
    }

    private static XLWorkbook WorkbookWithOneEmployee()
    {
        var workbook = new XLWorkbook();
        var sheet = AddPayrollSheet(workbook, "Tổ may 1");
        AddEmployee(sheet, 8, 1, "Nguyễn Thị Thanh ", 25001, 15529.445m);
        return workbook;
    }

    [Fact]
    public void Parse_EmployeeRow_ConvertsThousandUnitAndMapsItemsByHeaderName()
    {
        using var workbook = WorkbookWithOneEmployee();

        var row = Parse(workbook).Should().ContainSingle().Subject;

        row.ParseError.Should().BeNull();
        row.SheetName.Should().Be("Tổ may 1");
        row.RowNumber.Should().Be(8);
        row.AttendanceCode.Should().Be("25001");
        row.FullName.Should().Be("Nguyễn Thị Thanh");
        row.GradeCode.Should().Be("A");
        row.WorkDays.Should().Be(26);
        row.NetPay.Should().BeApproximately(15_529_445m, 0.5m);

        var byName = row.Items.ToDictionary(i => i.Name);
        byName["LCB"].Should().Be(new PayrollExcelItemDto(PayslipItemGroup.Info, "LCB", 6_400_000m));
        byName["Ngày công SP"].Amount.Should().Be(26);   // số ngày công không nhân 1000
        byName["Số giờ làm thêm ngày thường"].Amount.Should().Be(35);
        byName["Phân loại: A"].Group.Should().Be(PayslipItemGroup.Info);
        byName["Thưởng PL"].Amount.Should().BeApproximately(1_471_154m, 0.5m);
        byName["WES SHORT"].Group.Should().Be(PayslipItemGroup.Addition);   // cột mã hàng nhận động
        byName["WES SHORT"].Amount.Should().BeApproximately(12_308_204m, 0.5m);
        byName["Thâm niên"].Amount.Should().Be(300_000m);                     // "TN" → Thâm niên
        byName["Chuyên cần + Xăng xe"].Amount.Should().Be(1_000_000m);
        byName["BHXH 10.5%"].Group.Should().Be(PayslipItemGroup.Insurance);
    }

    [Fact]
    public void Parse_SubtotalsAndIgnoredColumns_AreNotStoredAsItems()
    {
        using var workbook = WorkbookWithOneEmployee();

        var names = Parse(workbook).Single().Items.Select(i => i.Name).ToList();

        names.Should().NotContain(["Tổng các mã", "Tổng lương sản phẩm", "Tổng cộng", "Hệ số quy đổi",
            "Bồi dưỡng tiền ăn", "Thuế TNCN"]);   // Thuế TNCN = "-" nên là 0, không tạo khoản
        names.Should().NotContain(n => n.Contains("STT") || n.Contains("Họ"));
    }

    [Fact]
    public void Parse_LeafAdditionsMinusInsurance_EqualNetPay()
    {
        using var workbook = WorkbookWithOneEmployee();

        var row = Parse(workbook).Single();
        var additions = row.Items.Where(i => i.Group == PayslipItemGroup.Addition).Sum(i => i.Amount);
        var insurance = row.Items.Where(i => i.Group == PayslipItemGroup.Insurance).Sum(i => i.Amount);

        (additions - insurance).Should().BeApproximately(row.NetPay!.Value, 1m);
    }

    [Fact]
    public void Parse_NumberingRowTotalRowAndTrailingTable_AreNotEmployees()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddPayrollSheet(workbook, "Tổ may 1");
        AddEmployee(sheet, 8, 1, "Nguyễn Thị Thanh", 25001, 15529.445m);
        sheet.Cell(9, 2).Value = " Tổng cán bộ ";                 // không có SLĐ
        AddEmployee(sheet, 10, 2, "Nguyễn Thị Hạnh", 25014, 15529.445m);
        sheet.Cell(11, 1).Value = "Tổng cộng";                    // hết bảng nhân viên
        sheet.Cell(13, 1).Value = 6;                              // bảng nhỏ bên dưới: số công nằm đúng cột SLĐ
        sheet.Cell(13, 2).Value = "Huỳnh Triệu Vi";
        sheet.Cell(13, 3).Value = 23;

        var rows = Parse(workbook);

        rows.Select(r => r.AttendanceCode).Should().Equal("25001", "25014");
    }

    [Fact]
    public void Parse_MultipleSheets_ReadsAllPayrollSheetsAndSkipsOthers()
    {
        using var workbook = new XLWorkbook();
        AddEmployee(AddPayrollSheet(workbook, "Tổ may 1"), 8, 1, "Nguyễn Thị Thanh", 25001, 15529.445m);
        AddEmployee(AddPayrollSheet(workbook, "Tổ may 2"), 8, 1, "Lê Bích Thuỷ", 25062, 15529.445m);
        workbook.Worksheets.Add("Ghi chú").Cell(1, 1).Value = "không có cột mã chấm công";

        var rows = Parse(workbook);

        rows.Select(r => (r.SheetName, r.AttendanceCode)).Should()
            .Equal(("Tổ may 1", "25001"), ("Tổ may 2", "25062"));
    }

    [Fact]
    public void Parse_CutSheetWithConLinhAndNoProductColumns_ReadsByName()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Cắt");
        sheet.Cell(3, 1).Value = "Đvt: 1000 đ";
        string[] headers = ["STT", "Họ tên", "SLĐ", "LCB", "PL", "Lương sản phẩm theo hệ số", "Thưởng phân loại",
            "BHXH 10.5%", "Còn lĩnh", "Ký nhận"];
        for (var c = 0; c < headers.Length; c++) sheet.Cell(5, c + 1).Value = headers[c];
        sheet.Cell(8, 1).Value = 1;
        sheet.Cell(8, 2).Value = "Lương Thị Tuyết";
        sheet.Cell(8, 3).Value = 25090;
        sheet.Cell(8, 4).Value = 5300;
        sheet.Cell(8, 5).Value = "A";
        sheet.Cell(8, 6).Value = 8000;
        sheet.Cell(8, 7).Value = 600;
        sheet.Cell(8, 8).Value = 557;
        sheet.Cell(8, 9).Value = 8043;

        var row = Parse(workbook).Should().ContainSingle().Subject;

        row.ParseError.Should().BeNull();
        row.NetPay.Should().Be(8_043_000m);
        row.GradeCode.Should().Be("A");
        row.Items.Single(i => i.Name == "Lương sản phẩm theo hệ số").Amount.Should().Be(8_000_000m);
        row.Items.Single(i => i.Name == "Thưởng PL").Amount.Should().Be(600_000m);   // "Thưởng phân loại" → tên chuẩn
    }

    [Fact]
    public void Parse_NoUnitLine_KeepsAmountsAsIs()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddPayrollSheet(workbook, "Tổ may 1", thousandUnit: false);
        AddEmployee(sheet, 8, 1, "Nguyễn Thị Thanh", 25001, 15529445m);

        Parse(workbook).Single().NetPay.Should().Be(15_529_445m);
    }

    [Fact]
    public void Parse_FormulaErrorInNetPay_MarksOnlyThatRowAsError()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddPayrollSheet(workbook, "Tổ may 1");
        AddEmployee(sheet, 8, 1, "Nguyễn Thị Thanh", 25001, 15529.445m);
        AddEmployee(sheet, 9, 2, "Nguyễn Thị Hạnh", 25014, 15529.445m);
        sheet.Cell(9, NetPayCol).FormulaA1 = "=1/0";

        var rows = Parse(workbook);

        rows.Should().HaveCount(2);
        rows[0].ParseError.Should().BeNull();
        rows[1].ParseError.Should().Contain("Thực lĩnh").And.Contain("dòng 9");
        rows[1].AttendanceCode.Should().Be("25014");
    }

    [Fact]
    public void Parse_NonIntegerAttendanceCode_MarksRowError()
    {
        using var workbook = new XLWorkbook();
        var sheet = AddPayrollSheet(workbook, "Tổ may 1");
        AddEmployee(sheet, 8, 1, "Nguyễn Thị Thanh", 25001, 15529.445m);
        sheet.Cell(8, 3).Value = "ABC";

        Parse(workbook).Single().ParseError.Should().Contain("SLĐ");
    }

    [Fact]
    public void Parse_NoSheetWithAttendanceCode_ThrowsInvalidData()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sheet1").Cell(1, 1).Value = "bảng khác";

        var act = () => Parse(workbook);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_AttendanceCodeSheetWithoutNetPayColumn_ThrowsInvalidData()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tổ may 1");
        sheet.Cell(5, 1).Value = "STT";
        sheet.Cell(5, 3).Value = "SLĐ";

        var act = () => Parse(workbook);

        act.Should().Throw<InvalidDataException>().WithMessage("*Tổ may 1*Thực lĩnh*");
    }
}

[Collection(ExcelTestCollection.Name)]
public class PayslipLayoutTests
{
    [Theory]
    [InlineData("SLĐ", "sld")]
    [InlineData(" Chuyên Cần +Xăng Xe ", "chuyencanxangxe")]
    [InlineData("BHXH 10.5%", "bhxh105")]
    [InlineData(null, "")]
    public void NormalizeKey_StripsAccentsSpacesAndSymbols(string? input, string expected) =>
        PayslipLayout.NormalizeKey(input).Should().Be(expected);

    [Fact]
    public void FindColumn_DifferentSpellingsOfSameColumn_ResolveToSameEntry()
    {
        PayslipLayout.FindColumn("Chuyên Cần +Xăng Xe")!.DisplayName.Should().Be("Chuyên cần + Xăng xe");
        PayslipLayout.FindColumn("Chuyên cần + Xăng xe ")!.DisplayName.Should().Be("Chuyên cần + Xăng xe");
        PayslipLayout.FindColumn("TLSP/ Giờ công")!.DisplayName.Should().Be("Tiền LSP/ Giờ công");
    }

    [Fact]
    public void FindColumn_UnknownLabel_IsNull() => PayslipLayout.FindColumn("WES SHORT").Should().BeNull();

    [Fact]
    public void Describe_KnownColumns_ReturnSectionAndKind()
    {
        var days = PayslipLayout.Describe(PayslipItemGroup.Info, "Ngày công SP");
        days.Section.Should().Be(PayslipLayout.SectionDays);
        days.Kind.Should().Be(PayslipValueKind.Number);

        var overtime = PayslipLayout.Describe(PayslipItemGroup.Addition, "Tăng giờ ngày thường");
        overtime.Section.Should().Be(PayslipLayout.SectionOvertime);
        overtime.Kind.Should().Be(PayslipValueKind.Money);

        PayslipLayout.Describe(PayslipItemGroup.Insurance, "BHXH 10.5%").Section
            .Should().Be(PayslipLayout.SectionDeductions);
    }

    [Fact]
    public void Describe_GradeItem_IsTextWithValue()
    {
        var grade = PayslipLayout.Describe(PayslipItemGroup.Info, "Phân loại: A");

        grade.Kind.Should().Be(PayslipValueKind.Text);
        grade.DisplayName.Should().Be("Phân loại");
        grade.TextValue.Should().Be("A");
        grade.Section.Should().Be(PayslipLayout.SectionGeneral);
    }

    [Fact]
    public void Describe_UnknownAddition_IsProductCodeUnderPieceSection()
    {
        var item = PayslipLayout.Describe(PayslipItemGroup.Addition, "FANKA");

        item.Section.Should().Be(PayslipLayout.SectionPiece);
        item.DisplayName.Should().Be("FANKA");
        item.Kind.Should().Be(PayslipValueKind.Money);
    }
}
