using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// Quy tắc dùng chung của luồng may + QC (KI-01…KI-11): tổ của người đăng nhập, hạn sửa (GBR-09), giờ hết ca,
/// khóa tháng (GBR-10), và quyền sửa sau hạn của Quản đốc (UC22, MSG20).
/// </summary>
public sealed class ProductionGuard
{
    private static readonly TimeOnly DefaultLunchStart = new(11, 30);
    private static readonly TimeOnly DefaultWorkEnd = new(16, 30);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly ISystemSettingReader _settings;

    public ProductionGuard(IUnitOfWork uow, ICurrentUser user, IClock clock, ISystemSettingReader settings)
    {
        _uow = uow; _user = user; _clock = clock; _settings = settings;
    }

    public bool IsManager => _user.Role == Roles.Manager;

    /// <summary>Tổ may người đăng nhập thao tác. Quản đốc sửa hộ phải chọn tổ; tổ trưởng / tổ phó dùng tổ của mình
    /// và phải là tổ may.</summary>
    public Result<int> ResolveSewingTeam(int? requestedTeamId)
    {
        if (IsManager)
            return requestedTeamId is > 0
                ? Result<int>.Success(requestedTeamId.Value)
                : Result<int>.Failure(ErrorCodes.OutputInvalid, Messages.OutputTeamRequired, ResultKind.Validation);

        if (_user.TeamId is { } teamId && _user.TeamTypeCode == TeamTypes.Sewing)
            return Result<int>.Success(teamId);

        return Result<int>.Failure(ErrorCodes.ProductionForbidden, Messages.ProductionSewingTeamOnly, ResultKind.Forbidden);
    }

    /// <summary>Hết ngày làm việc kế tiếp sau <paramref name="workDate"/>, bỏ qua Chủ nhật — cùng công thức
    /// <c>fn_NextWorkingDayEnd</c> trong DB.</summary>
    public static DateTime NextWorkingDayEnd(DateOnly workDate)
    {
        var next = workDate.AddDays(1);
        if (next.DayOfWeek == DayOfWeek.Sunday) next = next.AddDays(1);
        return next.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1);
    }

    /// <summary>Hạn sửa của QA/QC: hết ca. Sáng đến đầu nghỉ trưa, chiều đến hết giờ làm (cả hai lấy từ
    /// SystemSetting), tăng ca đến hết ngày làm việc đó.</summary>
    public async Task<DateTime> ShiftEndAsync(DateOnly workDate, string shiftCode, CancellationToken ct)
    {
        var time = shiftCode switch
        {
            ShiftCodes.Morning => await _settings.GetTimeAsync(SettingKeys.LunchStartTime, DefaultLunchStart, ct),
            ShiftCodes.Afternoon => await _settings.GetTimeAsync(SettingKeys.WorkEndTime, DefaultWorkEnd, ct),
            _ => (TimeOnly?)null,
        };
        return time is { } t
            ? workDate.ToDateTime(t)
            : workDate.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1);
    }

    /// <summary>Còn trong hạn thì ai có quyền cũng sửa được. Quá hạn: chỉ Quản đốc và bắt buộc lý do (UC22, MSG20).
    /// Trả null khi được phép; lý do nhập được dùng cho AuditLog.</summary>
    public Result? CheckEditWindow(DateTime editDeadline, string? reason)
    {
        if (_clock.Now <= editDeadline) return null;
        if (!IsManager)
            return Result.Failure(ErrorCodes.ProductionEditDeadlinePassed, Messages.ProductionEditDeadlinePassed,
                ResultKind.Forbidden);
        return string.IsNullOrWhiteSpace(reason)
            ? Result.Failure(ErrorCodes.CommonValidation, Messages.ReasonRequired, ResultKind.Validation)
            : null;
    }

    /// <summary>true nếu ngày thuộc tháng đã khóa (chặn bằng pre-check để trả lỗi sạch, ngoài trigger DB).</summary>
    public Task<bool> IsPeriodLockedAsync(DateOnly workDate, CancellationToken ct) =>
        _uow.Repository<AccountingPeriod>().Query().AsNoTracking()
            .AnyAsync(p => p.Status == RecordStatus.Locked && p.StartDate <= workDate && workDate <= p.EndDate, ct);

    public Result PeriodLockedFailure(DateOnly workDate) => Result.Failure(ErrorCodes.ProductionPeriodLocked,
        string.Format(Messages.ProductionPeriodLocked, workDate.ToString("yyyy-MM")));
}
