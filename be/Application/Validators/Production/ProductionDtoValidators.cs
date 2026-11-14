using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public static class OperationLimits
{
    public const int MinSkillLevel = 1;
    public const int MaxSkillLevel = 9;
    public const decimal MaxStandardTimeSec = 9999999.99m;   // DECIMAL(9,2)
    public const int MaxGroupNameLength = 200;
    public const int MaxOperationsPerGroupChange = 500;
}

public static class PieceRateLimits
{
    public const decimal MaxRate = 100_000_000m;
    public const int MaxRatesPerSubmit = 500;
}

public sealed class SaveOperationDtoValidator : AbstractValidator<SaveOperationDto>
{
    public SaveOperationDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.OperationCode).NotEmpty().WithMessage(Messages.Required("mã công đoạn")).MaximumLength(30);
        RuleFor(x => x.OperationCode).Must(c => CodeFormat.IsMatch(c.Trim())).When(x => !string.IsNullOrWhiteSpace(x.OperationCode))
            .WithMessage(Messages.OperationCodeFormat);   // cột VARCHAR: chữ có dấu sẽ bị mất khi lưu
        RuleFor(x => x.OperationName).NotEmpty().WithMessage(Messages.Required("tên công đoạn")).MaximumLength(200);
        RuleFor(x => x.StationSequence).GreaterThan((short)0);
        RuleFor(x => x.SkillLevel).InclusiveBetween((byte)OperationLimits.MinSkillLevel, (byte)OperationLimits.MaxSkillLevel)
            .When(x => x.SkillLevel.HasValue);
        RuleFor(x => x.StandardTimeSec).GreaterThan(0).LessThanOrEqualTo(OperationLimits.MaxStandardTimeSec).When(x => x.StandardTimeSec.HasValue);
        RuleFor(x => x.GroupName).MaximumLength(OperationLimits.MaxGroupNameLength);
        RuleFor(x => x.TeamType).Must(t => t is "Sewing" or "Cutting").When(x => !string.IsNullOrWhiteSpace(x.TeamType))
            .WithMessage("Loại tổ phải là Sewing hoặc Cutting.");
        RuleFor(x => x.IsFinalStep).Must(final => !final).When(x => x.TeamType == "Cutting")
            .WithMessage(Messages.OperationFinalMustBeSewing);
    }

    private static readonly System.Text.RegularExpressions.Regex CodeFormat = new("^[A-Za-z0-9._-]+$");
}

public sealed class CreateOperationGroupDtoValidator : AbstractValidator<CreateOperationGroupDto>
{
    public CreateOperationGroupDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.GroupName).NotEmpty().WithMessage(Messages.Required("tên nhóm công đoạn")).MaximumLength(OperationLimits.MaxGroupNameLength);
    }
}

public sealed class RenameOperationGroupDtoValidator : AbstractValidator<RenameOperationGroupDto>
{
    public RenameOperationGroupDtoValidator() =>
        RuleFor(x => x.GroupName).NotEmpty().WithMessage(Messages.Required("tên nhóm công đoạn")).MaximumLength(OperationLimits.MaxGroupNameLength);
}

public sealed class SetOperationsGroupDtoValidator : AbstractValidator<SetOperationsGroupDto>
{
    public SetOperationsGroupDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.OperationGroupId).GreaterThan(0).When(x => x.OperationGroupId.HasValue);
        RuleFor(x => x.OperationIds).NotEmpty();
        RuleFor(x => x.OperationIds).Must(ids => ids is null || ids.Count <= OperationLimits.MaxOperationsPerGroupChange);
        RuleForEach(x => x.OperationIds).GreaterThan(0);
        RuleFor(x => x.OperationIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.OperationDuplicateInRequest);
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
        RuleFor(x => x.Rate).GreaterThan(0).WithMessage(Messages.PieceRatePositive)
            .LessThanOrEqualTo(PieceRateLimits.MaxRate).WithMessage(string.Format(Messages.PieceRateTooHigh, PieceRateLimits.MaxRate))
            .Must(r => decimal.Round(r, 2) == r).WithMessage(Messages.PieceRateScale);
        RuleFor(x => x.EffectiveDate).NotEmpty().WithMessage(Messages.Required("ngày hiệu lực"));
    }
}

