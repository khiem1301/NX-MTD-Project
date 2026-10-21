using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SavePurchaseOrderLineDtoValidator : AbstractValidator<SavePurchaseOrderLineDto>
{
    public SavePurchaseOrderLineDtoValidator()
    {
        RuleFor(x => x.MaterialId).GreaterThan(0).When(x => x.MaterialId.HasValue);
        RuleFor(x => x.NewMaterialName).NotEmpty().MaximumLength(200).When(x => !x.MaterialId.HasValue);
        RuleFor(x => x.NewMaterialUnit).NotEmpty().MaximumLength(20).When(x => !x.MaterialId.HasValue);
        RuleFor(x => x.MaterialColor).MaximumLength(50);
        RuleFor(x => x.OrderedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
    }
}

public sealed class CreatePurchaseOrderDtoValidator : AbstractValidator<CreatePurchaseOrderDto>
{
    public CreatePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId.HasValue);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new SavePurchaseOrderLineDtoValidator());
        RuleFor(x => x.Lines).Must(l => l is null
                || l.Select(x => (x.MaterialId, Name: x.MaterialId.HasValue ? null : x.NewMaterialName?.Trim().ToLowerInvariant(),
                Color: x.MaterialColor?.Trim() ?? string.Empty)).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
