using System.Text.RegularExpressions;
using FluentValidation;

namespace NX_MTD.Application.Common;

/// <summary>Quy tắc họ tên người dùng chung: chỉ chữ cái (kể cả tiếng Việt có dấu) và khoảng trắng giữa các từ; không số, không ký tự đặc biệt.</summary>
public static class PersonNameRule
{
    // \p{L} chữ cái, \p{M} dấu rời (tên gõ dạng tổ hợp). \z, không dùng $: $ cho phép xuống dòng ở cuối.
    public const string Pattern = @"^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)*\z";
    public const string Message = "Họ tên chỉ gồm chữ cái và khoảng trắng, không được có số hay ký tự đặc biệt.";

    private static readonly Regex NameRegex = new(Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? name) => name is not null && NameRegex.IsMatch(name.Trim());

    public static IRuleBuilderOptions<T, string> MustBePersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsValid).WithMessage(Message);
}
