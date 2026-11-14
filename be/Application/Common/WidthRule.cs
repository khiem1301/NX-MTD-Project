using FluentValidation;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>DEV-ALL-102: khổ (chiều rộng, mét) của vải / bìa: bắt buộc, lớn hơn 0, tối đa 3,5 m, tối đa 2 số lẻ.</summary>
public static class WidthRule
{
    /// <param name="label">Tên trường viết hoa chữ đầu, vd "Khổ vải".</param>
    public static IRuleBuilderOptions<T, decimal?> ValidWidthM<T>(this IRuleBuilderInitial<T, decimal?> rule, string label) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(Messages.Required(label.ToLowerInvariant()))
            .GreaterThan(0m).WithMessage(string.Format(Messages.WidthPositive, label))
            .LessThanOrEqualTo(WidthLimits.MaxM).WithMessage(string.Format(Messages.WidthTooLarge, label))
            .Must(v => v is null || decimal.Round(v.Value, WidthLimits.Decimals) == v)
            .WithMessage(string.Format(Messages.WidthDecimals, label, WidthLimits.Decimals));
}

/// <summary>Tên trường khổ trong thông báo lỗi (DEV-ALL-102).</summary>
public static class WidthLabels
{
    public const string Fabric = "Khổ vải";
    public const string Marker = "Khổ bìa";
}
