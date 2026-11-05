using System.Globalization;
using System.Text.Json;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>
/// Dựng câu mô tả tiếng Việt cho một dòng AuditLog ngay lúc ghi, vd. "Cập nhật Đơn hàng DH-0012: Trạng thái Mới → Đang sản xuất".
/// Người đọc nhật ký thấy ngay ai đã làm gì mà không phải đọc JSON. Mã bản ghi lấy từ giá trị đã log (OrderCode, ProductCode, ...),
/// không có thì dùng Id.
/// </summary>
public static class AuditText
{
    private const int MaxLength = 500;
    private const int MaxChanges = 4;

    private static readonly Dictionary<string, string> Tables = new()
    {
        ["Account"] = "Tài khoản", ["AccountingPeriod"] = "Kỳ kế toán", ["Announcement"] = "Thông báo chung",
        ["BreakEvenResult"] = "Kết quả hòa vốn", ["Customer"] = "Khách hàng", ["CustomerOrder"] = "Đơn hàng",
        ["DefectRecord"] = "Phiếu lỗi", ["DispatchNote"] = "Phiếu xuất giao", ["Employee"] = "Nhân viên",
        ["GradeLevel"] = "Mức tiền bậc thợ", ["GradeLevelCode"] = "Bậc thợ", ["KpiTarget"] = "Chỉ tiêu KPI",
        ["MonthlyCost"] = "Chi phí tháng", ["Operation"] = "Công đoạn", ["OutputRecord"] = "Sản lượng",
        ["Payroll"] = "Bảng lương", ["PieceRate"] = "Đơn giá công đoạn", ["Product"] = "Mã hàng",
        ["PurchaseOrder"] = "Đơn mua NVL", ["QcBatch"] = "Lô QC", ["Request"] = "Đơn từ", ["Stocktake"] = "Phiếu kiểm kho",
        ["Supplier"] = "Nhà cung cấp", ["SystemSetting"] = "Cài đặt hệ thống", ["Team"] = "Tổ", ["Vehicle"] = "Xe",
        ["VehicleTrip"] = "Chuyến xe", ["WorkAllocation"] = "Chia việc",
    };

    private static readonly Dictionary<string, string> Verbs = new()
    {
        [AuditActions.Insert] = "Tạo mới", [AuditActions.Update] = "Cập nhật", [AuditActions.Delete] = "Xóa",
        [AuditActions.Approve] = "Duyệt", [AuditActions.Reject] = "Từ chối", [AuditActions.Confirm] = "Xác nhận",
        [AuditActions.Lock] = "Khóa", [AuditActions.Unlock] = "Mở khóa",
    };

