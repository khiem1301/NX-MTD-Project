using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class OrderMaterialInputValidator : AbstractValidator<IOrderMaterialInput>
{
    /// <summary>Cột DECIMAL(18,3): vượt thì database báo tràn số (lỗi hệ thống), quá 3 số lẻ thì bị làm tròn âm thầm.</summary>
    public const decimal MaxQty = 100_000_000m;
    public const int QtyDecimals = 3;

    public OrderMaterialInputValidator()
    {
        RuleFor(x => x.MaterialId).GreaterThan(0).WithMessage("Vui lòng chọn NVL.");
        RuleFor(x => x.RequiredQty).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Số cần không được âm.")
            .LessThanOrEqualTo(MaxQty).WithMessage("Số cần tối đa 100.000.000.")
            .Must(HasValidScale).WithMessage($"Số cần tối đa {QtyDecimals} số lẻ.");
        RuleFor(x => x.CustomerQty).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Số khách cấp không được âm.")
            .LessThanOrEqualTo(MaxQty).WithMessage("Số khách cấp tối đa 100.000.000.")
            .Must(HasValidScale).WithMessage($"Số khách cấp tối đa {QtyDecimals} số lẻ.");
        // Chỉ báo khi từng số đã hợp lệ, tránh hai thông báo cho cùng một ô.
        RuleFor(x => x).Must(x => x.RequiredQty > 0 || x.CustomerQty > 0).WithName("RequiredQty")
            .WithMessage(WarehouseMessages.OrderMaterialQtyRequired)
            .When(x => x.RequiredQty >= 0 && x.CustomerQty >= 0);
        RuleFor(x => x.Note).MaxTrimmedLength(500, "Ghi chú");
    }

    private static bool HasValidScale(decimal value) => decimal.Round(value, QtyDecimals) == value;
}

public sealed class CreateOrderMaterialDtoValidator : AbstractValidator<CreateOrderMaterialDto>
{
    public CreateOrderMaterialDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        Include(new OrderMaterialInputValidator());
    }
}

public sealed class UpdateOrderMaterialDtoValidator : AbstractValidator<UpdateOrderMaterialDto>
{
    public UpdateOrderMaterialDtoValidator() => Include(new OrderMaterialInputValidator());
}

public sealed class CreateOrderMaterialBatchDtoValidator : AbstractValidator<CreateOrderMaterialBatchDto>
{
    public CreateOrderMaterialBatchDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.OrderMaterialLinesRequired);
        RuleForEach(x => x.Lines).SetValidator(new OrderMaterialInputValidator());
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.MaterialId).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

/// <summary>Lịch khách giao NVL của cả đơn: số lần 1–99, số ngày giao đúng bằng số lần, ngày sau không trước ngày trước (DEV-ALL-95, DEV-ALL-113).</summary>
public sealed class SaveOrderCustomerDeliveryDtoValidator : AbstractValidator<SaveOrderCustomerDeliveryDto>
{
    public SaveOrderCustomerDeliveryDtoValidator()
    {
        RuleFor(x => x.DeliveryCount).InclusiveBetween((short)1, DeliveryLimits.MaxCount)
            .WithMessage(string.Format(WarehouseMessages.CustomerDeliveryCountInvalid, DeliveryLimits.MaxCount));
        RuleFor(x => x.DeliveryDates).NotNull().WithMessage(WarehouseMessages.ExpectedDeliveryRequired)
            .Must((x, dates) => DocumentDateRules.CheckDeliveryDates(x.DeliveryCount, dates) is null)
            .WithMessage((x, dates) => DocumentDateRules.CheckDeliveryDates(x.DeliveryCount, dates));
    }
}
