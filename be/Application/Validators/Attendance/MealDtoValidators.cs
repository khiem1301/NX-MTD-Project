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
        // MealCount bỏ qua — số suất lấy từ checklist WillEat.
    }
}

public sealed class SetTeamMemberMealDtoValidator : AbstractValidator<SetTeamMemberMealDto>
{
    public SetTeamMemberMealDtoValidator()
    {
        RuleFor(x => x.MealDate).NotEmpty().WithMessage("Vui lòng chọn ngày.");
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Vui lòng chọn nhân viên.");
    }
}

public sealed class SetMyMealDtoValidator : AbstractValidator<SetMyMealDto>
{
    public SetMyMealDtoValidator()
    {
        // MealDate null = hôm nay (service lấy từ clock).
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
