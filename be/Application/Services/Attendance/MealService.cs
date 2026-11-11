using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC30: tick suất ăn từ chấm công, công nhân/tổ trưởng bỏ tick, JOB-02/JOB-03 chốt theo WillEat.
/// </summary>
public sealed class MealService : IMealService
{
    private readonly IMealRepository _meals;
    private readonly IMealRegistrationSeeder _seeder;
    private readonly IAttendanceService _attendance;
    private readonly ISystemSettingReader _settings;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;
    private readonly ILogger<MealService> _logger;

    private const string MealLink = "/team/meals";

    public MealService(
        IMealRepository meals,
        IMealRegistrationSeeder seeder,
        IAttendanceService attendance,
        ISystemSettingReader settings,
        INotificationService notifier,
        IClock clock,
        ILogger<MealService> logger)
    {
        _meals = meals;
        _seeder = seeder;
        _attendance = attendance;
        _settings = settings;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    // ---------- Tổ trưởng ----------

    public async Task<Result<TeamMealSuggestionDto>> GetTeamSuggestionAsync(int teamId, DateOnly date, CancellationToken ct)
    {
        var team = await _meals.GetTeamAsync(teamId, ct);
        if (team is null)
            return Result<TeamMealSuggestionDto>.Failure(ErrorCodes.MealTeamNotFound, Messages.MealTeamNotFound,
                ResultKind.NotFound);

        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.FindByDateAsync(date, ct);
        var attendanceCount = await _meals.GetTeamAttendanceCountAsync(teamId, date, ct);
        var willEat = meal is null
            ? attendanceCount
            : await _meals.CountWillEatAsync(meal.DailyMealId, teamId, ct);

        TeamMealConfirmation? confirmation = null;
        var locked = false;
        if (meal is not null)
        {
            confirmation = await _meals.FindConfirmationAsync(meal.DailyMealId, teamId, ct);
            locked = await IsLockedAsync(meal, ct);
        }

        return Result<TeamMealSuggestionDto>.Success(new TeamMealSuggestionDto(
            date, teamId, team.TeamName, attendanceCount,
            confirmation?.MealCount ?? willEat, ToPascalOrNull(confirmation?.Source),
            confirmation?.ConfirmedAt, locked));
    }

    public async Task<Result> ConfirmTeamMealAsync(int teamId, ConfirmTeamMealDto dto, CancellationToken ct)
    {
        var team = await _meals.GetTeamAsync(teamId, ct);
        if (team is null)
            return Result.Failure(ErrorCodes.MealTeamNotFound, Messages.MealTeamNotFound, ResultKind.NotFound);

        await _seeder.SeedForDateAsync(dto.MealDate, ct);

        var meal = await _meals.GetOrCreateAsync(dto.MealDate, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealLocked, Messages.MealLocked);

        var attendanceCount = await _meals.GetTeamAttendanceCountAsync(teamId, dto.MealDate, ct);
        var mealCount = await _meals.CountWillEatAsync(meal.DailyMealId, teamId, ct);
        var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, teamId, ct);
        Upsert(meal, existing, teamId, mealCount, attendanceCount,
            MealConfirmationSource.Confirmed, confirmedAt: _clock.Now, remindedAt: null);

        await _meals.SaveChangesAsync(ct);

        _logger.LogInformation("Tổ {TeamId} xác nhận {MealCount} suất ăn ngày {MealDate}",
            teamId, mealCount, dto.MealDate);
        return Result.Success(Messages.Saved);
    }

    public async Task<Result<TeamMealMembersDto>> GetTeamMembersAsync(int teamId, DateOnly date, CancellationToken ct)
    {
        var team = await _meals.GetTeamAsync(teamId, ct);
        if (team is null)
            return Result<TeamMealMembersDto>.Failure(ErrorCodes.MealTeamNotFound, Messages.MealTeamNotFound,
                ResultKind.NotFound);

        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.GetOrCreateAsync(date, ct);
        var regs = await _meals.GetTeamRegistrationsAsync(meal.DailyMealId, teamId, ct);
        var members = regs.Select(r => new TeamMealMemberDto(
            r.EmployeeId, r.Employee.EmployeeCode, r.Employee.FullName, r.WillEat, HasAttendance: true)).ToList();

        return Result<TeamMealMembersDto>.Success(new TeamMealMembersDto(
            date, teamId, team.TeamName,
            members.Count, members.Count(m => m.WillEat),
            await IsLockedAsync(meal, ct), members));
    }

