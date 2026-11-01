using FluentValidation;

namespace NX_MTD.Application.Common;

public static class TextLengthRule
{
    /// <summary>Độ dài tối đa tính sau khi cắt khoảng trắng đầu cuối, đúng với giá trị service lưu (MaximumLength tính cả khoảng trắng thừa).</summary>
    public static IRuleBuilderOptions<T, string?> MaxTrimmedLength<T>(this IRuleBuilder<T, string?> rule, int max, string label) =>
        rule.Must(v => v is null || v.Trim().Length <= max).WithMessage($"{label} tối đa {max} ký tự.");
}
