using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveReceiptLineDtoValidator : AbstractValidator<SaveReceiptLineDto>
{
    public SaveReceiptLineDtoValidator()
    {
        // Khách cấp chọn NVL của đơn, nhà cung cấp chọn dòng đơn mua: dịch vụ xác định đúng loại theo Source.
        RuleFor(x => x).Must(x => (x.OrderMaterialId ?? 0) > 0 || (x.PurchaseOrderLineId ?? 0) > 0)
            .WithMessage(WarehouseMessages.ReceiptLineTarget).OverridePropertyName("orderMaterialId");
        // 0 = lô chưa giao / hỏng cả lô: vẫn giữ dòng để gắn yêu cầu thiếu NVL (CK_MRL_Qty cho phép 0).
        RuleFor(x => x.AcceptedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
    }
}

public sealed class SaveMaterialReceiptDtoValidator : AbstractValidator<SaveMaterialReceiptDto>
{
    public SaveMaterialReceiptDtoValidator()
    {
        RuleFor(x => x.Source).Must(s => s is MaterialSources.Customer or MaterialSources.Supplier)
            .WithMessage(WarehouseMessages.SourceInvalid);
        RuleFor(x => x.OrderId).NotNull().GreaterThan(0).When(x => x.Source == MaterialSources.Customer)
            .WithMessage(WarehouseMessages.OrderRequired);
        RuleFor(x => x.PurchaseOrderId).NotNull().GreaterThan(0).When(x => x.Source == MaterialSources.Supplier)
            .WithMessage(WarehouseMessages.PurchaseOrderRequired);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new SaveReceiptLineDtoValidator());
        RuleFor(x => x.Lines).Must(l => l is null || l.Any(x => x.AcceptedQty > 0))
            .WithMessage(WarehouseMessages.QtyMustBePositive).OverridePropertyName("acceptedQty");
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.PurchaseOrderLineId ?? -x.OrderMaterialId ?? 0).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class SupplementaryLineDtoValidator : AbstractValidator<SupplementaryLineDto>
{
    public SupplementaryLineDtoValidator()
    {
        RuleFor(x => x.OriginalReceiptLineId).GreaterThan(0);
        RuleFor(x => x.AcceptedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
    }
}

public sealed class SaveSupplementaryReceiptDtoValidator : AbstractValidator<SaveSupplementaryReceiptDto>
{
    public SaveSupplementaryReceiptDtoValidator()
    {
        RuleFor(x => x.ShortageRequestId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new SupplementaryLineDtoValidator());
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.OriginalReceiptLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
        RuleFor(x => x.Lines).Must(l => l is null || l.Any(x => x.AcceptedQty > 0))
            .WithMessage(WarehouseMessages.QtyMustBePositive).OverridePropertyName("acceptedQty");
    }
}

public sealed class ResolveShortDeliveryDtoValidator : AbstractValidator<ResolveShortDeliveryDto>
{
    public ResolveShortDeliveryDtoValidator()
    {
        RuleFor(x => x.Resolution).Must(r => r is ShortageResolutions.Replenish or ShortageResolutions.NoReplenish)
            .WithMessage(WarehouseMessages.ShortageResolutionInvalid);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.ReplenishQty).GreaterThanOrEqualTo(0)
            .WithMessage(WarehouseMessages.QtyMustNotBeNegative));
    }
}

public sealed class ReasonDtoValidator : AbstractValidator<ReasonDto>
{
    public ReasonDtoValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
}
