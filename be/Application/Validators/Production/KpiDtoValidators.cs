using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>Giới hạn của chỉ tiêu KPI: số lượng tối đa 1.000.000 (bằng trần số lượng đơn, cột INT, tổng các ngày không tràn); kỳ và số ngày đặt tối đa 366.</summary>
public static class KpiLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxPeriodDays = 366;
    public const int MaxDailyRows = 366;
    public static readonly IReadOnlySet<string> Statuses = new HashSet<string> { "Active", "Closed", "Cancelled" };
}

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
        // Kỳ quá dài làm vòng lặp theo ngày (số ngày làm việc, lịch KPI) chạy rất lâu và tràn cột WorkingDays (SMALLINT).
        RuleFor(x => x.EndDate)
            .Must((x, end) => end.DayNumber - x.StartDate.DayNumber < KpiLimits.MaxPeriodDays)
            .WithMessage(Messages.KpiPeriodTooLong)
            .When(x => x.StartDate != default && x.EndDate >= x.StartDate);

        RuleFor(x => x.TargetFinishedQty).GreaterThan(0).WithMessage(Messages.KpiTargetQtyMustBePositive)
            .LessThanOrEqualTo(KpiLimits.MaxQty).WithMessage(Messages.KpiTargetQtyMax);
    }
}

public sealed class SaveKpiDailyPlanDtoValidator : AbstractValidator<SaveKpiDailyPlanDto>
{
    public SaveKpiDailyPlanDtoValidator()
    {
        RuleFor(x => x.DailyTargets).Cascade(CascadeMode.Stop).NotNull()
            .Must(days => days.Where(d => d is not null).Select(d => d.WorkDate).Distinct().Count() == days.Count(d => d is not null))
            .WithMessage(Messages.KpiDailyTargetDuplicateDate);
        RuleFor(x => x.DailyTargets).Must(days => days.Count <= KpiLimits.MaxDailyRows)
            .WithMessage(Messages.KpiDailyTargetTooMany).When(x => x.DailyTargets is not null);
        RuleForEach(x => x.DailyTargets).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(Messages.KpiDailyTargetQtyMustBePositive)
            .Must(d => d.TargetQty > 0).WithMessage(Messages.KpiDailyTargetQtyMustBePositive)
            .Must(d => d.TargetQty <= KpiLimits.MaxQty).WithMessage(Messages.KpiDailyTargetQtyMax)
            .When(x => x.DailyTargets is not null && x.DailyTargets.Count <= KpiLimits.MaxDailyRows);
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
        RuleFor(x => x.Keyword).MaxTrimmedLength(100, "Từ khóa");
        RuleFor(x => x.Status).Must(s => KpiLimits.Statuses.Contains(s!)).WithMessage("Trạng thái chỉ tiêu không hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}
