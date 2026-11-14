using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialReturnDtoValidator : AbstractValidator<CreateMaterialReturnDto>
{
    public CreateMaterialReturnDtoValidator()
    {
        RuleFor(x => x.WarehouseType).Must(t => t is WarehouseTypes.Material or WarehouseTypes.SemiFinished)
            .WithMessage(WarehouseMessages.ReturnWarehouseNotAllowed);
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= RequisitionLimits.MaxLines)
            .WithMessage(string.Format(WarehouseMessages.ReturnTooManyLines, RequisitionLimits.MaxLines));
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.ReturnedQty).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive)
                .LessThanOrEqualTo(RequisitionLimits.MaxMaterialQty).WithMessage(WarehouseMessages.QtyTooLarge)
                .PrecisionScale(18, 3, false).WithMessage(WarehouseMessages.ReturnQtyDecimals));   // cột DECIMAL(18,3)
        // NVL trả chọn NVL, BTP trả chọn dòng đơn (cả bộ, số nguyên đến trần BTP).
        RuleFor(x => x).Must(x => x.Lines is null || x.Lines.All(l => x.WarehouseType == WarehouseTypes.SemiFinished
                ? l.OrderLineId is not null && l.MaterialId is null
                : l.MaterialId is not null && l.OrderLineId is null))
            .WithMessage(WarehouseMessages.ReturnLineKindInvalid).OverridePropertyName(nameof(CreateMaterialReturnDto.Lines));
        RuleFor(x => x).Must(x => x.Lines is null || x.WarehouseType != WarehouseTypes.SemiFinished || x.Lines.All(l =>
                l.ReturnedQty == decimal.Truncate(l.ReturnedQty) && l.ReturnedQty <= RequisitionLimits.MaxPieceQty))
            .WithMessage(WarehouseMessages.ReturnPieceQtyInvalid).OverridePropertyName(nameof(CreateMaterialReturnDto.Lines));
        RuleFor(x => x.Lines).Must(l => l is null
                || l.Select(x => (x.MaterialId, x.OrderLineId)).Distinct().Count() == l.Count)
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
            line.RuleFor(l => l.ConfirmedQty).Cascade(CascadeMode.Stop)
                .GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative)
                .PrecisionScale(18, 3, false).WithMessage(WarehouseMessages.ConfirmedQtyDecimals);
            line.RuleFor(l => l.DiscrepancyReason).MaxTrimmedLength(TextLimits.Reason, "Lý do lệch");
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialReturnLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
