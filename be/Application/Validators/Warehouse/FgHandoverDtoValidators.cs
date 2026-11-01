using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class CreateFgHandoverDtoValidator : AbstractValidator<CreateFgHandoverDto>
{
    public CreateFgHandoverDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.HandedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive));
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.OrderLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmFgHandoverDtoValidator : AbstractValidator<ConfirmFgHandoverDto>
{
    public ConfirmFgHandoverDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(500);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.FgHandoverLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
