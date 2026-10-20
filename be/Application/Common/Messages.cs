namespace NX_MTD.Application.Common;

/// <summary>Chuỗi tiếng Việt theo mã MSG trong SRS.</summary>
public static class Messages
{
    public const string InvalidCredentials = "Tên đăng nhập hoặc mật khẩu không đúng.";       // MSG04
    public const string AccountLocked = "Tài khoản đã bị khóa. Vui lòng liên hệ Admin.";     // MSG06
    public const string AccountDisabled = "Nhân viên đã nghỉ việc, không thể đăng nhập.";
    public const string InvalidRefreshToken = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    public const string UsernameRequired = "Vui lòng nhập tên đăng nhập.";                   // MSG01
    public const string PasswordRequired = "Vui lòng nhập mật khẩu.";                         // MSG01
    public const string Unauthorized = "Bạn chưa đăng nhập hoặc phiên đã hết hạn.";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string InternalError = "Lỗi hệ thống. Vui lòng thử lại.";
    public const string ConcurrencyConflict = "Dữ liệu đã bị người khác thay đổi. Vui lòng tải lại.";
    public const string LoggedOut = "Đã đăng xuất.";

    // --- Chấm công & suất ăn (Minh) ---
    public const string Saved = "Đã lưu.";                                                   // MSG03
    public const string NoData = "Không có dữ liệu phù hợp.";                                // MSG02
    public const string ReasonRequired = "Vui lòng nhập lý do.";                              // MSG09
    public const string AttendanceEmployeeNotFound = "Không tìm thấy nhân viên.";
    public const string AttendanceCodeAlreadyMapped = "Mã chấm công này đã gán cho nhân viên khác.";
    public const string AttendanceEmployeeHasCode = "Nhân viên này đã có mã chấm công.";
    public const string AttendanceCodeNotFound = "Không có lượt quẹt nào mang mã chấm công này.";
    public const string AttendanceEmptyBatch = "Lô dữ liệu chấm công rỗng.";
    public const string AttendanceSyncOverdue =
        "Chưa đồng bộ được chấm công hơn 30 phút. Có thể nhập file dự phòng.";                // MSG23
    public const string DateRangeInvalid = "Ngày kết thúc phải không nhỏ hơn ngày bắt đầu.";
    public const string DateRangeTooWide = "Khoảng ngày tối đa là {0} ngày.";
    public const string AttendanceAdjustmentExists = "Nhân viên này đã có chấm bù cho ngày này.";
    public const string AttendanceEmployeeInactive = "Nhân viên đã nghỉ việc, không thể chấm bù.";
    public const string AttendanceAdjustmentNoTime = "Vui lòng nhập giờ vào hoặc giờ ra.";
    public const string AttendanceAdjustmentRequestRequired = "Chấm bù theo đơn phải chọn đơn xin chấm công.";
    public const string AttendanceRequestInvalid = "Đơn xin chấm công bù phải ở trạng thái đã duyệt.";
    public const string AttendancePeriodLockedMessage = "Tháng {0} đã khóa, không thể thay đổi dữ liệu.";   // MSG21
    public const string AttendanceExcelInvalid = "File Excel không đúng định dạng (cần cột: mã chấm công, ngày, giờ vào, giờ ra).";
    public const string AttendanceExcelEmpty = "File Excel không có dòng dữ liệu nào.";
}
