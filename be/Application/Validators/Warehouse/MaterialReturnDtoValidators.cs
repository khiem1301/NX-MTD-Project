using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialReturnDtoValidator : AbstractValidator<CreateMaterialReturnDto>
{
    public CreateMaterialReturnDtoValidator()
    {
        RuleFor(x => x.MaterialIssueId).GreaterThan(0).WithMessage(WarehouseMessages.ReturnIssueRequired);
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.ReturnedQty).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive)
                .PrecisionScale(18, 3, false).WithMessage(WarehouseMessages.ReturnQtyDecimals));   // cột DECIMAL(18,3)
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
            line.RuleFor(l => l.ConfirmedQty).Cascade(CascadeMode.Stop)
                .GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative)
                .PrecisionScale(18, 3, false).WithMessage(WarehouseMessages.ConfirmedQtyDecimals);
            line.RuleFor(l => l.DiscrepancyReason).MaxTrimmedLength(TextLimits.Reason, "Lý do lệch");
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialReturnLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
