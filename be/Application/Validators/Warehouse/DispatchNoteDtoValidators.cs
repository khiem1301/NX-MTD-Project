using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class CreateDispatchNoteDtoValidator : AbstractValidator<CreateDispatchNoteDto>
{
    public CreateDispatchNoteDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.DeliveryDate).NotEmpty().WithMessage(WarehouseMessages.DeliveryDateRequired);
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage(WarehouseMessages.DeliveryAddressRequired).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.IssueQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive));
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.OrderLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmDeliveryDtoValidator : AbstractValidator<ConfirmDeliveryDto>
{
    public ConfirmDeliveryDtoValidator()
    {
        RuleFor(x => x.CustomerReceivedDate).NotEmpty().WithMessage(WarehouseMessages.CustomerReceivedDateRequired);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.ReceivedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative));
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.DispatchNoteLineId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}
