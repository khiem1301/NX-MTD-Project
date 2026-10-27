using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class OrderMaterialInputValidator : AbstractValidator<IOrderMaterialInput>
{
    public OrderMaterialInputValidator()
    {
        RuleFor(x => x.MaterialId).GreaterThan(0);
        RuleFor(x => x.RequiredQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.CustomerQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        // CK_OM_Delivery: khách có cấp thì bắt buộc khai số lần giao (>= 1); khách không cấp thì để trống.
        RuleFor(x => x.ExpectedDeliveryCount).NotNull().GreaterThanOrEqualTo((short)1).When(x => x.CustomerQty > 0)
            .WithMessage(WarehouseMessages.CustomerDeliveryCountRequired);
        RuleFor(x => x.ExpectedDeliveryCount).Null().When(x => x.CustomerQty == 0)
            .WithMessage(WarehouseMessages.CustomerDeliveryCountNotAllowed);
        RuleFor(x => x).Must(x => x.RequiredQty > 0 || x.CustomerQty > 0).WithName("RequiredQty")
            .WithMessage(WarehouseMessages.OrderMaterialQtyRequired);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CreateOrderMaterialDtoValidator : AbstractValidator<CreateOrderMaterialDto>
{
    public CreateOrderMaterialDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        Include(new OrderMaterialInputValidator());
    }
}

public sealed class UpdateOrderMaterialDtoValidator : AbstractValidator<UpdateOrderMaterialDto>
{
    public UpdateOrderMaterialDtoValidator() => Include(new OrderMaterialInputValidator());
}
