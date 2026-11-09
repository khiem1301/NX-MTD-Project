using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

/// <summary>Giới hạn của phiếu xuất hàng: số lượng mỗi dòng tối đa 1.000.000 (bằng trần số lượng đơn, cột INT); địa chỉ 500, ghi chú 1.000 ký tự (tính sau khi cắt khoảng trắng).</summary>
public static class DispatchLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxLines = 2000;
    public const int MaxAddress = 500;
    public const int MaxNote = 1000;
    public const string StaleRowVer = "Thiếu phiên bản phiếu, hãy tải lại trang.";
}

public sealed class CreateDispatchNoteDtoValidator : AbstractValidator<CreateDispatchNoteDto>
{
    public CreateDispatchNoteDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(Messages.Required("đơn hàng"));
        RuleFor(x => x.DeliveryDate).NotEmpty().WithMessage(WarehouseMessages.DeliveryDateRequired);
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage(WarehouseMessages.DeliveryAddressRequired)
            .MaxTrimmedLength(DispatchLimits.MaxAddress, "Địa chỉ giao hàng");
        RuleFor(x => x.Note).MaxTrimmedLength(DispatchLimits.MaxNote, "Ghi chú");
        RuleFor(x => x.Lines).NotEmpty().WithMessage(WarehouseMessages.LinesRequired);
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= DispatchLimits.MaxLines)
            .WithMessage(WarehouseMessages.DispatchTooManyLines);
        RuleForEach(x => x.Lines).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l.OrderLineId > 0).WithMessage(WarehouseMessages.LinesRequired)
            .Must(l => l.IssueQty > 0).WithMessage(WarehouseMessages.QtyMustBePositive)
            .Must(l => l.IssueQty <= DispatchLimits.MaxQty).WithMessage(WarehouseMessages.DispatchQtyMax);
        RuleFor(x => x.Lines).Must(l => l is null || l.Where(x => x is not null).Select(x => x.OrderLineId).Distinct().Count() == l.Count(x => x is not null))
            .WithMessage(WarehouseMessages.DuplicateLine);
    }
}

public sealed class ConfirmDeliveryDtoValidator : AbstractValidator<ConfirmDeliveryDto>
{
    public ConfirmDeliveryDtoValidator()
    {
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DispatchLimits.StaleRowVer);
        RuleFor(x => x.CustomerReceivedDate).NotEmpty().WithMessage(WarehouseMessages.CustomerReceivedDateRequired);
    }
}

public sealed class DispatchActionDtoValidator : AbstractValidator<DispatchActionDto>
{
    public DispatchActionDtoValidator() => RuleFor(x => x.RowVer).NotEmpty().WithMessage(DispatchLimits.StaleRowVer);
}

public sealed class RejectDispatchDtoValidator : AbstractValidator<RejectDispatchDto>
{
    public RejectDispatchDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaxTrimmedLength(TextLimits.Reason, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DispatchLimits.StaleRowVer);
    }
}

public sealed class DispatchNoteQueryValidator : PageQueryValidatorBase<DispatchNoteQuery>
{
    public DispatchNoteQueryValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId.HasValue);
        RuleFor(x => x.Status).Must(s => DispatchStatuses.All.Contains(s!)).WithMessage("Trạng thái phiếu xuất không hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}
