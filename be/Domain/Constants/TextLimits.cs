namespace NX_MTD.Domain.Constants;

/// <summary>
/// Giới hạn độ dài chữ dùng chung (DEV-ALL-72). FE giữ cùng giá trị ở <c>src/constants/limits.ts</c>.
/// </summary>
public static class TextLimits
{
    /// <summary>Mọi ô "Lý do" (từ chối, trả lại, lệch số, sửa sau hạn…); khớp các cột lý do NVARCHAR(500).</summary>
    public const int Reason = 500;

    /// <summary>Mật khẩu mới khi đổi mật khẩu.</summary>
    public const int Password = 50;
}
