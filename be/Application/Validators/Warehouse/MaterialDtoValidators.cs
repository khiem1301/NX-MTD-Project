using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialDtoValidator : AbstractValidator<CreateMaterialDto>
{
    public CreateMaterialDtoValidator()
    {
        // Để trống thì hệ thống tự sinh; nhập thì chỉ chữ không dấu, số và . _ - (service Trim + chữ hoa trước khi lưu).
        RuleFor(x => x.MaterialCode!.Trim()).MaximumLength(MaterialLimits.MaxCodeLength)
            .Must(CodePattern.IsMatch).WithMessage(Messages.MaterialCodeFormat)
            .OverridePropertyName(nameof(CreateMaterialDto.MaterialCode))
            .When(x => !string.IsNullOrWhiteSpace(x.MaterialCode));
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(MaterialLimits.MaxNameLength);
        RuleFor(x => x.MaterialType).NotEmpty().Must(MaterialTypes.IsValid);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.Unit).NotEmpty().When(x => x.MaterialType != MaterialTypes.Fabric);
        RuleFor(x => x.Color).MaximumLength(50);
        RuleFor(x => x.WidthM).ValidWidthM(WidthLabels.Fabric).When(x => x.MaterialType == MaterialTypes.Fabric);
    }
}

public sealed class UpdateMaterialDtoValidator : AbstractValidator<UpdateMaterialDto>
{
    public UpdateMaterialDtoValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(MaterialLimits.MaxNameLength);
        RuleFor(x => x.MaterialType).NotEmpty().Must(MaterialTypes.IsValid);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.Unit).NotEmpty().When(x => x.MaterialType != MaterialTypes.Fabric);
        RuleFor(x => x.Color).MaximumLength(50);
        RuleFor(x => x.WidthM).ValidWidthM(WidthLabels.Fabric).When(x => x.MaterialType == MaterialTypes.Fabric);
    }
}

public sealed class ChangeMaterialStatusDtoValidator : AbstractValidator<ChangeMaterialStatusDto>
{
    public ChangeMaterialStatusDtoValidator() =>
        RuleFor(x => x.Status).Must(s => s is MaterialStatuses.Active or MaterialStatuses.Discontinued);
}
