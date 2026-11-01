using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>UC59: khoảng ngày xem lương tạm tính hợp lệ và không quá rộng (cùng giới hạn như bảng công).</summary>
public sealed class MyEstimatedPayQueryValidator : AbstractValidator<MyEstimatedPayQuery>
{
    public const int MaxRangeDays = 93;

    public MyEstimatedPayQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue).WithMessage(Messages.DateRangeInvalid);
        RuleFor(x => x).Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber < MaxRangeDays)
            .When(x => x.From.HasValue && x.To.HasValue && x.To >= x.From)
            .WithMessage(string.Format(Messages.DateRangeTooWide, MaxRangeDays));
    }
}
