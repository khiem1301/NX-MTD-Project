using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>
/// Validate định dạng đầu vào khi Quản đốc lập/sửa chỉ tiêu KPI tổ may (sai → 400).
/// Nghiệp vụ (đơn/mã hàng/tổ không hợp lệ, trùng kỳ, trạng thái không cho sửa) kiểm ở KpiService (→ 409).
/// </summary>
public sealed class SaveKpiTargetDtoValidator : AbstractValidator<SaveKpiTargetDto>
{
    public SaveKpiTargetDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("Vui lòng chọn đơn hàng.");
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Vui lòng chọn mã hàng.");
        RuleFor(x => x.SewingTeamId).GreaterThan(0).WithMessage("Vui lòng chọn tổ may.");

        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Vui lòng chọn ngày bắt đầu.");
        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("Vui lòng chọn ngày kết thúc.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage(Messages.DateRangeInvalid);

        RuleFor(x => x.TargetFinishedQty).GreaterThan(0).WithMessage(Messages.KpiTargetQtyMustBePositive);
        RuleFor(x => x.WorkingDays).GreaterThan((short)0).WithMessage(Messages.KpiWorkingDaysMustBePositive);
    }
}

public sealed class KpiTargetQueryValidator : AbstractValidator<KpiTargetQuery>
{
    public KpiTargetQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang từ 1 đến {Paging.MaxPageSize}.");
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId.HasValue);
        RuleFor(x => x.SewingTeamId).GreaterThan(0).When(x => x.SewingTeamId.HasValue);
        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}
