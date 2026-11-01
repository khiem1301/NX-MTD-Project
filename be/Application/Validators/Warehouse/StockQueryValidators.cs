using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class PendingMaterialOrderQueryValidator : AbstractValidator<PendingMaterialOrderQuery>
{
    public PendingMaterialOrderQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.MaterialState).Must(s => PendingMaterialStates.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.MaterialState));
    }
}

public sealed class OrderMaterialPageQueryValidator : AbstractValidator<OrderMaterialPageQuery>
{
    public OrderMaterialPageQueryValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.Supply).Must(s => OrderMaterialSupplies.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Supply));
        RuleFor(x => x.ReceiptState).Must(s => OrderMaterialReceiptStates.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.ReceiptState));
    }
}

public sealed class StockBalanceQueryValidator : AbstractValidator<StockBalanceQuery>
{
    public StockBalanceQueryValidator() =>
        RuleFor(x => x.WarehouseType).Must(t => WarehouseTypes.All.Contains(t));
}

public sealed class StockCardQueryValidator : AbstractValidator<StockCardQuery>
{
    public StockCardQueryValidator()
    {
        RuleFor(x => x.WarehouseType).Must(t => WarehouseTypes.All.Contains(t));
        RuleFor(x => x.MaterialId).NotNull().When(x => x.WarehouseType == WarehouseTypes.Material);
        RuleFor(x => x.OrderLineId).NotNull().When(x => x.WarehouseType != WarehouseTypes.Material);
    }
}
