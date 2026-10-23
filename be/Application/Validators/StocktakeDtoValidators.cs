using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveStocktakeDtoValidator : AbstractValidator<SaveStocktakeDto>
{
    private const int MaxLines = 1000;

    public SaveStocktakeDtoValidator()
    {
        RuleFor(x => x.WarehouseType)
            .Must(w => w is WarehouseTypes.Material or WarehouseTypes.SemiFinished or WarehouseTypes.Finished)
            .WithMessage(WarehouseMessages.StocktakeWarehouseInvalid);
        RuleFor(x => x.StocktakeDate).NotEmpty();
        RuleFor(x => x.Scope).MaximumLength(300);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l is null || l.Count <= MaxLines).WithMessage(WarehouseMessages.StocktakeTooManyLines);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.CountedQty).GreaterThanOrEqualTo(0).When(l => l.CountedQty is not null)
                .WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.Note).MaximumLength(300);
        });
    }
}
