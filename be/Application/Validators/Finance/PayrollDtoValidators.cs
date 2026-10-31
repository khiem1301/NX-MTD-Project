using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>UC32 — Admin từ chối bảng lương: bắt buộc lý do (GBR-05, MSG09).</summary>
public sealed class RejectPayrollDtoValidator : AbstractValidator<RejectPayrollDto>
{
    public RejectPayrollDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired)
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}

/// <summary>UC32 — Admin mở lại bảng lương đã duyệt về Nháp: bắt buộc lý do (MSG09).</summary>
public sealed class ReopenPayrollDtoValidator : AbstractValidator<ReopenPayrollDto>
{
    public ReopenPayrollDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired)
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}
