using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Services;

namespace NX_MTD.Application.Validators;

public sealed class MealSettingsDtoValidator : AbstractValidator<MealSettingsDto>
{
    public MealSettingsDtoValidator()
    {
        RuleFor(x => x.ReminderTime).Must(MealSettingsService.IsValidTime).WithMessage(Messages.MealSettingsTimeInvalid);
        RuleFor(x => x.CutoffTime).Must(MealSettingsService.IsValidTime).WithMessage(Messages.MealSettingsTimeInvalid);
    }
}