public sealed class SubmitPieceRatesDtoValidator : AbstractValidator<SubmitPieceRatesDto>
{
    public SubmitPieceRatesDtoValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage(Messages.PieceRateNoneSelected);
        RuleFor(x => x.Ids).Must(ids => ids is null || ids.Count <= PieceRateLimits.MaxRatesPerSubmit);
        RuleForEach(x => x.Ids).GreaterThan(0);
        RuleFor(x => x.Ids).Must(ids => ids is null || ids.Distinct().Count() == ids.Count);
    }
}

public sealed class RejectPieceRateDtoValidator : AbstractValidator<RejectPieceRateDto>
{
    public RejectPieceRateDtoValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(TextLimits.Reason);   // MSG09
}

/// <summary>Giới hạn của phân công công đoạn. Mỗi công đoạn kiểm hợp lệ riêng nên phải chặn số công đoạn trong một lần giao.</summary>
public static class AssignmentLimits
{
    public const int MaxOperations = 500;
    public const int MaxOrderLines = 500;
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

public sealed class SetOrderLineTeamsDtoValidator : AbstractValidator<SetOrderLineTeamsDto>
{
    public SetOrderLineTeamsDtoValidator()
    {
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.TeamIds).NotNull();
        RuleForEach(x => x.TeamIds).GreaterThan(0);
        RuleFor(x => x.TeamIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.OrderLineTeamDuplicate);
    }
}

public sealed class AddOrderLineTeamsDtoValidator : AbstractValidator<AddOrderLineTeamsDto>
{
    public AddOrderLineTeamsDtoValidator()
    {
        RuleFor(x => x.OrderLineIds).NotEmpty();
        RuleFor(x => x.OrderLineIds).Must(ids => ids is null || ids.Count <= AssignmentLimits.MaxOrderLines);
        RuleForEach(x => x.OrderLineIds).GreaterThan(0);
        RuleFor(x => x.OrderLineIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.OrderLineTeamLineDuplicate);
        RuleFor(x => x.TeamIds).NotEmpty();
        RuleForEach(x => x.TeamIds).GreaterThan(0);
        RuleFor(x => x.TeamIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.OrderLineTeamDuplicate);
    }
}

public sealed class AssignOperationsDtoValidator : AbstractValidator<AssignOperationsDto>
{
    public AssignOperationsDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.WorkerId).GreaterThan(0);
        RuleFor(x => x.OperationIds).NotEmpty().WithMessage(Messages.AssignmentOperationsRequired);
        RuleFor(x => x.OperationIds).Must(ids => ids is null || ids.Count <= AssignmentLimits.MaxOperations)
            .WithMessage(string.Format(Messages.AssignmentOperationsTooMany, AssignmentLimits.MaxOperations));
        RuleForEach(x => x.OperationIds).GreaterThan(0);
        RuleFor(x => x.OperationIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.AssignmentOperationsDuplicate);
    }
}

public sealed class ReplaceAssignmentDtoValidator : AbstractValidator<ReplaceAssignmentDto>
{
    public ReplaceAssignmentDtoValidator() => RuleFor(x => x.NewWorkerId).GreaterThan(0);
}

public sealed class CopyAssignmentsDtoValidator : AbstractValidator<CopyAssignmentsDto>
{
    public CopyAssignmentsDtoValidator()
    {
        RuleFor(x => x.FromOrderId).GreaterThan(0);
        RuleFor(x => x.ToOrderId).GreaterThan(0);
    }
}

/// <summary>
/// Giới hạn của sản lượng. Số lượng tối đa 1.000.000: cột INT, và tổng với số đã nhập của công nhân khi so với trần
/// không được tràn số (tràn thì phép so với trần cho qua nhầm).
/// </summary>
public static class OutputLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxRows = 2000;
    public const int MaxReason = TextLimits.Reason;
}

