using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SaveCustomerDtoValidator : AbstractValidator<SaveCustomerDto>
{
    public SaveCustomerDtoValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().WithMessage(Messages.Required("tên khách hàng")).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(15).Matches(@"^[0-9+]*$").WithMessage("Số điện thoại chỉ gồm chữ số và dấu +.");
        RuleFor(x => x.Email).MaximumLength(100).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.DefaultAddress).MaximumLength(500);
    }
}

public sealed class SaveProductDtoValidator : AbstractValidator<SaveProductDto>
{
    public SaveProductDtoValidator()
    {
        RuleFor(x => x.ProductCode).NotEmpty().WithMessage(Messages.Required("mã hàng")).MaximumLength(30)
            .Matches(@"^[A-Za-z0-9._-]+$").WithMessage("Mã hàng chỉ gồm chữ, số và . _ -");
        RuleFor(x => x.ProductName).NotEmpty().WithMessage(Messages.Required("tên mã hàng")).MaximumLength(200);
        RuleFor(x => x.SampleImageUrl).MaximumLength(500)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(x => !string.IsNullOrWhiteSpace(x.SampleImageUrl)).WithMessage("Ảnh mẫu phải là đường dẫn http hoặc https.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class OrderLineInputValidator : AbstractValidator<OrderLineInput>
{
    public OrderLineInputValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Color).NotEmpty().WithMessage(Messages.Required("màu")).MaximumLength(50);
        RuleFor(x => x.SizeCode).NotEmpty().WithMessage(Messages.Required("size")).MaximumLength(10)
            .Matches(@"^[A-Za-z0-9]+$").WithMessage("Size chỉ gồm chữ và số.");
        RuleFor(x => x.OrderedQty).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.CmtUnitPrice).InclusiveBetween(0, 100_000_000m)
            .PrecisionScale(18, 2, false).WithMessage("Đơn giá tối đa 2 số lẻ.");
    }
}

/// <summary>Luật chung của tạo / sửa đơn hàng.</summary>
public abstract class OrderInputValidatorBase<T> : AbstractValidator<T> where T : IOrderInput
{
    protected OrderInputValidatorBase()
    {
        RuleFor(x => x.OrderCode).NotEmpty().WithMessage(Messages.Required("mã đơn hàng")).MaximumLength(30)
            .Matches(@"^[A-Za-z0-9._-]+$").WithMessage("Mã đơn hàng chỉ gồm chữ, số và . _ -");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage(Messages.Required("khách hàng"));
        RuleFor(x => x.DueDate).Must((x, d) => d >= x.ReceivedDate).WithMessage("Ngày giao phải sau hoặc bằng ngày nhận.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Vui lòng nhập ít nhất một dòng màu × size.");
        RuleForEach(x => x.Lines).SetValidator(new OrderLineInputValidator());
        RuleFor(x => x.Lines).Custom((list, ctx) =>
        {
            var seen = new HashSet<(int, string, string)>();
            foreach (var l in list)
            {
                if (!seen.Add((l.ProductId, l.Color.Trim().ToLowerInvariant(), l.SizeCode.Trim().ToUpperInvariant())))
                    ctx.AddFailure($"Dòng mã hàng #{l.ProductId} / {l.Color} / {l.SizeCode} bị lặp.");
            }
        });
    }
}

public sealed class CreateOrderDtoValidator : OrderInputValidatorBase<CreateOrderDto> { }

public sealed class UpdateOrderDtoValidator : OrderInputValidatorBase<UpdateOrderDto>
{
    public UpdateOrderDtoValidator() => RuleFor(x => x.RowVer).NotEmpty();
}

public sealed class StopOrderDtoValidator : AbstractValidator<StopOrderDto>
{
    public StopOrderDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(500);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class CancelOrderDtoValidator : AbstractValidator<CancelOrderDto>
{
    public CancelOrderDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ProductQueryValidator : PageQueryValidatorBase<ProductQuery> { }

public sealed class OrderLookupQueryValidator : PageQueryValidatorBase<OrderLookupQuery> { }

public sealed class OrderQueryValidator : PageQueryValidatorBase<OrderQuery>
{
    public OrderQueryValidator() =>
        RuleFor(x => x.DueTo).Must((x, to) => to >= x.DueFrom).When(x => x.DueFrom is not null && x.DueTo is not null)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
}
