using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

/// <summary>
/// Giới hạn của lệnh cắt. Ghi chú tối đa 600 và lý do cắt thiếu tối đa 300: báo cắt xong nối lý do vào ghi chú
/// (cột Note 1000), nên cộng lại luôn vừa cột.
/// </summary>
public static class WorkOrderLimits
{
    public const int MaxNote = 600;
    public const int MaxFinishReason = 300;
    /// <summary>Mỗi dòng là một màu – size (tối đa 200) hoặc một chi tiết cắt lẻ của màu – size đó.</summary>
    public const int MaxLines = 1000;
    public const int MaxPlannedQty = 1_000_000;
    public const decimal MaxFabricTolerancePercent = 100m;
}

public sealed class SaveWorkOrderDtoValidator : AbstractValidator<SaveWorkOrderDto>
{
    public SaveWorkOrderDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Vui lòng chọn mã hàng.");
        RuleFor(x => x.CuttingTeamId).GreaterThan(0).WithMessage("Vui lòng chọn tổ cắt.");
        RuleFor(x => x.DueDate).NotEmpty().WithMessage(Messages.Required("hạn cắt"));
        RuleFor(x => x.Note).MaxTrimmedLength(WorkOrderLimits.MaxNote, "Ghi chú");
        RuleFor(x => x.FabricTolerancePercent)
            .InclusiveBetween(0m, WorkOrderLimits.MaxFabricTolerancePercent).WithMessage(WarehouseMessages.WorkOrderToleranceRange)
            .Must(v => decimal.Round(v, 2) == v).WithMessage(WarehouseMessages.WorkOrderToleranceDecimals);
        RuleFor(x => x.Lines).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l.Count <= WorkOrderLimits.MaxLines).WithMessage($"Tối đa {WorkOrderLimits.MaxLines} dòng.");
        RuleForEach(x => x.Lines).SetValidator(new SaveWorkOrderLineDtoValidator());
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.ProductComponentMaterialId)).Distinct().Count() == l.Count)
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class SaveWorkOrderLineDtoValidator : AbstractValidator<SaveWorkOrderLineDto>
{
    public SaveWorkOrderLineDtoValidator()
    {
        RuleFor(x => x.OrderLineId).GreaterThan(0).WithMessage("Dòng đơn không hợp lệ.");
        RuleFor(x => x.PlannedQty).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive)
            .LessThanOrEqualTo(WorkOrderLimits.MaxPlannedQty).WithMessage("Số lượng cắt tối đa 1.000.000.");
        RuleFor(x => x.ProductComponentMaterialId).GreaterThan(0).When(x => x.ProductComponentMaterialId is not null)
            .WithMessage("Vui lòng chọn chi tiết.");
    }
}

public sealed class CheckWorkOrderFabricDtoValidator : AbstractValidator<CheckWorkOrderFabricDto>
{
    public CheckWorkOrderFabricDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        RuleFor(x => x.CuttingTeamId).GreaterThan(0).WithMessage("Vui lòng chọn tổ cắt.");
        RuleFor(x => x.Lines).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l.Count <= WorkOrderLimits.MaxLines).WithMessage($"Tối đa {WorkOrderLimits.MaxLines} dòng.");
        RuleForEach(x => x.Lines).SetValidator(new SaveWorkOrderLineDtoValidator());
    }
}

public sealed class CreateReplacementWorkOrderDtoValidator : AbstractValidator<CreateReplacementWorkOrderDto>
{
    public CreateReplacementWorkOrderDtoValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0).WithMessage("Vui lòng chọn đơn cắt bù.");
        RuleFor(x => x.CuttingTeamId).GreaterThan(0).WithMessage("Vui lòng chọn tổ cắt.");
        RuleFor(x => x.DueDate).NotEmpty().WithMessage(Messages.Required("hạn cắt"));
        RuleFor(x => x.Note).MaxTrimmedLength(WorkOrderLimits.MaxNote, "Ghi chú");
    }
}

public sealed class CloseShortWorkOrderDtoValidator : AbstractValidator<CloseShortWorkOrderDto>
{
    public CloseShortWorkOrderDtoValidator()
    {
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(Messages.Required("lý do cắt thiếu"))
            .MaxTrimmedLength(WorkOrderLimits.MaxFinishReason, "Lý do cắt thiếu");
    }
}

public sealed class WorkOrderQueryValidator : PageQueryValidatorBase<WorkOrderQuery>
{
    public WorkOrderQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => WorkOrderStatuses.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage(WarehouseMessages.WorkOrderStatusInvalid);
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId is not null).WithMessage("Đơn hàng không hợp lệ.");
    }
}