    public async Task<Result> SetTeamMemberMealAsync(
        int teamId, int actorEmployeeId, SetTeamMemberMealDto dto, CancellationToken ct)
    {
        var team = await _meals.GetTeamAsync(teamId, ct);
        if (team is null)
            return Result.Failure(ErrorCodes.MealTeamNotFound, Messages.MealTeamNotFound, ResultKind.NotFound);

        await _seeder.SeedForDateAsync(dto.MealDate, ct);

        var meal = await _meals.GetOrCreateAsync(dto.MealDate, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealLocked, Messages.MealLocked);

        var employee = await _meals.GetEmployeeAsync(dto.EmployeeId, ct);
        if (employee is null || employee.TeamId != teamId)
            return Result.Failure(ErrorCodes.MealEmployeeNotInTeam, Messages.MealEmployeeNotInTeam,
                ResultKind.Validation);

        var reg = await _meals.FindRegistrationTrackedAsync(meal.DailyMealId, dto.EmployeeId, ct);
        if (reg is null)
            return Result.Failure(ErrorCodes.MealNoAttendance, Messages.MealNoAttendance, ResultKind.Validation);

        reg.WillEat = dto.WillEat;
        reg.UpdatedByEmployeeId = actorEmployeeId;
        reg.UpdatedAt = _clock.Now;

        await SyncTeamConfirmationAsync(meal, teamId, dto.MealDate, markConfirmed: true, ct);
        await _meals.SaveChangesAsync(ct);
        return Result.Success(Messages.MealMemberUpdated);
    }

    // ---------- Cá nhân ----------

    public async Task<Result<MyMealDto>> GetMyMealAsync(int employeeId, DateOnly date, CancellationToken ct)
    {
        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.FindByDateAsync(date, ct);
        var locked = meal is not null && await IsLockedAsync(meal, ct);
        var punched = await _meals.HasCheckInAsync(employeeId, date, ct);
        if (meal is null)
            return Result<MyMealDto>.Success(new MyMealDto(date, punched, false, locked));

        var reg = await _meals.FindRegistrationAsync(meal.DailyMealId, employeeId, ct);
        if (reg is null)
            // Đã quẹt nhưng suất đã chốt trước khi seed (quẹt sau giờ chốt) → vẫn báo đã quẹt.
            return Result<MyMealDto>.Success(new MyMealDto(date, punched, false, locked));

        return Result<MyMealDto>.Success(new MyMealDto(date, true, reg.WillEat, locked));
    }

    public async Task<Result> SetMyMealAsync(int employeeId, SetMyMealDto dto, CancellationToken ct)
    {
        var date = dto.MealDate ?? _clock.Today;
        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.GetOrCreateAsync(date, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealLocked, Messages.MealLocked);

        var reg = await _meals.FindRegistrationTrackedAsync(meal.DailyMealId, employeeId, ct);
        if (reg is null)
            return Result.Failure(ErrorCodes.MealNoAttendance, Messages.MealNoAttendance, ResultKind.Validation);

        // Chỉ người đã quẹt mới có dòng; được bỏ tick hoặc tick lại.
        reg.WillEat = dto.WillEat;
        reg.UpdatedByEmployeeId = employeeId;
        reg.UpdatedAt = _clock.Now;

        if (reg.TeamId is { } teamId)
            await SyncTeamConfirmationAsync(meal, teamId, date, markConfirmed: false, ct);

        await _meals.SaveChangesAsync(ct);
        return Result.Success(Messages.MealMemberUpdated);
    }

    // ---------- Kế toán ----------

    public async Task<Result> SetGuestMealAsync(SetGuestMealDto dto, CancellationToken ct)
    {
        var meal = await _meals.GetOrCreateAsync(dto.MealDate, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealGuestLocked, Messages.MealGuestLocked);

        meal.GuestCount = dto.GuestCount;
        meal.GuestNote = dto.GuestNote;
        await _meals.SaveChangesAsync(ct);

        return Result.Success(Messages.Saved);
    }

