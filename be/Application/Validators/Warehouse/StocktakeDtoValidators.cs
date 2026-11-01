using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveStocktakeDtoValidator : AbstractValidator<SaveStocktakeDto>
{
    private const int MaxLines = 1000;

    public SaveStocktakeDtoValidator()
    {
        RuleFor(x => x.WarehouseType)
            .Must(w => w is WarehouseTypes.Material or WarehouseTypes.SemiFinished or WarehouseTypes.Finished)
            .WithMessage(WarehouseMessages.StocktakeWarehouseInvalid);
        RuleFor(x => x.StocktakeDate).NotEmpty().WithMessage(Messages.Required("ngày kiểm kê"));
        RuleFor(x => x.Scope).MaxTrimmedLength(300, "Phạm vi");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l is null || l.Count <= MaxLines).WithMessage(WarehouseMessages.StocktakeTooManyLines);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialId).GreaterThan(0).When(l => l.MaterialId is not null).WithMessage("Vui lòng chọn NVL.");
            line.RuleFor(l => l.OrderLineId).GreaterThan(0).When(l => l.OrderLineId is not null).WithMessage("Dòng đơn không hợp lệ.");
            line.RuleFor(l => l.ProductComponentMaterialId).GreaterThan(0).When(l => l.ProductComponentMaterialId is not null)
                .WithMessage("Chi tiết không hợp lệ.");
            // Cột DECIMAL(18,3): vượt thì database báo tràn số (lỗi hệ thống), quá 3 số lẻ thì bị làm tròn âm thầm.
            line.RuleFor(l => l.CountedQty).Cascade(CascadeMode.Stop)
                .GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative)
                .LessThanOrEqualTo(OrderMaterialInputValidator.MaxQty).WithMessage(WarehouseMessages.StocktakeQtyMax)
                .Must(v => decimal.Round(v!.Value, OrderMaterialInputValidator.QtyDecimals) == v).WithMessage(WarehouseMessages.StocktakeQtyScale)
                .When(l => l.CountedQty is not null);
            line.RuleFor(l => l.Note).MaxTrimmedLength(300, "Ghi chú");
        });
        // Kho BTP / thành phẩm đếm theo cái, không có số lẻ (kho NVL mới đếm mét / kg).
        RuleForEach(x => x.Lines)
            .Must((dto, l) => dto.WarehouseType == WarehouseTypes.Material || l.CountedQty is null || l.CountedQty == decimal.Truncate(l.CountedQty.Value))
            .WithMessage(WarehouseMessages.StocktakePieceQtyWhole);
    }
}

public sealed class StocktakeQueryValidator : PageQueryValidatorBase<StocktakeQuery>
{
    public StocktakeQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => StocktakeStatuses.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage(WarehouseMessages.StocktakeStatusInvalid);
        RuleFor(x => x.WarehouseType).Must(w => WarehouseTypes.All.Contains(w!))
            .When(x => !string.IsNullOrEmpty(x.WarehouseType)).WithMessage(WarehouseMessages.StocktakeWarehouseInvalid);
    }
}
