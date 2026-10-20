using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveReceiptLineDtoValidator : AbstractValidator<SaveReceiptLineDto>
{
    public SaveReceiptLineDtoValidator()
    {
        RuleFor(x => x.OrderMaterialId).GreaterThan(0);
        RuleFor(x => x.AcceptedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.ShortQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.DefectiveQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.AcceptedQty + x.ShortQty + x.DefectiveQty).GreaterThan(0)
            .WithMessage(WarehouseMessages.QtyMustBePositive).OverridePropertyName("acceptedQty");
    }
}

public sealed class SaveMaterialReceiptDtoValidator : AbstractValidator<SaveMaterialReceiptDto>
{
    public SaveMaterialReceiptDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Source).Equal(MaterialSources.Customer).WithMessage(WarehouseMessages.SupplierSourceNotSupported);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new SaveReceiptLineDtoValidator());
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.OrderMaterialId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ReasonDtoValidator : AbstractValidator<ReasonDto>
{
    public ReasonDtoValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
}
