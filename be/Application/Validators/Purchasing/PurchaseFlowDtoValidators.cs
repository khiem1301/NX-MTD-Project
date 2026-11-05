using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SupplierQueryValidator : PageQueryValidatorBase<SupplierQuery>
{
    public SupplierQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is "Active" or "Discontinued")
            .When(x => !string.IsNullOrWhiteSpace(x.Status)).WithMessage("Trạng thái nhà cung cấp không hợp lệ.");
    }
}

public sealed class SaveSupplierDtoValidator : AbstractValidator<SaveSupplierDto>
{
    public SaveSupplierDtoValidator()
    {
        RuleFor(x => x.SupplierName).NotEmpty().WithMessage(Messages.Required("tên nhà cung cấp"))
            .MaxTrimmedLength(200, "Tên nhà cung cấp");
        RuleFor(x => x.Phone).NotEmpty().WithMessage(Messages.Required("số điện thoại"))
            .MustBePhone();
        RuleFor(x => x.Email).NotEmpty().WithMessage(Messages.Required("email"))
            .MaxTrimmedLength(100, "Email").MustBeEmail();
        RuleFor(x => x.Address).NotEmpty().WithMessage(Messages.Required("địa chỉ")).MaxTrimmedLength(500, "Địa chỉ");
        RuleFor(x => x.MaterialTypes).NotEmpty().WithMessage(Messages.Required("loại NVL cung cấp"))
            .MaxTrimmedLength(200, "Loại NVL cung cấp");
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
    }
}

public sealed class PurchaseOrderFlowQueryValidator : PageQueryValidatorBase<PurchaseOrderFlowQuery>
{
    public PurchaseOrderFlowQueryValidator()
    {
        RuleFor(x => x.GoodsStatus).MaximumLength(30);
        RuleFor(x => x.PaymentStatus).MaximumLength(20);
    }
}

public sealed class PlacePurchaseLineDtoValidator : AbstractValidator<PlacePurchaseLineDto>
{
    public PlacePurchaseLineDtoValidator()
    {
        RuleFor(x => x.PurchaseOrderLineId).GreaterThan(0);
        RuleFor(x => x.OrderedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive);
        RuleFor(x => x.UnitPrice).GreaterThan(0).WithMessage(Messages.PurchaseOrderPriceRequired);
    }
}

public sealed class PlacePurchaseOrderDtoValidator : AbstractValidator<PlacePurchaseOrderDto>
{
    public PlacePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage(Messages.Required("nhà cung cấp"));
        RuleFor(x => x.ExpectedDeliveryCount).GreaterThanOrEqualTo((short)1).WithMessage(Messages.PurchaseOrderDeliveryCountInvalid);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new PlacePurchaseLineDtoValidator());
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class DiscontinueSupplierDtoValidator : AbstractValidator<DiscontinueSupplierDto>
{
    public DiscontinueSupplierDtoValidator() =>
        RuleFor(x => x.Reason).MaxTrimmedLength(TextLimits.Reason, "Lý do");
}

public sealed class CancelPurchaseOrderDtoValidator : AbstractValidator<CancelPurchaseOrderDto>
{
    public CancelPurchaseOrderDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(TextLimits.Reason);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class RepricePurchaseLineDtoValidator : AbstractValidator<RepricePurchaseLineDto>
{
    public RepricePurchaseLineDtoValidator()
    {
        RuleFor(x => x.PurchaseOrderLineId).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThan(0).WithMessage(Messages.PurchaseOrderPriceRequired);
    }
}

public sealed class RepricePurchaseOrderDtoValidator : AbstractValidator<RepricePurchaseOrderDto>
{
    public RepricePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new RepricePurchaseLineDtoValidator());
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ConfirmPaymentDtoValidator : AbstractValidator<ConfirmPaymentDto>
{
    public ConfirmPaymentDtoValidator()
    {
        RuleFor(x => x.PaymentMethod).NotEmpty().WithMessage(Messages.PaymentMethodInvalid)
            .Must(m => m is "BankTransfer" or "Cash").WithMessage(Messages.PaymentMethodInvalid);
        RuleFor(x => x.PaidAmount).GreaterThan(0).WithMessage(Messages.PaymentAmountInvalid);
        RuleFor(x => x.PaymentNote).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ReturnPaymentDtoValidator : AbstractValidator<ReturnPaymentDto>
{
    public ReturnPaymentDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(TextLimits.Reason);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}
