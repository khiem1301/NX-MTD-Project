using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC30 (suất ăn): tổ trưởng xác nhận số suất tổ (gợi ý từ chấm công), Kế toán nhập suất khách,
/// xem tổng hợp và nhập đơn giá. JOB-02 (08:15) và JOB-03 (08:30, MI-04) cũng gọi qua đây.
/// </summary>
public sealed class MealService : IMealService
{
    private readonly IMealRepository _meals;
    private readonly IAttendanceService _attendance;
    private readonly ISystemSettingReader _settings;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;
    private readonly ILogger<MealService> _logger;

    /// <summary>Màn xác nhận suất ăn của tổ trưởng (FE: MEAL_ROUTES.teamMeal).</summary>
    private const string MealLink = "/team/meals";

    public MealService(IMealRepository meals, IAttendanceService attendance, ISystemSettingReader settings,
        INotificationService notifier, IClock clock, ILogger<MealService> logger)
    {
        _meals = meals;
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

        var suggested = await _meals.GetTeamAttendanceCountAsync(teamId, date, ct);

        var meal = await _meals.FindByDateAsync(date, ct);
        TeamMealConfirmation? confirmation = null;
        var locked = false;
        if (meal is not null)
        {
            confirmation = await _meals.FindConfirmationAsync(meal.DailyMealId, teamId, ct);
            locked = await IsLockedAsync(meal, ct);
        }

        return Result<TeamMealSuggestionDto>.Success(new TeamMealSuggestionDto(
            date, teamId, team.TeamName, suggested,
            confirmation?.MealCount, ToPascalOrNull(confirmation?.Source), confirmation?.ConfirmedAt, locked));
    }

    public async Task<Result> ConfirmTeamMealAsync(int teamId, ConfirmTeamMealDto dto, CancellationToken ct)
    {
        var team = await _meals.GetTeamAsync(teamId, ct);
        if (team is null)
            return Result.Failure(ErrorCodes.MealTeamNotFound, Messages.MealTeamNotFound, ResultKind.NotFound);

        var meal = await _meals.GetOrCreateAsync(dto.MealDate, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealLocked, Messages.MealLocked);        // MSG22

        var attendanceCount = await _meals.GetTeamAttendanceCountAsync(teamId, dto.MealDate, ct);
        var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, teamId, ct);
        Upsert(meal, existing, teamId, dto.MealCount, attendanceCount,
            MealConfirmationSource.Confirmed, confirmedAt: _clock.Now, remindedAt: null);

        await _meals.SaveChangesAsync(ct);

