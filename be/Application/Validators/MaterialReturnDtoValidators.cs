using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialReturnDtoValidator : AbstractValidator<CreateMaterialReturnDto>
{
    public CreateMaterialReturnDtoValidator()
    {
        RuleFor(x => x.MaterialIssueId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.ReturnedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive));
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialIssueLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmMaterialReturnDtoValidator : AbstractValidator<ConfirmMaterialReturnDto>
{
    public ConfirmMaterialReturnDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(500);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialReturnLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
