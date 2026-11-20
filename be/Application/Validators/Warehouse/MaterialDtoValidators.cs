using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

/// <summary>Tên NVL là định danh: chỉ chữ không dấu, số, khoảng trắng và . _ - (service Trim + chữ hoa trước khi lưu).</summary>
internal static class MaterialNamePattern
{
    private static readonly System.Text.RegularExpressions.Regex Regex = new(@"^[A-Za-z0-9._ -]+$");
    public static bool IsMatch(string value) => Regex.IsMatch(value);
}

public sealed class CreateMaterialDtoValidator : AbstractValidator<CreateMaterialDto>
{
    public CreateMaterialDtoValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200)
            .Must(n => MaterialNamePattern.IsMatch(n.Trim())).WithMessage(Messages.MaterialNameFormat);
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
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200)
            .Must(n => MaterialNamePattern.IsMatch(n.Trim())).WithMessage(Messages.MaterialNameFormat);
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