    public async Task<Result<DailyMealSummaryDto>> GetSummaryAsync(DateOnly date, CancellationToken ct)
    {
        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.GetOrCreateAsync(date, ct);
        var rows = await BuildTeamRowsAsync(meal, date, ct);
        var suggestedPrice = meal.MealPrice ?? await _meals.GetPreviousMealPriceAsync(date, ct);

        var teamTotal = rows.Where(r => r.Source != null).Sum(r => r.MealCount);
        // Tổ chưa có confirmation: dùng số WillEat hiện tại (gợi ý) để Kế toán xem trước giờ chốt.
        if (meal.Status == MealStatus.Open)
            teamTotal = rows.Sum(r => r.MealCount);

        var officeCount = meal.Status == MealStatus.Open
            ? await _meals.CountOfficeWillEatAsync(meal.DailyMealId, ct)
            : meal.OfficeCount;

        var totalMeals = teamTotal + officeCount + meal.GuestCount;
        var totalAmount = meal.MealPrice is null ? (decimal?)null : Math.Round(totalMeals * meal.MealPrice.Value, 2);

        return Result<DailyMealSummaryDto>.Success(new DailyMealSummaryDto(
            meal.DailyMealId, date, CodeText.ToPascal(meal.Status), officeCount, meal.GuestCount, meal.GuestNote,
            teamTotal, totalMeals, meal.MealPrice, suggestedPrice, totalAmount,
            meal.AttendanceIncomplete, meal.AutoClosedAt, meal.ClosedAt, meal.KitchenNotifiedAt, rows));
    }

    public async Task<Result> SetMealPriceAsync(SetMealPriceDto dto, CancellationToken ct)
    {
        var meal = await _meals.FindByDateAsync(dto.MealDate, ct);
        if (meal is null)
            return Result.Failure(ErrorCodes.MealNotFound, Messages.MealNotFound, ResultKind.NotFound);
        if (meal.Status == MealStatus.Open)
            return Result.Failure(ErrorCodes.MealNotClosedYet, Messages.MealNotClosedYet);

        meal.MealPrice = dto.MealPrice;
        await _meals.SaveChangesAsync(ct);

        return Result.Success(Messages.Saved);
    }

    // ---------- Job nền ----------

    public async Task RunReminderJobAsync(DateOnly date, CancellationToken ct)
    {
        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.GetOrCreateAsync(date, ct);
        if (await IsLockedAsync(meal, ct)) return;

        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        foreach (var team in teams)
        {
            var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, team.TeamId, ct);
            if (existing is { Source: MealConfirmationSource.Confirmed }) continue;

            var attendanceCount = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);
            var mealCount = await _meals.CountWillEatAsync(meal.DailyMealId, team.TeamId, ct);
            var alreadyReminded = existing?.RemindedAt is not null;
            Upsert(meal, existing, team.TeamId, mealCount, attendanceCount,
                MealConfirmationSource.Attendance, confirmedAt: null, remindedAt: _clock.Now);

