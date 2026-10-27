using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveCuttingLayDtoValidator : AbstractValidator<SaveCuttingLayDto>
{
    public SaveCuttingLayDtoValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0);
        RuleFor(x => x.ShiftCode).Must(s => ShiftCodes.All.Contains(s));
        RuleFor(x => x.FabricMaterialId).GreaterThan(0).WithMessage(WarehouseMessages.FabricRequired);
        RuleFor(x => x.PlyCount).InclusiveBetween(1, short.MaxValue);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).GreaterThan(0);
            line.RuleFor(l => l.RatioValue).InclusiveBetween(1, short.MaxValue);
            line.RuleFor(l => l.ActualQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.AdjustReason).MaximumLength(500);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.ProductComponentMaterialId)).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class CreateCutHandoverDtoValidator : AbstractValidator<CreateCutHandoverDto>
{
    public CreateCutHandoverDtoValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.HandedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive));
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.ProductComponentMaterialId)).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmCutHandoverDtoValidator : AbstractValidator<ConfirmCutHandoverDto>
{
    public ConfirmCutHandoverDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(500);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.CutHandoverLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
