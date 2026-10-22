using System.Globalization;
using System.Text;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>Cách hiển thị giá trị một khoản trên phiếu lương: tiền, số (ngày/giờ công) hoặc chữ (bậc thợ).</summary>
public enum PayslipValueKind { Money, Number, Text }

/// <summary>Một cột đã biết của bảng thanh toán lương. <see cref="Group"/> = null nghĩa là cột bỏ qua
/// (số tổng, hệ số, phần công ty đóng...) — không lưu thành khoản trên phiếu.</summary>
public sealed record PayslipColumn(
    string DisplayName, string? Group, string Section, PayslipValueKind Kind, bool CountsAsWorkDay = false);

/// <summary>Kết quả tra danh mục cho một khoản đã lưu: nhóm hiển thị, kiểu giá trị, tên và chữ (nếu là kiểu chữ).</summary>
public sealed record PayslipItemView(string Section, PayslipValueKind Kind, string DisplayName, string? TextValue);

/// <summary>
/// Danh mục cột của file bảng thanh toán lương thật (DEV-MI-09-4): tên cột → nhóm cộng/trừ (<c>ItemGroup</c>) và
/// nhóm hiển thị trên phiếu lương. Đặt trong code vì <c>PayslipItem</c> chỉ có 4 giá trị <c>ItemGroup</c> và schema
/// không có cột nhóm hiển thị. Tên cột được so khớp sau khi bỏ dấu/khoảng trắng/ký tự đặc biệt nên
/// "Chuyên Cần +Xăng Xe" và "Chuyên cần + Xăng xe " là một.
/// </summary>
public static class PayslipLayout
{
    public const string SectionGeneral = "Thông tin chung";
    public const string SectionDays = "Ngày công, giờ công";
    public const string SectionPiece = "Lương sản phẩm";
    public const string SectionAllowance = "Phụ cấp";
    public const string SectionRates = "Đơn giá";
    public const string SectionOvertime = "Lương tăng giờ";
    public const string SectionTimeWage = "Lương thời gian";
    public const string SectionBenefits = "Các khoản phụ cấp, phúc lợi và hỗ trợ";
    public const string SectionBonus = "Tiền thưởng";
    public const string SectionDeductions = "Các khoản trừ";
    public const string SectionOther = "Khác";

    /// <summary>Thứ tự các nhóm trên phiếu, theo mẫu phiếu lương công nhân đang dùng.</summary>
    public static readonly string[] Sections =
    [
        SectionGeneral, SectionDays, SectionPiece, SectionAllowance, SectionRates, SectionOvertime,
        SectionTimeWage, SectionBenefits, SectionBonus, SectionDeductions, SectionOther,
    ];

    public const string GradeItemPrefix = "Phân loại";
    public const string GradeColumnKey = "phanloai";
    public const string PieceBonusKey = "thuongpl";
    public const string PieceSubtotalKey = "tongcacma";
    public const string NetPayKey = "thuclinh";
    public const string NetPayAltKey = "conlinh";
    public const string EmployeeCodeKey = "sld";
    public const string FullNameKey = "hoten";

    private static readonly Dictionary<string, PayslipColumn> Columns = Build();

