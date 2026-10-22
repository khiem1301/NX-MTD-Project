using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>Validate định dạng (sai → 400). Khóa sau giờ chốt kiểm ở Service (→ 409, MSG22).</summary>
public sealed class ConfirmTeamMealDtoValidator : AbstractValidator<ConfirmTeamMealDto>
{
    public ConfirmTeamMealDtoValidator()
    {
        RuleFor(x => x.MealDate).NotEmpty().WithMessage("Vui lòng chọn ngày.");
        RuleFor(x => x.MealCount).GreaterThanOrEqualTo(0).WithMessage(Messages.MealCountInvalid);
    }
}

public sealed class SetGuestMealDtoValidator : AbstractValidator<SetGuestMealDto>
{
    public SetGuestMealDtoValidator()
    {
        RuleFor(x => x.MealDate).NotEmpty().WithMessage("Vui lòng chọn ngày.");
        RuleFor(x => x.GuestCount).GreaterThanOrEqualTo(0).WithMessage(Messages.MealCountInvalid);
        RuleFor(x => x.GuestNote).MaximumLength(300).WithMessage("Ghi chú tối đa 300 ký tự.");
    }
}

public sealed class SetMealPriceDtoValidator : AbstractValidator<SetMealPriceDto>
{
    public SetMealPriceDtoValidator()
    {
        RuleFor(x => x.MealDate).NotEmpty().WithMessage("Vui lòng chọn ngày.");
        RuleFor(x => x.MealPrice).GreaterThanOrEqualTo(0).WithMessage(Messages.MealPriceInvalid);
    }
}
