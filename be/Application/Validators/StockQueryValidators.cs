using FluentValidation;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

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
