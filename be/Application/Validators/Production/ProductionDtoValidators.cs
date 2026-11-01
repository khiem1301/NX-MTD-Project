using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

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

public sealed class AssignOperationsDtoValidator : AbstractValidator<AssignOperationsDto>
{
    public AssignOperationsDtoValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.WorkerId).GreaterThan(0);
        RuleFor(x => x.OperationIds).NotEmpty().WithMessage(Messages.AssignmentOperationsRequired);
        RuleForEach(x => x.OperationIds).GreaterThan(0);
        RuleFor(x => x.OperationIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(Messages.AssignmentOperationsDuplicate);
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
    public const int MaxReason = 500;
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

/// <summary>Giới hạn của phiếu hàng hỏng. Số lượng tối đa 1.000.000 (cột INT); ghi chú, lý do tối đa 500 (cột NVARCHAR(500)).</summary>
public static class DefectLimits
{
    public const int MaxQty = 1_000_000;
    public const int MaxText = 500;
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
        RuleFor(x => x.DefectType).Must(t => CodeText.ToCode(t!) is DefectTypes.Rework or DefectTypes.Scrap)
            .When(x => !string.IsNullOrEmpty(x.DefectType)).WithMessage(Messages.DefectTypeInvalid);
        RuleFor(x => x.Objection).Must(o => DefectObjectionFilters.All.Contains(o!))
            .When(x => !string.IsNullOrEmpty(x.Objection)).WithMessage("Tình trạng phản đối không hợp lệ.");
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
        RuleFor(x => x.DefectType).Must(t => CodeText.ToCode(t ?? string.Empty) is DefectTypes.Rework or DefectTypes.Scrap)
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
        RuleFor(x => x.Reason).MaxTrimmedLength(DefectLimits.MaxText, "Lý do");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}

public sealed class ObjectDefectDtoValidator : AbstractValidator<ObjectDefectDto>
{
    public ObjectDefectDtoValidator()
    {
        RuleFor(x => x.Objection).Must(o => !string.IsNullOrWhiteSpace(o)).WithMessage(Messages.DefectObjectionRequired)
            .MaxTrimmedLength(DefectLimits.MaxText, "Nội dung phản hồi");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}

public sealed class ConfirmDefectDtoValidator : AbstractValidator<ConfirmDefectDto>
{
    public ConfirmDefectDtoValidator()
    {
        RuleFor(x => x.Note).MaxTrimmedLength(DefectLimits.MaxText, "Ghi chú");
        RuleFor(x => x.RowVer).NotEmpty().WithMessage(DefectLimits.StaleRowVer);
    }
}