    private static readonly Dictionary<string, string> Fields = new()
    {
        ["ActualQty"] = "Số lượng thực tế", ["AttendanceCode"] = "Mã chấm công", ["ConfirmedQty"] = "Số lượng xác nhận",
        ["Content"] = "Nội dung", ["CurrentGradeCode"] = "Bậc thợ hiện tại", ["CustomerReceivedDate"] = "Ngày khách nhận",
        ["DateOfBirth"] = "Ngày sinh", ["DecisionNote"] = "Ghi chú duyệt", ["Description"] = "Mô tả",
        ["DueDate"] = "Ngày giao", ["EffectiveDate"] = "Ngày hiệu lực", ["ExpectedDeliveryCount"] = "Số lần giao dự kiến",
        ["ExtraImages"] = "Số ảnh phụ",
        ["ColorImages"] = "Số ảnh theo màu",
        ["FullName"] = "Họ tên", ["Gender"] = "Giới tính", ["GoodsStatus"] = "Trạng thái hàng", ["GradeCode"] = "Mã bậc thợ",
        ["GradeName"] = "Tên bậc thợ", ["MonthlyAmount"] = "Số tiền theo tháng", ["Note"] = "Ghi chú",
        ["OrderDate"] = "Ngày đặt", ["PaidAmount"] = "Số tiền đã trả", ["PayType"] = "Hình thức lương",
        ["PaymentDate"] = "Ngày thanh toán", ["PaymentMethod"] = "Hình thức thanh toán", ["PaymentStatus"] = "Trạng thái thanh toán",
        ["Phone"] = "Số điện thoại", ["ProductName"] = "Tên hàng", ["Quantity"] = "Số lượng", ["Rate"] = "Đơn giá",
        ["ReceivedDate"] = "Ngày nhận", ["ResignDate"] = "Ngày nghỉ việc", ["Result"] = "Kết quả", ["RoleCode"] = "Vai trò", ["SampleImageUrl"] = "Ảnh chính",
        ["SkillLevel"] = "Bậc tay nghề", ["StandardTimeSec"] = "Thời gian chuẩn (giây)", ["StartDate"] = "Ngày vào làm",
        ["Status"] = "Trạng thái", ["StopReason"] = "Lý do dừng", ["SupplierId"] = "Nhà cung cấp", ["TargetDate"] = "Ngày mục tiêu",
        ["TeamId"] = "Tổ", ["TotalAmount"] = "Tổng tiền", ["WorkDate"] = "Ngày làm việc",
    };

    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACTIVE"] = "Đang hoạt động", ["APPROVED"] = "Đã duyệt", ["CANCELLED"] = "Đã hủy", ["CLOSED"] = "Đã đóng",
        ["CONFIRMED"] = "Đã xác nhận", ["DISCONTINUED"] = "Ngừng dùng", ["DRAFT"] = "Nháp", ["IN_DELIVERY"] = "Đang giao",
        ["INDELIVERY"] = "Đang giao", ["LOCKED"] = "Đã khóa", ["NEW"] = "Mới", ["OPEN"] = "Mở", ["ORDERED"] = "Đã đặt hàng",
        ["PAID"] = "Đã thanh toán", ["PENDING"] = "Chờ xử lý", ["PENDING_APPROVAL"] = "Chờ duyệt", ["PENDINGAPPROVAL"] = "Chờ duyệt",
        ["RECORDED"] = "Đã ghi nhận", ["REJECTED"] = "Từ chối", ["RESIGNED"] = "Đã nghỉ việc", ["STARTED"] = "Đang sản xuất",
        ["UNPAID"] = "Chưa thanh toán", ["REWORK"] = "Cần sửa lại", ["SCRAP"] = "Hỏng không sửa được",
    };

    /// <summary>Mã / tên dùng để gọi bản ghi (ưu tiên mã nghiệp vụ), theo thứ tự các field thường có trong giá trị đã log.</summary>
    private static readonly string[] IdentityFields =
    [
        "OrderCode", "ProductCode", "EmployeeCode", "DispatchCode", "DefectCode", "OperationCode", "PoCode", "RequestCode",
        "GradeCode", "PlateNumber", "FullName", "ProductName", "OperationName", "Title",
    ];

    public static string TableLabel(string tableName) => Tables.GetValueOrDefault(tableName, tableName);

    /// <summary>Câu mô tả đầy đủ: "{Hành động} {Đối tượng} {mã}[: thay đổi][ (lý do)]".</summary>
    public static string Summary(string tableName, long recordId, string action, string? oldJson, string? newJson, string? reason)
    {
        var oldValues = Flatten(oldJson);
        var newValues = Flatten(newJson);
        var identity = IdentityFields.Select(f => newValues.GetValueOrDefault(f) ?? oldValues.GetValueOrDefault(f))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var text = $"{Verbs.GetValueOrDefault(action, action)} {TableLabel(tableName).ToLowerInvariantFirst()} " +
                   (identity ?? (recordId > 0 ? $"#{recordId}" : string.Empty));
        text = text.TrimEnd();

        var changes = action == AuditActions.Update ? DescribeChanges(oldValues, newValues) : [];
        if (changes.Count > 0) text += ": " + string.Join("; ", changes.Take(MaxChanges)) + (changes.Count > MaxChanges ? "…" : string.Empty);
        if (!string.IsNullOrWhiteSpace(reason)) text += $" (lý do: {reason.Trim()})";
        return text.Length > MaxLength ? text[..(MaxLength - 1)] + "…" : text;
    }

    private static List<string> DescribeChanges(Dictionary<string, string?> before, Dictionary<string, string?> after)
    {
        var result = new List<string>();
        foreach (var (key, value) in after)
        {
            if (IdentityFields.Contains(key) && !Fields.ContainsKey(key)) continue;
            if (before.TryGetValue(key, out var old))
            {
                if (old == value) continue;
                result.Add($"{FieldLabel(key)} {Show(old)} → {Show(value)}");
            }
            else
                result.Add($"{FieldLabel(key)} {Show(value)}");
        }
        return result;
    }

    private static string FieldLabel(string key) => Fields.GetValueOrDefault(key, SplitWords(key));

    private static string Show(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(trống)";
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return Values.GetValueOrDefault(value, value);
    }

    private static string SplitWords(string key)
    {
        var chars = new List<char>();
        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && char.IsUpper(key[i]) && char.IsLower(key[i - 1])) chars.Add(' ');
            chars.Add(i == 0 ? key[i] : char.ToLowerInvariant(key[i]));
        }
        return new string([.. chars]);
    }

    private static string ToLowerInvariantFirst(this string text) =>
        text.Length > 0 ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    /// <summary>Các field đơn giản (chuỗi, số, bool, ngày) của JSON log; bỏ mảng và object lồng nhau.</summary>
    private static Dictionary<string, string?> Flatten(string? json)
    {
        var result = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                result[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetDecimal(out var number) ? number.ToString("0.##", CultureInfo.InvariantCulture) : p.Value.GetRawText(),
                    JsonValueKind.True => "Có",
                    JsonValueKind.False => "Không",
                    JsonValueKind.Null => null,
                    _ => "(nhiều mục)",
                };
            }
        }
        catch (JsonException)
        {
            // JSON log hỏng: không dựng được chi tiết, chỉ còn câu chung.
        }
        return result;
    }
}
