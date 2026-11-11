using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CuttingLayQueryValidator : PageQueryValidatorBase<CuttingLayQuery>
{
    public CuttingLayQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => CuttingLayStatuses.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage(WarehouseMessages.LayStatusInvalid);
    }
}

public sealed class SaveCuttingLayDtoValidator : AbstractValidator<SaveCuttingLayDto>
{
    /// <summary>Số mét vải tối đa của một bàn cắt; cột DECIMAL(9,2).</summary>
    public const decimal MaxFabricUsedM = 10000m;

    /// <summary>Số lượng sau cắt tối đa của một dòng màu - size.</summary>
    public const int MaxLineQty = 100000;

    public SaveCuttingLayDtoValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0).WithMessage(WarehouseMessages.LayWorkOrderRequired);
        RuleFor(x => x.WorkDate).NotEqual(default(DateOnly)).WithMessage(WarehouseMessages.LayWorkDateRequired);
        RuleFor(x => x.ShiftCode).Must(s => ShiftCodes.All.Contains(s)).WithMessage(WarehouseMessages.LayShiftInvalid);
        RuleFor(x => x.Fabrics).NotEmpty().WithMessage(WarehouseMessages.FabricRequired);
        RuleForEach(x => x.Fabrics).ChildRules(f =>
        {
            f.RuleFor(l => l.MaterialId).GreaterThan(0).WithMessage(WarehouseMessages.FabricRequired);
            f.RuleFor(l => l.UsedM).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(WarehouseMessages.LayFabricUsedRequired)
                .GreaterThan(0m).WithMessage(WarehouseMessages.LayFabricUsedInvalid)
                .LessThanOrEqualTo(MaxFabricUsedM).WithMessage(WarehouseMessages.LayFabricUsedMax)
                .Must(v => decimal.Round(v!.Value, 2) == v.Value).WithMessage(WarehouseMessages.LayFabricUsedDecimals);
        });
        RuleFor(x => x.Fabrics).Must(f => f is null || f.Select(x => x.MaterialId).Distinct().Count() == f.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LayQtyRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).GreaterThan(0).WithMessage(WarehouseMessages.LayOrderLineRequired);
            line.RuleFor(l => l.ActualQty).InclusiveBetween(1, MaxLineQty).WithMessage(WarehouseMessages.LayQtyRange);
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
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(TextLimits.Reason);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.CutHandoverLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