public sealed class SaveOutputDtoValidator : AbstractValidator<SaveOutputDto>
{
    public SaveOutputDtoValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty().WithMessage(Messages.Required("ngày làm việc"));
        RuleFor(x => x.ShiftCode).NotEmpty().WithMessage("Vui lòng chọn ca.")
            .MaximumLength(10).WithMessage("Ca làm việc không hợp lệ.");
        RuleFor(x => x.OrderLineId).GreaterThan(0).WithMessage("Vui lòng chọn dòng đơn.");
        RuleFor(x => x.SewingTeamId).GreaterThan(0).When(x => x.SewingTeamId is not null).WithMessage("Vui lòng chọn tổ may.");
        RuleFor(x => x.Reason).MaxTrimmedLength(OutputLimits.MaxReason, "Lý do");
        RuleFor(x => x.Rows).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(Messages.OutputRowsRequired)
            .Must(r => r.Count <= OutputLimits.MaxRows).WithMessage(Messages.OutputTooManyRows);
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.OperationId).GreaterThan(0).WithMessage("Công đoạn không hợp lệ.");
            row.RuleFor(r => r.WorkerId).GreaterThan(0).WithMessage("Công nhân không hợp lệ.");
            row.RuleFor(r => r.Quantity).Cascade(CascadeMode.Stop)
                .GreaterThanOrEqualTo(0).WithMessage(Messages.OutputQtyInvalid)
                .LessThanOrEqualTo(OutputLimits.MaxQty).WithMessage(Messages.OutputQtyMax);
        }).When(x => x.Rows is { Count: <= OutputLimits.MaxRows });
    }
}

public sealed class QcBatchQueryValidator : PageQueryValidatorBase<QcBatchQuery> { }

/// <summary>Giới hạn của đợt QC. Số đạt tối đa 1.000.000 (cột INT, và tổng với số đã QC khi so với trần không được tràn); lý do tối đa 500 (TextLimits.Reason).</summary>
public static class QcLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxReason = TextLimits.Reason;
}

public sealed class CreateQcBatchDtoValidator : AbstractValidator<CreateQcBatchDto>
{
    public CreateQcBatchDtoValidator()
    {
        RuleFor(x => x.ShiftCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.SewingTeamId).GreaterThan(0);
        RuleFor(x => x.OrderLineId).GreaterThan(0);
        RuleFor(x => x.FinishedQty).GreaterThan(0).WithMessage(Messages.QcQtyInvalid)
            .LessThanOrEqualTo(QcLimits.MaxQty).WithMessage(Messages.QcQtyMax);
    }
}

