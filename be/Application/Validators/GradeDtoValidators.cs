using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveGradeLevelDtoValidator : AbstractValidator<SaveGradeLevelDto>
{
    public SaveGradeLevelDtoValidator()
    {
        RuleFor(x => x.GradeCode).Must(GradeCodes.IsWellFormed).WithMessage(Messages.GradeCodeMalformed);
        RuleFor(x => x.GradeName).NotEmpty().WithMessage("Vui lòng nhập tên bậc.").MaximumLength(50);
        RuleFor(x => x.MonthlyAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền bậc không được âm.");
        RuleFor(x => x.EffectiveDate).NotEmpty();
    }
}

public sealed class SaveGradeCodeDtoValidator : AbstractValidator<SaveGradeCodeDto>
{
    public SaveGradeCodeDtoValidator()
    {
        RuleFor(x => x.GradeCode).Must(c => GradeCodes.IsWellFormed(c?.ToUpperInvariant())).WithMessage(Messages.GradeCodeMalformed);
        RuleFor(x => x.GradeName).NotEmpty().WithMessage("Vui lòng nhập tên bậc.").MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo((short)0);
    }
}

public sealed class UpdateGradeCodeDtoValidator : AbstractValidator<UpdateGradeCodeDto>
{
    public UpdateGradeCodeDtoValidator()
    {
        RuleFor(x => x.GradeName).NotEmpty().WithMessage("Vui lòng nhập tên bậc.").MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo((short)0);
        RuleFor(x => x.Status).Must(s => s is "Active" or "Inactive").WithMessage(Messages.GradeCodeStatusInvalid);
    }
}

public sealed class UpdateGradeLevelDtoValidator : AbstractValidator<UpdateGradeLevelDto>
{
    public UpdateGradeLevelDtoValidator()
    {
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
        RuleFor(x => x.ProposedGradeCode).Must(GradeCodes.IsWellFormed).WithMessage(Messages.GradeCodeMalformed);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
    }
}

public sealed class SetGradeDtoValidator : AbstractValidator<SetGradeDto>
{
    public SetGradeDtoValidator()
    {
        RuleFor(x => x.WorkerId).GreaterThan(0);
        RuleFor(x => x.GradeCode).Must(c => GradeCodes.IsWellFormed(c?.ToUpperInvariant())).WithMessage(Messages.GradeCodeMalformed);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
    }
}

public sealed class ApplyGradeDtoValidator : AbstractValidator<ApplyGradeDto>
{
    public ApplyGradeDtoValidator() => RuleFor(x => x.DecisionNote).MaximumLength(500);
}
