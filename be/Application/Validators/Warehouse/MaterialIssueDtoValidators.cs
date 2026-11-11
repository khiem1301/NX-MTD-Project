using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateMaterialIssueDtoValidator : AbstractValidator<CreateMaterialIssueDto>
{
    public CreateMaterialIssueDtoValidator()
    {
        RuleFor(x => x.WarehouseType).Must(t => t is WarehouseTypes.Material or WarehouseTypes.SemiFinished);
        // Lệnh cắt và đơn xin đều tùy chọn (DEV-ALL-99): thiếu thì phải có tổ nhận + đơn hàng; lệnh cắt hoặc đơn xin điền sẵn hai thứ này.
        RuleFor(x => x).Must(x => x.WorkOrderId is not null || x.RequestId is not null
                || (x.OrderId is not null && x.ReceivingTeamId is not null))
            .WithName(nameof(CreateMaterialIssueDto.OrderId)).WithMessage(WarehouseMessages.IssueTargetRequired);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= RequisitionLimits.MaxLines)
            .WithMessage(string.Format(WarehouseMessages.IssueTooManyLines, RequisitionLimits.MaxLines));
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.IssuedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive)
                .LessThanOrEqualTo(RequisitionLimits.MaxMaterialQty).WithMessage(WarehouseMessages.QtyTooLarge));
        // Cột DECIMAL(18,3): không nhận quá 3 chữ số sau dấu phẩy; vải 2 số, NVL khác số nguyên được kiểm tiếp ở service (cần loại NVL).
        RuleForEach(x => x.Lines)
            .Must(l => decimal.Round(l.IssuedQty, 3) == l.IssuedQty)
            .WithMessage(WarehouseMessages.QtyTooManyDecimals);
        // BTP đếm theo bộ: số nguyên từ 1 đến 1.000.000.
        RuleForEach(x => x.Lines)
            .Must(l => l.IssuedQty >= 1 && l.IssuedQty <= RequisitionLimits.MaxPieceQty && l.IssuedQty == decimal.Truncate(l.IssuedQty))
            .When(x => x.WarehouseType == WarehouseTypes.SemiFinished)
            .WithMessage(string.Format(WarehouseMessages.IssuePieceQtyInvalid, RequisitionLimits.MaxPieceQty));
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
            line.RuleFor(l => l.DiscrepancyReason).MaximumLength(TextLimits.Reason);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialIssueLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