    /// <summary>Bỏ dấu, bỏ mọi ký tự không phải chữ/số, hạ chữ thường: "SLĐ" → "sld", "BHXH 10.5%" → "bhxh105".</summary>
    public static string NormalizeKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is 'đ' or 'Đ') { sb.Append('d'); continue; }
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    public static PayslipColumn? FindColumn(string? label) =>
        Columns.TryGetValue(NormalizeKey(label), out var column) ? column : null;

    public static int SectionOrder(string section)
    {
        var index = Array.IndexOf(Sections, section);
        return index < 0 ? Sections.Length : index;
    }

    /// <summary>Tra một khoản đã lưu (nhóm + tên) để biết nó nằm ở nhóm nào trên phiếu và hiển thị kiểu gì.
    /// Khoản không có trong danh mục là cột mã hàng (parser chỉ sinh khoản cộng lạ cho cột mã hàng).</summary>
    public static PayslipItemView Describe(string group, string itemName)
    {
        if (itemName.StartsWith(GradeItemPrefix + ":", StringComparison.OrdinalIgnoreCase))
            return new PayslipItemView(SectionGeneral, PayslipValueKind.Text, GradeItemPrefix,
                itemName[(GradeItemPrefix.Length + 1)..].Trim());

        var column = FindColumn(itemName);
        if (column is { Group: not null })
            return new PayslipItemView(column.Section, column.Kind, column.DisplayName, null);

        return group switch
        {
            PayslipItemGroup.Addition => new PayslipItemView(SectionPiece, PayslipValueKind.Money, itemName, null),
            PayslipItemGroup.Deduction or PayslipItemGroup.Insurance =>
                new PayslipItemView(SectionDeductions, PayslipValueKind.Money, itemName, null),
            _ => new PayslipItemView(SectionOther, PayslipValueKind.Number, itemName, null),
        };
    }

    private static Dictionary<string, PayslipColumn> Build()
    {
        var map = new Dictionary<string, PayslipColumn>();

        void Add(PayslipColumn column, params string[] aliases)
        {
            map[NormalizeKey(column.DisplayName)] = column;
            foreach (var alias in aliases) map[NormalizeKey(alias)] = column;
        }

        PayslipColumn Info(string name, string section, PayslipValueKind kind, bool workDay = false) =>
            new(name, PayslipItemGroup.Info, section, kind, workDay);
        PayslipColumn Plus(string name, string section) =>
            new(name, PayslipItemGroup.Addition, section, PayslipValueKind.Money);
        PayslipColumn Ignored(string name) => new(name, null, SectionOther, PayslipValueKind.Number);

        // Thông tin chung + ngày công, giờ công (INFO — chỉ hiển thị, không cộng vào tổng)
        Add(Info("LCB", SectionGeneral, PayslipValueKind.Money));
        Add(Info(GradeItemPrefix, SectionGeneral, PayslipValueKind.Text), "PL");
        Add(Info("Số giờ tập huấn, hội họp, nghỉ ngắn", SectionDays, PayslipValueKind.Number));
        Add(Info("Ngày công lễ", SectionDays, PayslipValueKind.Number, workDay: true));
        Add(Info("Ngày công phép", SectionDays, PayslipValueKind.Number, workDay: true));
        Add(Info("Ngày công SP", SectionDays, PayslipValueKind.Number, workDay: true));
        Add(Info("Số giờ làm thêm ngày thường", SectionDays, PayslipValueKind.Number));
        Add(Info("Số giờ làm thêm chủ nhật", SectionDays, PayslipValueKind.Number));
        Add(Info("Số giờ công hành chính", SectionDays, PayslipValueKind.Number));
        Add(Info("Tiền LCB/ Giờ công", SectionRates, PayslipValueKind.Money), "TLCB/ Giờ công");
        Add(Info("Tiền LSP/ Giờ công", SectionRates, PayslipValueKind.Money), "TLSP/ Giờ công");

        // Khoản cộng
        Add(Plus("Thưởng PL", SectionPiece), "Thưởng phân loại");
        Add(Plus("Lương sản phẩm theo hệ số", SectionPiece));
        Add(Plus("Tiểu tác thừa", SectionPiece));
        Add(Plus("Chuyên cần + Xăng xe", SectionAllowance));
        Add(Plus("Tăng giờ ngày thường", SectionOvertime));
        Add(Plus("Tăng giờ chủ nhật", SectionOvertime));
        Add(Plus("Lương tập huấn, hội họp, nghỉ ngắn", SectionTimeWage));
        Add(Plus("Lương ngày công lễ", SectionTimeWage));
        Add(Plus("Lương ngày công phép", SectionTimeWage));
        Add(Plus("1.5H cho CN 18T", SectionBenefits));
        Add(Plus("Tiền phụ nữ", SectionBenefits));
        Add(Plus("Tiền ưu tiên", SectionBenefits));
        Add(Plus("Tiền gửi trẻ", SectionBenefits));
        Add(Plus("Phụ cấp AT VSV", SectionBenefits));
        Add(Plus("Nhà lông, là, cắt", SectionBenefits));
        Add(Plus("Nhà trọ", SectionBenefits));
        Add(Plus("CTY bù", SectionBenefits));
        Add(Plus("Thâm niên", SectionBonus), "TN");
        Add(Plus("Thưởng tuần", SectionBonus));
        Add(Plus("Thưởng tuyển dụng", SectionBonus));
        Add(Plus("Công ty bù", SectionBonus));

        // Khoản trừ (thực lĩnh = tổng cộng − BHXH − thuế)
        Add(new PayslipColumn("BHXH 10.5%", PayslipItemGroup.Insurance, SectionDeductions, PayslipValueKind.Money));
        Add(new PayslipColumn("Thuế TNCN", PayslipItemGroup.Deduction, SectionDeductions, PayslipValueKind.Money));

        // Bỏ qua: số tổng (cộng lại sẽ bị đếm hai lần), hệ số, phần BHXH công ty đóng, cột nhận dạng
        foreach (var name in new[]
                 {
                     "STT", "Họ Tên", "Họ và tên", "SLĐ", "Tổng các mã", "Tổng lương sản phẩm",
                     "Tổng lương sản phẩm (Ẩn)", "Tổng cộng", "Trả BHXH 21.5%", "Công ngày thường +chủ nhật (Ẩn)",
                     "Hệ số quy đổi", "Hệ số phân loại", "Hệ số công việc", "Tiền thưởng", "Ngày công, giờ công",
                     "Các khoản phụ cấp, phúc lợi và hỗ trợ", "Các khoản trừ", "Lương tăng giờ", "Lương thời gian",
                 })
            Add(Ignored(name));

        return map;
    }
}
