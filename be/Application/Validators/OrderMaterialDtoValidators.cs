using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SaveNormDtoValidator : AbstractValidator<SaveNormDto>
{
    public SaveNormDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ComponentName).MaximumLength(100);
        RuleFor(x => x.GarmentColor).MaximumLength(50);
        RuleFor(x => x.SizeCode).MaximumLength(10);
        RuleFor(x => x.QtyPerPiece).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive);
        RuleFor(x => x.WastagePercent).InclusiveBetween(0, 100);
    }
}

public sealed class OrderMaterialInputValidator : AbstractValidator<IOrderMaterialInput>
{
    public OrderMaterialInputValidator()
    {
        RuleFor(x => x.MaterialId).GreaterThan(0);
        RuleFor(x => x.MaterialColor).MaximumLength(50);
        RuleFor(x => x.RequiredQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.CustomerQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.CustomerExpectedDate).NotNull().When(x => x.CustomerQty > 0)
            .WithMessage(WarehouseMessages.CustomerDateRequired);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleForEach(x => x.Norms).SetValidator(new SaveNormDtoValidator());
        RuleFor(x => x.Norms).Must(HaveUniqueKeys).WithMessage(WarehouseMessages.DuplicateLine);
    }

    private static bool HaveUniqueKeys(IReadOnlyList<SaveNormDto>? norms) =>
        norms is null || norms.Select(n => (n.ProductId, Key(n.ComponentName), Key(n.GarmentColor), Key(n.SizeCode)))
            .Distinct().Count() == norms.Count;

    private static string Key(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
}

public sealed class CreateOrderMaterialDtoValidator : AbstractValidator<CreateOrderMaterialDto>
{
    public CreateOrderMaterialDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        Include(new OrderMaterialInputValidator());
    }
}

public sealed class UpdateOrderMaterialDtoValidator : AbstractValidator<UpdateOrderMaterialDto>
{
    public UpdateOrderMaterialDtoValidator() => Include(new OrderMaterialInputValidator());
}
