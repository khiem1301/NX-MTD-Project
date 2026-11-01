using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class PendingMaterialOrderQueryValidator : AbstractValidator<PendingMaterialOrderQuery>
{
    public PendingMaterialOrderQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Số trang phải từ 1 trở lên.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang phải từ 1 đến {PageQuery.MaxPageSize}.");
        RuleFor(x => x.Keyword).MaximumLength(100).WithMessage("Từ khóa tối đa 100 ký tự.");
        RuleFor(x => x.MaterialState).Must(s => PendingMaterialStates.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.MaterialState)).WithMessage("Tình trạng NVL không hợp lệ.");
    }
}

public sealed class OrderMaterialPageQueryValidator : AbstractValidator<OrderMaterialPageQuery>
{
    public OrderMaterialPageQueryValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Số trang phải từ 1 trở lên.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang phải từ 1 đến {PageQuery.MaxPageSize}.");
        RuleFor(x => x.Keyword).MaximumLength(100).WithMessage("Từ khóa tối đa 100 ký tự.");
        RuleFor(x => x.Supply).Must(s => OrderMaterialSupplies.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Supply)).WithMessage("Nguồn cấp không hợp lệ.");
        RuleFor(x => x.ReceiptState).Must(s => OrderMaterialReceiptStates.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.ReceiptState)).WithMessage("Tình trạng nhận không hợp lệ.");
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
