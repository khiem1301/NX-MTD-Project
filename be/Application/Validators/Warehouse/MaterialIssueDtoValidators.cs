using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialIssueDtoValidator : AbstractValidator<CreateMaterialIssueDto>
{
    public CreateMaterialIssueDtoValidator()
    {
        RuleFor(x => x.WarehouseType).Must(t => t is WarehouseTypes.Material or WarehouseTypes.SemiFinished);
        RuleFor(x => x.WorkOrderId).NotNull().When(x => x.WarehouseType == WarehouseTypes.Material)
            .WithMessage(WarehouseMessages.WorkOrderRequiredForMaterial);
        RuleFor(x => x.OrderId).NotNull().When(x => x.WarehouseType == WarehouseTypes.SemiFinished);
        RuleFor(x => x.ReceivingTeamId).NotNull().When(x => x.WarehouseType == WarehouseTypes.SemiFinished);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.IssuedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive));
        // CK_MIL_Target / THROW 50045: phiếu cấp NVL chỉ có dòng NVL, phiếu cấp BTP chỉ có dòng BTP.
        RuleForEach(x => x.Lines)
            .Must(l => l.MaterialId > 0 && l.OrderLineId is null && l.ProductComponentMaterialId is null)
            .When(x => x.WarehouseType == WarehouseTypes.Material)
            .WithMessage(WarehouseMessages.IssueLineMaterialOnly);
        RuleForEach(x => x.Lines)
            .Must(l => l.OrderLineId > 0 && l.MaterialId is null)
            .When(x => x.WarehouseType == WarehouseTypes.SemiFinished)
            .WithMessage(WarehouseMessages.IssueLinePieceOnly);
        RuleFor(x => x.Lines)
            .Must(l => l is null || l.Select(x => (x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId))
                .Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmIssueDtoValidator : AbstractValidator<ConfirmIssueDto>
{
    public ConfirmIssueDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(500);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialIssueLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