        _logger.LogInformation("Tổ {TeamId} xác nhận {MealCount} suất ăn ngày {MealDate}",
            teamId, dto.MealCount, dto.MealDate);
        return Result.Success(Messages.Saved);
    }

    // ---------- Kế toán ----------

    public async Task<Result> SetGuestMealAsync(SetGuestMealDto dto, CancellationToken ct)
    {
        var meal = await _meals.GetOrCreateAsync(dto.MealDate, ct);
        if (await IsLockedAsync(meal, ct))
            return Result.Failure(ErrorCodes.MealGuestLocked, Messages.MealGuestLocked);   // MSG22

        meal.GuestCount = dto.GuestCount;
        meal.GuestNote = dto.GuestNote;
        await _meals.SaveChangesAsync(ct);

        return Result.Success(Messages.Saved);
    }

    public async Task<Result<DailyMealSummaryDto>> GetSummaryAsync(DateOnly date, CancellationToken ct)
    {
        var meal = await _meals.GetOrCreateAsync(date, ct);
        var rows = await BuildTeamRowsAsync(meal, date, ct);
        var suggestedPrice = meal.MealPrice ?? await _meals.GetPreviousMealPriceAsync(date, ct);

        // Tổng suất tính như vw_DailyMealTotal: suất các tổ + khối văn phòng + khách; thành tiền = tổng × đơn giá.
        var teamTotal = rows.Where(r => r.Source != null).Sum(r => r.MealCount);   // chỉ tổ đã có dòng xác nhận, không tính gợi ý
        var totalMeals = teamTotal + meal.OfficeCount + meal.GuestCount;
        var totalAmount = meal.MealPrice is null ? (decimal?)null : Math.Round(totalMeals * meal.MealPrice.Value, 2);

        return Result<DailyMealSummaryDto>.Success(new DailyMealSummaryDto(
            meal.DailyMealId, date, CodeText.ToPascal(meal.Status), meal.OfficeCount, meal.GuestCount, meal.GuestNote,
            teamTotal, totalMeals, meal.MealPrice, suggestedPrice, totalAmount,
            meal.AttendanceIncomplete, meal.AutoClosedAt, meal.ClosedAt, meal.KitchenNotifiedAt, rows));
    }

    public async Task<Result> MarkKitchenNotifiedAsync(DateOnly date, CancellationToken ct)
    {
        var meal = await _meals.FindByDateAsync(date, ct);
        if (meal is null)
            return Result.Failure(ErrorCodes.MealNotFound, Messages.MealNotFound, ResultKind.NotFound);
        if (meal.Status == MealStatus.Open)
            return Result.Failure(ErrorCodes.MealNotClosedYet, Messages.MealNotClosedYet);

        meal.Status = MealStatus.KitchenNotified;
        meal.KitchenNotifiedAt = _clock.Now;
        await _meals.SaveChangesAsync(ct);

        return Result.Success(Messages.MealKitchenNotified);
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

    // ---------- Job nền (MI-04) ----------

    public async Task RunReminderJobAsync(DateOnly date, CancellationToken ct)
    {
        var meal = await _meals.GetOrCreateAsync(date, ct);
        if (meal.Status != MealStatus.Open) return;   // đã chốt, không còn gì để nhắc

        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        foreach (var team in teams)
        {
            var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, team.TeamId, ct);
            if (existing is { Source: MealConfirmationSource.Confirmed }) continue;

            var count = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);
            var alreadyReminded = existing?.RemindedAt is not null;   // job chạy lại sau khi restart: không nhắc lần hai
            Upsert(meal, existing, team.TeamId, count, count,
                MealConfirmationSource.Attendance, confirmedAt: null, remindedAt: _clock.Now);

            if (alreadyReminded) continue;
            var leaderIds = await _meals.GetTeamLeaderIdsAsync(team.TeamId, ct);
            if (leaderIds.Count > 0)
                _notifier.NotifyEmployees(leaderIds, new NotificationMessage(NotificationSources.System,
                    Messages.NotifyMealReminderTitle,
                    string.Format(Messages.NotifyMealReminderBody, team.TeamName, date.ToString("dd/MM/yyyy"), count,
                        (await _settings.GetTimeAsync(SettingKeys.MealCutoffTime, new TimeOnly(8, 30), ct)).ToString("HH:mm")),
                    MealLink, nameof(TeamMealConfirmation)));
        }

        await _meals.SaveChangesAsync(ct);   // lưu cả dòng nhắc lẫn Notification (cùng DbContext) trong một lần
        _logger.LogInformation("JOB-02: đã nhắc các tổ chưa xác nhận suất ăn ngày {MealDate}", date);
    }

    public async Task RunAutoCloseJobAsync(DateOnly date, CancellationToken ct)
    {
        var meal = await _meals.GetOrCreateAsync(date, ct);
        if (meal.Status != MealStatus.Open) return;    // đã chốt rồi (job chạy lại do restart)

        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        foreach (var team in teams)
        {
            var existing = await _meals.FindConfirmationAsync(meal.DailyMealId, team.TeamId, ct);
            if (existing is { Source: MealConfirmationSource.Confirmed }) continue;   // giữ nguyên số tổ trưởng đã chốt

            var count = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);
            Upsert(meal, existing, team.TeamId, count, count,
                MealConfirmationSource.Attendance, confirmedAt: null, remindedAt: null);
        }

        meal.OfficeCount = await _meals.GetOfficeAttendanceCountAsync(date, ct);

        // "đồng bộ chấm công lần cuối" (AI Context 4.6): không có API nào ép RJ đồng bộ lại từ đây
        // (agent tự đẩy mỗi 10 phút), nên chỉ đọc tình trạng đồng bộ hiện tại để đánh dấu thiếu dữ liệu.
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

    /// <summary>Trả API dạng chuỗi PascalCase (Rules mục 2.2); null khi tổ chưa có dòng xác nhận.</summary>
    private static string? ToPascalOrNull(string? code) => code is null ? null : CodeText.ToPascal(code);

    /// <summary>Khóa khi đã qua trạng thái OPEN, hoặc hôm nay đã quá giờ chốt (phòng khi JOB-03 chưa kịp chạy);
    /// trigger của DB vẫn là tuyến chặn cuối cùng (MSG22).</summary>
    private async Task<bool> IsLockedAsync(DailyMeal meal, CancellationToken ct)
    {
        if (meal.Status != MealStatus.Open) return true;
        if (meal.MealDate != _clock.Today) return false;

        var cutoff = await _settings.GetTimeAsync(SettingKeys.MealCutoffTime, new TimeOnly(8, 30), ct);
        return TimeOnly.FromDateTime(_clock.Now) >= cutoff;
    }

    /// <summary>Thêm/sửa dòng xác nhận của một tổ (tổng suất do vw_DailyMealTotal cộng từ các dòng này).</summary>
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

    private async Task<IReadOnlyList<TeamMealRowDto>> BuildTeamRowsAsync(DailyMeal meal, DateOnly date, CancellationToken ct)
    {
        var teams = await _meals.GetConfirmableTeamsAsync(ct);
        var confirmations = (await _meals.GetConfirmationsAsync(meal.DailyMealId, ct))
            .ToDictionary(c => c.TeamId);

        var rows = new List<TeamMealRowDto>(teams.Count);
        foreach (var team in teams)
        {
            if (confirmations.TryGetValue(team.TeamId, out var c))
            {
                rows.Add(new TeamMealRowDto(team.TeamId, team.TeamName, c.MealCount, c.AttendanceCount,
                    ToPascalOrNull(c.Source), c.ConfirmedAt));
                continue;
            }

            var suggested = await _meals.GetTeamAttendanceCountAsync(team.TeamId, date, ct);
            rows.Add(new TeamMealRowDto(team.TeamId, team.TeamName, suggested, suggested, null, null));
        }

        return rows;
    }
}
