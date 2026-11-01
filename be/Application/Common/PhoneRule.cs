using FluentValidation;

namespace NX_MTD.Application.Common;

/// <summary>Quy tắc số điện thoại dùng chung: 10 số bắt đầu bằng 0 (vd. 0901234567) hoặc +84 kèm 9 số; cho phép để trống nếu trường không bắt buộc.</summary>
public static class PhoneRule
{
    public const string Pattern = @"^(?:0\d{9}|\+84\d{9})$";
    public const string Message = "Số điện thoại không hợp lệ (10 số bắt đầu bằng 0, hoặc +84 và 9 số).";

    public static IRuleBuilderOptions<T, string?> MustBePhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(Pattern).WithMessage(Message);
}
