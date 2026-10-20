using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SaveWorkOrderDtoValidator : AbstractValidator<SaveWorkOrderDto>
{
    public SaveWorkOrderDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.CuttingTeamId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).GreaterThan(0);
            line.RuleFor(l => l.PlannedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.OrderLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
