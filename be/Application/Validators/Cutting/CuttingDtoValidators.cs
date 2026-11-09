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
    /// <summary>Bàn cắt dài nhất, đủ rộng cho bàn cắt thực tế; cột DECIMAL(7,2).</summary>
    public const decimal MaxLayLengthM = 1000m;

    public SaveCuttingLayDtoValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0).WithMessage(WarehouseMessages.LayWorkOrderRequired);
        RuleFor(x => x.WorkDate).NotEqual(default(DateOnly)).WithMessage(WarehouseMessages.LayWorkDateRequired);
        RuleFor(x => x.ShiftCode).Must(s => ShiftCodes.All.Contains(s)).WithMessage(WarehouseMessages.LayShiftInvalid);
        RuleFor(x => x.FabricMaterialId).GreaterThan(0).WithMessage(WarehouseMessages.FabricRequired);
        RuleFor(x => x.PlyCount).InclusiveBetween(1, short.MaxValue).WithMessage(WarehouseMessages.LayPlyCountRange);
        RuleFor(x => x.LayLengthM).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(WarehouseMessages.LayLengthRequired)
            .GreaterThan(0m).WithMessage(WarehouseMessages.LayLengthInvalid)
            .LessThanOrEqualTo(MaxLayLengthM).WithMessage(WarehouseMessages.LayLengthMax)
            .Must(v => decimal.Round(v!.Value, 2) == v.Value).WithMessage(WarehouseMessages.LayLengthDecimals);
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).GreaterThan(0).WithMessage(WarehouseMessages.LayOrderLineRequired);
            line.RuleFor(l => l.RatioValue).InclusiveBetween(1, short.MaxValue).WithMessage(WarehouseMessages.LayRatioRange);
            line.RuleFor(l => l.ActualQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.AdjustReason).MaxTrimmedLength(TextLimits.Reason, "Lý do sửa số");
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
