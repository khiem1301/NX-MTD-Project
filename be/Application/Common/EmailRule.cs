using System.Text.RegularExpressions;
using FluentValidation;

namespace NX_MTD.Application.Common;

/// <summary>
/// Quy tắc email dùng chung: dạng ten@mien.com, chỉ ký tự ASCII (cột Email là VARCHAR nên chữ có dấu sẽ bị đổi thành "?"),
/// phần tên miền có ít nhất một dấu chấm, không có khoảng trắng. Kiểm sau khi cắt khoảng trắng đầu cuối vì service lưu bản đã cắt.
/// </summary>
public static class EmailRule
{
    public const string Pattern =
        @"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+\z";
    public const string Message = "Email không hợp lệ (dạng ten@mien.com, chỉ dùng chữ không dấu, số và ký tự thông thường).";

    private static readonly Regex Regex = new(Pattern, RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, string?> MustBeEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || Regex.IsMatch(v.Trim())).WithMessage(Message);
}
