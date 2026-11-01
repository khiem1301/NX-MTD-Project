using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveProductSpecDtoValidator : AbstractValidator<SaveProductSpecDto>
{
    public SaveProductSpecDtoValidator()
    {
        RuleForEach(x => x.Components).ChildRules(c =>
        {
            c.RuleFor(i => i.ComponentName).NotEmpty().MaximumLength(100);
            c.RuleFor(i => CodeText.ToCode(i.ComponentType)).Must(t => t is ComponentTypes.Panel or ComponentTypes.Trim)
                .OverridePropertyName("componentType").WithMessage("Loại thành phần phải là Panel hoặc Trim.");
            c.RuleFor(i => i.PiecesPerGarment).GreaterThan((short)0);
        });
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(i => i.Color).NotEmpty().MaximumLength(50);
            v.RuleFor(i => i.SizeCode).NotEmpty().MaximumLength(10);
            v.RuleFor(i => i.LengthCm).GreaterThan(0).When(i => i.LengthCm is not null);
            v.RuleFor(i => i.WidthCm).GreaterThan(0).When(i => i.WidthCm is not null);
        });
        RuleForEach(x => x.Norms).ChildRules(n =>
        {
            n.RuleFor(i => i.ComponentName).NotEmpty();
            n.RuleFor(i => i.Color).NotEmpty();
            n.RuleFor(i => i.SizeCode).NotEmpty();
            n.RuleFor(i => i.MaterialId).GreaterThan(0);
            n.RuleFor(i => i.LengthCm).GreaterThan(0).When(i => i.LengthCm is not null);
            n.RuleFor(i => i.WidthCm).GreaterThan(0).When(i => i.WidthCm is not null);
            n.RuleFor(i => i.SpecNote).MaximumLength(200);
        });
    }
}
