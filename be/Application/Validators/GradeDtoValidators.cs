using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveGradeLevelDtoValidator : AbstractValidator<SaveGradeLevelDto>
{
    public SaveGradeLevelDtoValidator()
    {
        RuleFor(x => x.GradeCode).NotEmpty().Length(1);
        RuleFor(x => x.GradeName).NotEmpty().WithMessage("Vui lòng nhập tên bậc.").MaximumLength(50);
        RuleFor(x => x.MonthlyAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền bậc không được âm.");
        RuleFor(x => x.EffectiveDate).NotEmpty();
    }
}

public sealed class ProposeGradeDtoValidator : AbstractValidator<ProposeGradeDto>
{
    public ProposeGradeDtoValidator()
    {
        RuleFor(x => x.WorkerId).GreaterThan(0).WithMessage("Vui lòng chọn công nhân.");
        RuleFor(x => x.ProposedGradeCode).NotEmpty().Length(1);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
    }
}

public sealed class SetGradeDtoValidator : AbstractValidator<SetGradeDto>
{
    public SetGradeDtoValidator()
    {
        RuleFor(x => x.WorkerId).GreaterThan(0);
        RuleFor(x => x.GradeCode).NotEmpty().Must(c => GradeCodes.All.Contains(c.Trim().ToUpperInvariant())).WithMessage(Messages.GradeCodeInvalid);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
    }
}

public sealed class ApplyGradeDtoValidator : AbstractValidator<ApplyGradeDto>
{
    public ApplyGradeDtoValidator() => RuleFor(x => x.DecisionNote).MaximumLength(500);
}
