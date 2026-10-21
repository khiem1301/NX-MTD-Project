using System.Text.RegularExpressions;

namespace NX_MTD.Application.Common;

/// <summary>Khóa lưu trữ ảnh mẫu mã hàng tải từ máy, vd. "PRODUCT/2026/10/3f2a….jpg". Ảnh cũ có thể là đường dẫn http(s).</summary>
public static class SampleImageKey
{
    private static readonly Regex Pattern = new(@"^PRODUCT/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$", RegexOptions.Compiled);
    public static bool IsValid(string? key) => key is not null && Pattern.IsMatch(key);
}
