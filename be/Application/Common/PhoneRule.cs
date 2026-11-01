using FluentValidation;

namespace NX_MTD.Application.Common;

/// <summary>Quy tắc số điện thoại dùng chung: 10 số bắt đầu bằng 0 (vd. 0901234567) hoặc +84 kèm 9 số; cho phép để trống nếu trường không bắt buộc.</summary>
public static class PhoneRule
{
    // [0-9], không dùng \d: \d của .NET khớp cả chữ số Ả Rập – Ấn Độ, mà cột là VARCHAR. \z, không dùng $: $ cho phép xuống dòng ở cuối.
    public const string Pattern = @"^(?:0[0-9]{9}|\+84[0-9]{9})\z";
    public const string Message = "Số điện thoại không hợp lệ (10 số bắt đầu bằng 0, hoặc +84 và 9 số).";

    public static IRuleBuilderOptions<T, string?> MustBePhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(Pattern).WithMessage(Message);
}
