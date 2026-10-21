using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>
/// Validate định dạng đầu vào cho Doanh thu (UC27) + Hòa vốn (UC11). Nghiệp vụ (tháng YYYY-MM hợp lệ,
/// cách phân bổ OUTPUT/LABOUR_HOURS hợp lệ, cổng điều kiện GBR-17) kiểm ở FinanceService (→ 409/400 theo
/// Result.Kind), đây chỉ chặn input rỗng/âm rõ ràng trước khi tới Service.
/// </summary>
public sealed class RevenueQueryValidator : AbstractValidator<RevenueQuery>
{
    public RevenueQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang từ 1 đến {Paging.MaxPageSize}.");
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId.HasValue);
        RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId.HasValue);
    }
}

public sealed class BreakEvenCalculateDtoValidator : AbstractValidator<BreakEvenCalculateDto>
{
    public BreakEvenCalculateDtoValidator()
    {
        RuleFor(x => x.PeriodMonth).NotEmpty().WithMessage(Messages.BreakEvenPeriodMonthInvalid);
        RuleFor(x => x.AllocationBasis).NotEmpty().WithMessage(Messages.BreakEvenInvalidAllocationBasis);
    }
}