public sealed class UpdateQcBatchDtoValidator : AbstractValidator<UpdateQcBatchDto>
{
    public UpdateQcBatchDtoValidator()
    {
        RuleFor(x => x.FinishedQty).GreaterThan(0).WithMessage(Messages.QcQtyInvalid)
            .LessThanOrEqualTo(QcLimits.MaxQty).WithMessage(Messages.QcQtyMax);
        RuleFor(x => x.Reason).MaxTrimmedLength(QcLimits.MaxReason, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}

public sealed class DeleteQcBatchDtoValidator : AbstractValidator<DeleteQcBatchDto>
{
    public DeleteQcBatchDtoValidator()
    {
        RuleFor(x => x.Reason).MaxTrimmedLength(QcLimits.MaxReason, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}

/// <summary>Giới hạn của phiếu hàng hỏng. Số lượng tối đa 1.000.000 (cột INT); ghi chú, lý do tối đa 500 (TextLimits.Reason, cột NVARCHAR(500)).</summary>
public static class DefectLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxText = TextLimits.Reason;
    /// <summary>Khoảng ngày lọc danh sách tối đa 366 ngày (một năm).</summary>
    public const int MaxRangeDays = 366;
    public const string StaleRowVer = "Thiếu phiên bản dữ liệu, hãy tải lại trang.";
}

public sealed class DefectQueryValidator : PageQueryValidatorBase<DefectQuery>
{
    public DefectQueryValidator()
    {
        RuleFor(x => x.SewingTeamId).GreaterThan(0).When(x => x.SewingTeamId is not null).WithMessage("Tổ không hợp lệ.");
        RuleFor(x => x.Status).Must(s => DefectStatuses.All.Contains(CodeText.ToCode(s!)))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Trạng thái phiếu lỗi không hợp lệ.");
        RuleFor(x => x.DefectType).Must(t => DefectTypes.All.Contains(CodeText.ToCode(t!)))
            .When(x => !string.IsNullOrEmpty(x.DefectType)).WithMessage(Messages.DefectTypeInvalid);
        RuleFor(x => x).Must(x => x.From!.Value <= x.To!.Value).WithName("From")
            .WithMessage("Từ ngày phải trước hoặc bằng đến ngày.")
            .When(x => x.From is not null && x.To is not null);
        RuleFor(x => x).Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber <= DefectLimits.MaxRangeDays).WithName("To")
            .WithMessage($"Khoảng ngày lọc tối đa {DefectLimits.MaxRangeDays} ngày.")
            .When(x => x.From is not null && x.To is not null && x.From <= x.To);
    }
}

public sealed class CreateDefectDtoValidator : AbstractValidator<CreateDefectDto>
{
    public CreateDefectDtoValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty().WithMessage(Messages.Required("ngày làm việc"));
        RuleFor(x => x.OrderLineId).GreaterThan(0).WithMessage("Vui lòng chọn dòng đơn.");
        RuleFor(x => x.OperationId).GreaterThan(0).WithMessage("Vui lòng chọn công đoạn.");
        RuleFor(x => x.ResponsibleWorkerId).GreaterThan(0).WithMessage("Vui lòng chọn công nhân gây lỗi.");
        RuleFor(x => x.Quantity).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(Messages.OutputQtyInvalid)
            .LessThanOrEqualTo(DefectLimits.MaxQty).WithMessage(Messages.OutputQtyMax);
        RuleFor(x => x.DefectType).Must(t => DefectTypes.All.Contains(CodeText.ToCode(t ?? string.Empty)))
            .WithMessage(Messages.DefectTypeInvalid);
        RuleFor(x => x.ReasonCode).Must(c => DefectReasons.All.Contains(CodeText.ToCode(c ?? string.Empty)))
            .WithMessage(Messages.DefectReasonInvalid);
        RuleFor(x => x.ReasonNote).MaxTrimmedLength(DefectLimits.MaxText, "Ghi chú");
        RuleFor(x => x.ReasonNote).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(Messages.DefectNoteRequired)
            .When(x => CodeText.ToCode(x.ReasonCode ?? string.Empty) == DefectReasons.Other);
        RuleFor(x => x.ShiftCode).Must(s => ShiftCodes.All.Contains(CodeText.ToCode(s!)))
            .When(x => !string.IsNullOrWhiteSpace(x.ShiftCode)).WithMessage("Ca làm việc không hợp lệ.");
    }
}

public sealed class CancelDefectDtoValidator : AbstractValidator<CancelDefectDto>
{
    public CancelDefectDtoValidator()
    {
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(Messages.DefectCancelReasonRequired)
            .MaxTrimmedLength(DefectLimits.MaxText, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}

public sealed class UpdateDefectDtoValidator : AbstractValidator<UpdateDefectDto>
{
    public UpdateDefectDtoValidator()
    {
        RuleFor(x => x.OperationId).GreaterThan(0).WithMessage("Vui lòng chọn công đoạn.");
        RuleFor(x => x.ResponsibleWorkerId).GreaterThan(0).WithMessage("Vui lòng chọn công nhân gây lỗi.");
        RuleFor(x => x.Quantity).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(Messages.OutputQtyInvalid)
            .LessThanOrEqualTo(DefectLimits.MaxQty).WithMessage(Messages.OutputQtyMax);
        RuleFor(x => x.DefectType).Must(t => DefectTypes.All.Contains(CodeText.ToCode(t ?? string.Empty)))
            .WithMessage(Messages.DefectTypeInvalid);
        RuleFor(x => x.ReasonCode).Must(c => DefectReasons.All.Contains(CodeText.ToCode(c ?? string.Empty)))
            .WithMessage(Messages.DefectReasonInvalid);
        RuleFor(x => x.ReasonNote).MaxTrimmedLength(DefectLimits.MaxText, "Ghi chú");
        RuleFor(x => x.ReasonNote).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(Messages.DefectNoteRequired)
            .When(x => CodeText.ToCode(x.ReasonCode ?? string.Empty) == DefectReasons.Other);
        RuleFor(x => x.ShiftCode).Must(s => ShiftCodes.All.Contains(CodeText.ToCode(s!)))
            .When(x => !string.IsNullOrWhiteSpace(x.ShiftCode)).WithMessage("Ca làm việc không hợp lệ.");
        RuleFor(x => x.Reason).MaxTrimmedLength(DefectLimits.MaxText, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}