            if (alreadyReminded) continue;
            var leaderIds = await _meals.GetTeamLeaderIdsAsync(team.TeamId, ct);
            if (leaderIds.Count > 0)
                _notifier.NotifyEmployees(leaderIds, new NotificationMessage(NotificationSources.System,
                    Messages.NotifyMealReminderTitle,
                    string.Format(Messages.NotifyMealReminderBody, team.TeamName, date.ToString("dd/MM/yyyy"), mealCount,
                        (await _settings.GetTimeAsync(SettingKeys.MealCutoffTime, new TimeOnly(8, 30), ct)).ToString("HH:mm")),
                    MealLink, nameof(TeamMealConfirmation)));
        }

        await _meals.SaveChangesAsync(ct);
        _logger.LogInformation("JOB-02: đã nhắc các tổ chưa xác nhận suất ăn ngày {MealDate}", date);
    }

    public async Task RunAutoCloseJobAsync(DateOnly date, CancellationToken ct)
    {
        await _seeder.SeedForDateAsync(date, ct);

        var meal = await _meals.GetOrCreateAsync(date, ct);
        if (meal.Status != MealStatus.Open) return;

        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        foreach (var team in teams)
        {
            var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, team.TeamId, ct);
            var attendanceCount = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);
            var mealCount = await _meals.CountWillEatAsync(meal.DailyMealId, team.TeamId, ct);

            if (existing is { Source: MealConfirmationSource.Confirmed })
            {
                // Giữ Source CONFIRMED nhưng cập nhật MealCount theo checklist mới nhất.
                existing.MealCount = mealCount;
                existing.AttendanceCount = attendanceCount;
                continue;
            }

            Upsert(meal, existing, team.TeamId, mealCount, attendanceCount,
                MealConfirmationSource.Attendance, confirmedAt: null, remindedAt: null);
        }

        await _meals.SaveChangesAsync(ct);

        // Sau seed: OfficeCount = số VP còn tick (có thể = 0 nếu tất cả bỏ suất).
        meal.OfficeCount = await _meals.CountOfficeWillEatAsync(meal.DailyMealId, ct);

        var health = await _attendance.GetHealthAsync(ct);
        meal.AttendanceIncomplete = health.Data?.IsOverdue ?? false;

        meal.Status = MealStatus.AutoClosed;
        meal.AutoClosedAt = _clock.Now;

        await _meals.SaveChangesAsync(ct);
        _logger.LogInformation(
            "JOB-03: đã chốt suất ăn ngày {MealDate} — OfficeCount={OfficeCount}, " +
            "AttendanceIncomplete={AttendanceIncomplete}",
            date, meal.OfficeCount, meal.AttendanceIncomplete);
    }

    // ---------- Dùng chung ----------

    private static string? ToPascalOrNull(string? code) => code is null ? null : CodeText.ToPascal(code);

    private async Task<bool> IsLockedAsync(DailyMeal meal, CancellationToken ct)
    {
        if (meal.Status != MealStatus.Open) return true;
        if (meal.MealDate != _clock.Today) return false;

        var cutoff = await _settings.GetTimeAsync(SettingKeys.MealCutoffTime, new TimeOnly(8, 30), ct);
        return TimeOnly.FromDateTime(_clock.Now) >= cutoff;
    }

    private void Upsert(DailyMeal meal, TeamMealConfirmation? existing, int teamId, int mealCount,
        int attendanceCount, string source, DateTime? confirmedAt, DateTime? remindedAt)
    {
        if (existing is null)
        {
            existing = new TeamMealConfirmation { DailyMealId = meal.DailyMealId, TeamId = teamId };
            _meals.AddConfirmation(existing);
        }

        existing.MealCount = mealCount;
        existing.AttendanceCount = attendanceCount;
        existing.Source = source;
        if (confirmedAt is not null) existing.ConfirmedAt = confirmedAt;
        if (remindedAt is not null) existing.RemindedAt = remindedAt;
    }

    /// <summary>Đồng bộ số tổng tổ sau khi đổi tick từng người.</summary>
    private async Task SyncTeamConfirmationAsync(
        DailyMeal meal, int teamId, DateOnly date, bool markConfirmed, CancellationToken ct)
    {
        var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, teamId, ct);
        var attendanceCount = await _meals.GetTeamAttendanceCountAsync(teamId, date, ct);
        var mealCount = await _meals.CountWillEatAsync(meal.DailyMealId, teamId, ct);

        // CountWillEat dùng AsNoTracking nên chưa thấy WillEat vừa sửa trên tracked entity → chỉnh tay ±1 khi cần.
        // Đơn giản hơn: đếm lại từ tracked team registrations.
        var regs = await _meals.GetTeamRegistrationsAsync(meal.DailyMealId, teamId, ct);
        mealCount = regs.Count(r => r.WillEat);

        if (markConfirmed || existing is { Source: MealConfirmationSource.Confirmed })
        {
            Upsert(meal, existing, teamId, mealCount, attendanceCount,
                MealConfirmationSource.Confirmed,
                confirmedAt: existing?.ConfirmedAt ?? _clock.Now, remindedAt: null);
        }
        else
        {
            Upsert(meal, existing, teamId, mealCount, attendanceCount,
                MealConfirmationSource.Attendance, confirmedAt: null, remindedAt: null);
        }
    }

    private async Task<IReadOnlyList<TeamMealRowDto>> BuildTeamRowsAsync(DailyMeal meal, DateOnly date, CancellationToken ct)
    {
        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        var confirmations = (await _meals.GetConfirmationsAsync(meal.DailyMealId, ct))
            .ToDictionary(c => c.TeamId);

        var rows = new List<TeamMealRowDto>(teams.Count);
        foreach (var team in teams)
        {
            var willEat = await _meals.CountWillEatAsync(meal.DailyMealId, team.TeamId, ct);
            var attendance = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);

            if (confirmations.TryGetValue(team.TeamId, out var c))
            {
                // Khi còn OPEN ưu tiên số WillEat mới nhất; đã chốt dùng MealCount đã lưu.
                var count = meal.Status == MealStatus.Open ? willEat : c.MealCount;
                rows.Add(new TeamMealRowDto(team.TeamId, team.TeamName, count, c.AttendanceCount,
                    ToPascalOrNull(c.Source), c.ConfirmedAt));
                continue;
            }

            rows.Add(new TeamMealRowDto(team.TeamId, team.TeamName, willEat, attendance, null, null));
        }

        return rows;
    }
}
