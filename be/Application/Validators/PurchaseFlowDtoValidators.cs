using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

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
        RuleFor(x => x.SupplierName).NotEmpty().WithMessage(Messages.Required("tên nhà cung cấp")).MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().WithMessage(Messages.Required("số điện thoại"))
            .MaximumLength(15).Matches("^[0-9+]*$").WithMessage("Số điện thoại chỉ gồm chữ số và dấu +.");
        RuleFor(x => x.Email).NotEmpty().WithMessage(Messages.Required("email"))
            .MaximumLength(100).EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.Address).NotEmpty().WithMessage(Messages.Required("địa chỉ")).MaximumLength(500);
        RuleFor(x => x.MaterialTypes).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500);
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

public sealed class CancelPurchaseOrderDtoValidator : AbstractValidator<CancelPurchaseOrderDto>
{
    public CancelPurchaseOrderDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(500);   // MSG09
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
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(500);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}
