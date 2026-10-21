using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SaveOperationDtoValidator : AbstractValidator<SaveOperationDto>
{
    public SaveOperationDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.OperationCode).NotEmpty().WithMessage(Messages.Required("mã công đoạn")).MaximumLength(30);
        RuleFor(x => x.OperationName).NotEmpty().WithMessage(Messages.Required("tên công đoạn")).MaximumLength(200);
        RuleFor(x => x.StationSequence).GreaterThan((short)0);
        RuleFor(x => x.StandardTimeSec).GreaterThan(0).When(x => x.StandardTimeSec.HasValue);
        RuleFor(x => x.TeamType).Must(t => t is "Sewing" or "Cutting").When(x => !string.IsNullOrWhiteSpace(x.TeamType))
            .WithMessage("Loại tổ phải là Sewing hoặc Cutting.");
    }
}

public sealed class PieceRateQueryValidator : PageQueryValidatorBase<PieceRateQuery>
{
    public PieceRateQueryValidator() => RuleFor(x => x.Status).MaximumLength(20);
}

public sealed class SavePieceRateDtoValidator : AbstractValidator<SavePieceRateDto>
{
    public SavePieceRateDtoValidator()
    {
        RuleFor(x => x.OperationId).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.");
    }
}

public sealed class RejectPieceRateDtoValidator : AbstractValidator<RejectPieceRateDto>
{
    public RejectPieceRateDtoValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(500);   // MSG09
}

public sealed class AssignOperationDtoValidator : AbstractValidator<AssignOperationDto>
{
    public AssignOperationDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.OperationId).GreaterThan(0);
        RuleFor(x => x.WorkerId).GreaterThan(0);
    }
}

public sealed class SaveOutputDtoValidator : AbstractValidator<SaveOutputDto>
{
    public SaveOutputDtoValidator()
    {
        RuleFor(x => x.ShiftCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Rows).NotEmpty().WithMessage(Messages.OutputRowsRequired);
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.OperationId).GreaterThan(0);
            row.RuleFor(r => r.WorkerId).GreaterThan(0);
            row.RuleFor(r => r.Quantity).GreaterThanOrEqualTo(0).WithMessage(Messages.OutputQtyInvalid);
        });
    }
}

public sealed class QcBatchQueryValidator : PageQueryValidatorBase<QcBatchQuery> { }

public sealed class CreateQcBatchDtoValidator : AbstractValidator<CreateQcBatchDto>
{
    public CreateQcBatchDtoValidator()
    {
        RuleFor(x => x.ShiftCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.SewingTeamId).GreaterThan(0);
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.FinishedQty).GreaterThan(0).WithMessage(Messages.OutputQtyInvalid);
    }
}

public sealed class UpdateQcBatchDtoValidator : AbstractValidator<UpdateQcBatchDto>
{
    public UpdateQcBatchDtoValidator()
    {
        RuleFor(x => x.FinishedQty).GreaterThan(0).WithMessage(Messages.OutputQtyInvalid);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class DeleteQcBatchDtoValidator : AbstractValidator<DeleteQcBatchDto>
{
    public DeleteQcBatchDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class DefectQueryValidator : PageQueryValidatorBase<DefectQuery> { }

public sealed class CreateDefectDtoValidator : AbstractValidator<CreateDefectDto>
{
    public CreateDefectDtoValidator()
    {
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.OperationId).GreaterThan(0);
        RuleFor(x => x.ResponsibleWorkerId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(Messages.OutputQtyInvalid);
        RuleFor(x => x.DefectType).NotEmpty().MaximumLength(10);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ReasonNote).MaximumLength(500);
        RuleFor(x => x.ShiftCode).MaximumLength(10);
    }
}

public sealed class CancelDefectDtoValidator : AbstractValidator<CancelDefectDto>
{
    public CancelDefectDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ObjectDefectDtoValidator : AbstractValidator<ObjectDefectDto>
{
    public ObjectDefectDtoValidator()
    {
        RuleFor(x => x.Objection).NotEmpty().WithMessage(Messages.DefectObjectionRequired).MaximumLength(500);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}
