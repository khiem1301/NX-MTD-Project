using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialDtoValidator : AbstractValidator<CreateMaterialDto>
{
    public CreateMaterialDtoValidator()
    {
        RuleFor(x => x.MaterialCode).MaximumLength(30);
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialType).NotEmpty().Must(t => MaterialTypes.Categories.ContainsKey(t));
        RuleFor(x => x.Category).NotEmpty()
            .Must((dto, c) => MaterialTypes.IsValidCategory(dto.MaterialType, c))
            .WithMessage(WarehouseMessages.MaterialCategoryInvalid);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateMaterialDtoValidator : AbstractValidator<UpdateMaterialDto>
{
    public UpdateMaterialDtoValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialType).NotEmpty().Must(t => MaterialTypes.Categories.ContainsKey(t));
        RuleFor(x => x.Category).NotEmpty()
            .Must((dto, c) => MaterialTypes.IsValidCategory(dto.MaterialType, c))
            .WithMessage(WarehouseMessages.MaterialCategoryInvalid);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
    }
}

public sealed class ChangeMaterialStatusDtoValidator : AbstractValidator<ChangeMaterialStatusDto>
{
    public ChangeMaterialStatusDtoValidator() =>
        RuleFor(x => x.Status).Must(s => s is MaterialStatuses.Active or MaterialStatuses.Discontinued);
}
