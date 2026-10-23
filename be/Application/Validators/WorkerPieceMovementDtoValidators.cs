using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class CreateWorkerMovementDtoValidator : AbstractValidator<CreateWorkerMovementDto>
{
    public CreateWorkerMovementDtoValidator()
    {
        RuleFor(x => x.MovementType).Must(t => t is PieceMovementTypes.Handover or PieceMovementTypes.Retrieval)
            .WithMessage(WarehouseMessages.MovementTypeInvalid);
        RuleFor(x => x.WorkerId).GreaterThan(0).WithMessage(WarehouseMessages.WorkerRequired);
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(WarehouseMessages.QtyMustBePositive);
        // Thu hồi bắt buộc nêu lý do (CK_WPM_Retr); giao thì không có lý do thu hồi.
        RuleFor(x => x.RetrievalReason).Must(r => r is not null && RetrievalReasons.All.Contains(r))
            .When(x => x.MovementType == PieceMovementTypes.Retrieval)
            .WithMessage(WarehouseMessages.RetrievalReasonRequired);
    }
}

public sealed class ConfirmWorkerMovementDtoValidator : AbstractValidator<ConfirmWorkerMovementDto>
{
    public ConfirmWorkerMovementDtoValidator()
    {
        RuleFor(x => x.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.DiscrepancyReason).MaximumLength(500);
    }
}

public sealed class ConfirmOnBehalfDtoValidator : AbstractValidator<ConfirmOnBehalfDto>
{
    public ConfirmOnBehalfDtoValidator()
    {
        RuleFor(x => x.ConfirmedQty).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.DiscrepancyReason).MaximumLength(500);
        RuleFor(x => x.OnBehalfReason).NotEmpty().WithMessage(WarehouseMessages.ReasonRequired).MaximumLength(500);
    }
}
