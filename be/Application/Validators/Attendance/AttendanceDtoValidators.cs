using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

/// <summary>
/// Validate định dạng đầu vào của luồng chấm công (sai → 400).
/// Nghiệp vụ (mã đã map, tháng khóa, trùng lượt) kiểm ở Service / thủ tục DB (→ 409).
/// </summary>
public sealed class AttendancePunchDtoValidator : AbstractValidator<AttendancePunchDto>
{
    /// <summary>Chặn giờ quẹt vô lý: trước mốc này chắc chắn là dữ liệu hỏng.</summary>
    private static readonly DateTime EarliestPunch = new(2020, 1, 1);

    public AttendancePunchDtoValidator()
    {
        RuleFor(x => x.AttendanceCode)
            .GreaterThan(0).WithMessage("Mã chấm công phải là số dương.");

        RuleFor(x => x.SourceRecordId)
            .GreaterThan(0).When(x => x.SourceRecordId.HasValue)
            .WithMessage("CheckInOut.ID phải là số dương.");

        RuleFor(x => x.PunchTime)
            .NotEmpty().WithMessage("Thiếu giờ quẹt.")
            .GreaterThanOrEqualTo(EarliestPunch).WithMessage("Giờ quẹt không hợp lệ.")
            // Máy chấm công có thể lệch giờ đôi chút nên nới một ngày thay vì chặn đúng hiện tại.
            .LessThanOrEqualTo(_ => DateTime.Now.AddDays(1)).WithMessage("Giờ quẹt vượt quá hiện tại.");
    }
}

public sealed class IngestPunchesDtoValidator : AbstractValidator<IngestPunchesDto>
{
    /// <summary>Trần một lô để agent không gửi quá lớn làm nghẽn TVP.</summary>
    public const int MaxBatchSize = 5000;

    public IngestPunchesDtoValidator()
    {
        RuleFor(x => x.Punches)
            .NotNull().WithMessage(Messages.AttendanceEmptyBatch)
            .Must(p => p is { Count: > 0 }).WithMessage(Messages.AttendanceEmptyBatch)
            .Must(p => p is null || p.Count <= MaxBatchSize)
            .WithMessage($"Mỗi lô tối đa {MaxBatchSize} lượt quẹt.");

        RuleForEach(x => x.Punches).SetValidator(new AttendancePunchDtoValidator());

        RuleFor(x => x.FileName)
            .MaximumLength(300).WithMessage("Tên file tối đa 300 ký tự.");
    }
}

public sealed class MapAttendanceCodeDtoValidator : AbstractValidator<MapAttendanceCodeDto>
{
    public MapAttendanceCodeDtoValidator()
    {
        RuleFor(x => x.AttendanceCode)
            .GreaterThan(0).WithMessage("Vui lòng chọn mã chấm công.");

        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("Vui lòng chọn nhân viên.");
    }
}

public sealed class TimesheetQueryValidator : AbstractValidator<TimesheetQuery>
{
    /// <summary>Một lần xem tối đa khoảng bằng hai tháng, tránh quét cả bảng công.</summary>
    public const int MaxRangeDays = 62;

    public TimesheetQueryValidator()
    {
        RuleFor(x => x.FromDate).NotEmpty().WithMessage("Vui lòng chọn từ ngày.");
        RuleFor(x => x.ToDate).NotEmpty().WithMessage("Vui lòng chọn đến ngày.");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate).WithMessage(Messages.DateRangeInvalid);

        RuleFor(x => x)
            .Must(q => q.ToDate.DayNumber - q.FromDate.DayNumber < MaxRangeDays)
            .WithMessage(string.Format(Messages.DateRangeTooWide, MaxRangeDays))
            .WithName(nameof(TimesheetQuery.ToDate));

        RuleFor(x => x.TeamId).GreaterThan(0).When(x => x.TeamId.HasValue);
        RuleFor(x => x.EmployeeId).GreaterThan(0).When(x => x.EmployeeId.HasValue);

        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang từ 1 đến {Paging.MaxPageSize}.");

        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}

public sealed class CreateAttendanceAdjustmentDtoValidator : AbstractValidator<CreateAttendanceAdjustmentDto>
{
    public CreateAttendanceAdjustmentDtoValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Vui lòng chọn nhân viên.");

        RuleFor(x => x.WorkDate).NotEmpty().WithMessage("Vui lòng chọn ngày công.");

        RuleFor(x => x.AdjustmentType)
            .Must(AdjustmentType.All.Contains)
            .WithMessage("Loại chấm bù không hợp lệ.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(Messages.ReasonRequired)              // GBR-25, MSG09
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        RuleFor(x => x.RequestId).GreaterThan(0).When(x => x.RequestId.HasValue);

        RuleFor(x => x.RequestId)
            .NotNull().When(x => x.AdjustmentType == AdjustmentType.ApprovedRequest)
            .WithMessage(Messages.AttendanceAdjustmentRequestRequired);

        RuleFor(x => x)
            .Must(x => x.CheckIn.HasValue || x.CheckOut.HasValue)
            .WithMessage(Messages.AttendanceAdjustmentNoTime);
    }
}

public sealed class AdjustmentQueryValidator : AbstractValidator<AdjustmentQuery>
{
    /// <summary>Một lần xem tối đa khoảng một năm chấm bù.</summary>
    public const int MaxRangeDays = 366;

    public AdjustmentQueryValidator()
    {
        RuleFor(x => x.FromDate).NotEmpty().WithMessage("Vui lòng chọn từ ngày.");
        RuleFor(x => x.ToDate).NotEmpty().WithMessage("Vui lòng chọn đến ngày.");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate).WithMessage(Messages.DateRangeInvalid);

        RuleFor(x => x)
            .Must(q => q.ToDate.DayNumber - q.FromDate.DayNumber < MaxRangeDays)
            .WithMessage(string.Format(Messages.DateRangeTooWide, MaxRangeDays))
            .WithName(nameof(AdjustmentQuery.ToDate));

        RuleFor(x => x.EmployeeId).GreaterThan(0).When(x => x.EmployeeId.HasValue);
        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}
