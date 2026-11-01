using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class RequestLineInputValidator : AbstractValidator<RequestLineInput>
{
    public RequestLineInputValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(9_999_999);
        RuleFor(x => x.ItemDescription).MaximumLength(300);
    }
}

public sealed class CreateRequestDtoValidator : AbstractValidator<CreateRequestDto>
{
    public CreateRequestDtoValidator()
    {
        RuleFor(x => x.RequestType).NotEmpty()
            .Must(t => RequestTypes.All.Contains(CodeText.ToCode(t))).WithMessage("Loại đơn không hợp lệ.");
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().WithMessage(Messages.RequestContentRequired).MaximumLength(2000);
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= 50).WithMessage("Tối đa 50 dòng.");
        RuleForEach(x => x.Lines).SetValidator(new RequestLineInputValidator());
    }
}

public sealed class UpdateRequestDtoValidator : AbstractValidator<UpdateRequestDto>
{
    public UpdateRequestDtoValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().WithMessage(Messages.RequestContentRequired).MaximumLength(2000);
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= 50).WithMessage("Tối đa 50 dòng.");
        RuleForEach(x => x.Lines).SetValidator(new RequestLineInputValidator());
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ApproveRequestDtoValidator : AbstractValidator<ApproveRequestDto>
{
    public ApproveRequestDtoValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class RejectRequestDtoValidator : AbstractValidator<RejectRequestDto>
{
    public RejectRequestDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(1000);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class CompleteRequestDtoValidator : AbstractValidator<CompleteRequestDto>
{
    public CompleteRequestDtoValidator()
    {
        RuleFor(x => x.ResultNote).MaximumLength(1000);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class CancelRequestDtoValidator : AbstractValidator<CancelRequestDto>
{
    public CancelRequestDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class RequestQueryValidator : PageQueryValidatorBase<RequestQuery> { }
