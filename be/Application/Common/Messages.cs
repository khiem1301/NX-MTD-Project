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
}
