using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SetFinalAmountDtoValidator : AbstractValidator<SetFinalAmountDto>
{
    public SetFinalAmountDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.FinalAmountReasonRequired).MaximumLength(TextLimits.Reason);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class AddOrderPaymentDtoValidator : AbstractValidator<AddOrderPaymentDto>
{
    public AddOrderPaymentDtoValidator()
    {
        RuleFor(x => x.PaymentMethod).NotEmpty().WithMessage(Messages.PaymentMethodInvalid)
            .Must(m => m is "BankTransfer" or "Cash").WithMessage(Messages.PaymentMethodInvalid);
        RuleFor(x => x.Amount).NotEqual(0).WithMessage(Messages.OrderPaymentAmountInvalid);
        RuleFor(x => x.PaymentNote).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class UpdateOrderPaymentDtoValidator : AbstractValidator<UpdateOrderPaymentDto>
{
    public UpdateOrderPaymentDtoValidator()
    {
        RuleFor(x => x.PaymentMethod).NotEmpty().WithMessage(Messages.PaymentMethodInvalid)
            .Must(m => m is "BankTransfer" or "Cash").WithMessage(Messages.PaymentMethodInvalid);
        RuleFor(x => x.Amount).NotEqual(0).WithMessage(Messages.OrderPaymentAmountInvalid);
        RuleFor(x => x.PaymentNote).MaximumLength(500);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.PaymentEditReasonRequired).MaximumLength(TextLimits.Reason);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}
