using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class MealServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTime Now0800 = new(2026, 10, 21, 8, 0, 0);

    private readonly IMealRepository _repo = Substitute.For<IMealRepository>();
    private readonly IAttendanceService _attendance = Substitute.For<IAttendanceService>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly MealService _sut;

    public MealServiceTests()
    {
        _clock.Now.Returns(Now0800);
        _clock.Today.Returns(Today);
        _settings.GetTimeAsync(SettingKeys.MealCutoffTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TimeOnly(8, 30));
        _sut = new MealService(_repo, _attendance, _settings, _notifier, _clock, NullLogger<MealService>.Instance);
    }

    private static Team Team(int id = 3, string name = "Tổ may 1") =>
        new() { TeamId = id, TeamCode = "MAY1", TeamName = name, TeamType = TeamTypes.Sewing, Status = RecordStatus.Active };

    private static DailyMeal Meal(int id = 1, string status = MealStatus.Open) =>
        new() { DailyMealId = id, MealDate = Today, Status = status };

    // ---------- GetTeamSuggestionAsync ----------

    [Fact]
    public async Task GetTeamSuggestionAsync_TeamNotFound_ReturnsNotFound()
    {
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns((Team?)null);

        var result = await _sut.GetTeamSuggestionAsync(3, Today, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.MealTeamNotFound);
    }

    [Fact]
    public async Task GetTeamSuggestionAsync_NoDailyMealYet_ReturnsSuggestionWithoutConfirmation()
    {
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns(Team());
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(12);
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns((DailyMeal?)null);

        var result = await _sut.GetTeamSuggestionAsync(3, Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.SuggestedCount.Should().Be(12);
        result.Data.MealCount.Should().BeNull();
        result.Data.IsLocked.Should().BeFalse();
    }

    // ---------- ConfirmTeamMealAsync ----------

    [Fact]
    public async Task ConfirmTeamMealAsync_TeamNotFound_ReturnsNotFound()
    {
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns((Team?)null);

        var result = await _sut.ConfirmTeamMealAsync(3, new ConfirmTeamMealDto(Today, 10), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MealTeamNotFound);
    }

    [Fact]
    public async Task ConfirmTeamMealAsync_AlreadyClosed_ReturnsLockedFailure()
    {
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns(Team());
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal(status: MealStatus.AutoClosed));

        var result = await _sut.ConfirmTeamMealAsync(3, new ConfirmTeamMealDto(Today, 10), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.MealLocked);        // MSG22
        result.Message.Should().Be(Messages.MealLocked);
    }

    [Fact]
    public async Task ConfirmTeamMealAsync_PastCutoffTime_ReturnsLockedFailure()
    {
        _clock.Now.Returns(new DateTime(2026, 10, 21, 8, 31, 0));
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns(Team());
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal());

        var result = await _sut.ConfirmTeamMealAsync(3, new ConfirmTeamMealDto(Today, 10), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MealLocked);
    }

    [Fact]
    public async Task ConfirmTeamMealAsync_NewConfirmation_AddsRow()
    {
        var meal = Meal();
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns(Team());
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(9);
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns((TeamMealConfirmation?)null);

        var result = await _sut.ConfirmTeamMealAsync(3, new ConfirmTeamMealDto(Today, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Received(1).AddConfirmation(Arg.Is<TeamMealConfirmation>(c =>
            c.TeamId == 3 && c.MealCount == 10 && c.AttendanceCount == 9
            && c.Source == MealConfirmationSource.Confirmed && c.ConfirmedAt != null));
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmTeamMealAsync_ReplacesExistingSuggestion_UpdatesCountAndSource()
    {
        var meal = Meal();
        var existing = new TeamMealConfirmation
        {
            DailyMealId = meal.DailyMealId, TeamId = 3, MealCount = 8, AttendanceCount = 8,
            Source = MealConfirmationSource.Attendance,
        };
        _repo.GetTeamAsync(3, Arg.Any<CancellationToken>()).Returns(Team());
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(8);
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.ConfirmTeamMealAsync(3, new ConfirmTeamMealDto(Today, 11), CancellationToken.None);

        existing.MealCount.Should().Be(11);
        existing.Source.Should().Be(MealConfirmationSource.Confirmed);
        existing.ConfirmedAt.Should().NotBeNull();
        _repo.DidNotReceiveWithAnyArgs().AddConfirmation(default!);
    }

    // ---------- SetGuestMealAsync ----------

    [Fact]
    public async Task SetGuestMealAsync_Locked_ReturnsGuestLockedFailure()
    {
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal(status: MealStatus.AutoClosed));

        var result = await _sut.SetGuestMealAsync(new SetGuestMealDto(Today, 5, null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MealGuestLocked);
        result.Message.Should().Be(Messages.MealGuestLocked);
    }

    [Fact]
    public async Task SetGuestMealAsync_Open_SavesGuestCountAndNote()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);

        var result = await _sut.SetGuestMealAsync(new SetGuestMealDto(Today, 7, "Khách công ty mẹ"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        meal.GuestCount.Should().Be(7);
        meal.GuestNote.Should().Be("Khách công ty mẹ");
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- GetSummaryAsync ----------

    [Fact]
    public async Task GetSummaryAsync_UnconfirmedTeam_ShowsSuggestionWithNullSource()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.GetConfirmationsAsync(meal.DailyMealId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TeamMealConfirmation>());
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(6);

        var result = await _sut.GetSummaryAsync(Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Teams.Should().ContainSingle(t => t.TeamId == 3 && t.MealCount == 6 && t.Source == null);
    }

    [Fact]
    public async Task GetSummaryAsync_NoMealPriceYet_SuggestsPreviousDayPrice()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Team>());
        _repo.GetConfirmationsAsync(meal.DailyMealId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TeamMealConfirmation>());
        _repo.GetPreviousMealPriceAsync(Today, Arg.Any<CancellationToken>()).Returns(25000m);

        var result = await _sut.GetSummaryAsync(Today, CancellationToken.None);

        result.Data!.MealPrice.Should().BeNull();
        result.Data.SuggestedMealPrice.Should().Be(25000m);
    }

    // ---------- MarkKitchenNotifiedAsync ----------

    [Fact]
    public async Task MarkKitchenNotifiedAsync_NotFound_ReturnsNotFound()
    {
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns((DailyMeal?)null);

        var result = await _sut.MarkKitchenNotifiedAsync(Today, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.MealNotFound);
    }

    [Fact]
    public async Task MarkKitchenNotifiedAsync_StillOpen_ReturnsNotClosedYetFailure()
    {
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal());

        var result = await _sut.MarkKitchenNotifiedAsync(Today, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MealNotClosedYet);
    }

    [Fact]
    public async Task MarkKitchenNotifiedAsync_Closed_SetsStatusAndTimestamp()
    {
        var meal = Meal(status: MealStatus.AutoClosed);
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);

        var result = await _sut.MarkKitchenNotifiedAsync(Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        meal.Status.Should().Be(MealStatus.KitchenNotified);
        meal.KitchenNotifiedAt.Should().Be(Now0800);
    }

    // ---------- SetMealPriceAsync ----------

    [Fact]
    public async Task SetMealPriceAsync_StillOpen_ReturnsNotClosedYetFailure()
    {
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal());

        var result = await _sut.SetMealPriceAsync(new SetMealPriceDto(Today, 20000m), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.MealNotClosedYet);
    }

    [Fact]
    public async Task SetMealPriceAsync_Closed_SavesPrice()
    {
        var meal = Meal(status: MealStatus.AutoClosed);
        _repo.FindByDateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);

        var result = await _sut.SetMealPriceAsync(new SetMealPriceDto(Today, 22000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        meal.MealPrice.Should().Be(22000m);
    }

    // ---------- RunReminderJobAsync (JOB-02) ----------

    [Fact]
    public async Task RunReminderJobAsync_AlreadyClosed_DoesNothing()
    {
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal(status: MealStatus.AutoClosed));

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RunReminderJobAsync_UnconfirmedTeam_CreatesAttendanceRowAndStampsRemindedAt()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns((TeamMealConfirmation?)null);
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(5);

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        _repo.Received(1).AddConfirmation(Arg.Is<TeamMealConfirmation>(c =>
            c.TeamId == 3 && c.MealCount == 5 && c.Source == MealConfirmationSource.Attendance
            && c.RemindedAt == Now0800 && c.ConfirmedAt == null));
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // DEV-MI-04-1: nhắc thật bằng INotificationService cho tổ trưởng + tổ phó.

    [Fact]
    public async Task RunReminderJobAsync_UnconfirmedTeam_NotifiesLeaderAndDeputy()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns((TeamMealConfirmation?)null);
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(5);
        _repo.GetTeamLeaderIdsAsync(3, Arg.Any<CancellationToken>()).Returns(new[] { 11, 12 });

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        _notifier.Received(1).NotifyEmployees(
            Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 11, 12 })),
            Arg.Is<NotificationMessage>(m => m.SourceType == NotificationSources.System
                && m.Title == Messages.NotifyMealReminderTitle && m.Message.Contains("21/10/2026") && m.Message.Contains("(5)")));
    }

    [Fact]
    public async Task RunReminderJobAsync_TeamWithoutLeader_SendsNothingButStillRecordsReminder()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns((TeamMealConfirmation?)null);
        _repo.GetTeamLeaderIdsAsync(3, Arg.Any<CancellationToken>()).Returns(Array.Empty<int>());

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        _notifier.DidNotReceiveWithAnyArgs().NotifyEmployees(default!, default!);
        _repo.Received(1).AddConfirmation(Arg.Is<TeamMealConfirmation>(c => c.RemindedAt == Now0800));
    }

    [Fact]
    public async Task RunReminderJobAsync_AlreadyRemindedTeam_DoesNotNotifyTwice()
    {
        var meal = Meal();
        var reminded = new TeamMealConfirmation
        {
            DailyMealId = meal.DailyMealId, TeamId = 3, MealCount = 5, Source = MealConfirmationSource.Attendance,
            RemindedAt = Now0800.AddMinutes(-1),
        };
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns(reminded);
        _repo.GetTeamLeaderIdsAsync(3, Arg.Any<CancellationToken>()).Returns(new[] { 11 });

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        _notifier.DidNotReceiveWithAnyArgs().NotifyEmployees(default!, default!);
    }

    [Fact]
    public async Task RunReminderJobAsync_AlreadyConfirmedTeam_IsSkipped()
    {
        var meal = Meal();
        var confirmed = new TeamMealConfirmation
        {
            DailyMealId = meal.DailyMealId, TeamId = 3, MealCount = 10, Source = MealConfirmationSource.Confirmed,
            ConfirmedAt = Now0800,
        };
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns(confirmed);

        await _sut.RunReminderJobAsync(Today, CancellationToken.None);

        confirmed.RemindedAt.Should().BeNull();
    }

    // ---------- RunAutoCloseJobAsync (JOB-03) ----------

    [Fact]
    public async Task RunAutoCloseJobAsync_AlreadyClosed_DoesNothing()
    {
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(Meal(status: MealStatus.AutoClosed));

        await _sut.RunAutoCloseJobAsync(Today, CancellationToken.None);

        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await _attendance.DidNotReceiveWithAnyArgs().GetHealthAsync(default);
    }

    [Fact]
    public async Task RunAutoCloseJobAsync_UnconfirmedTeam_FillsFromAttendanceAndLocksDay()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns((TeamMealConfirmation?)null);
        _repo.GetTeamAttendanceCountAsync(3, Today, Arg.Any<CancellationToken>()).Returns(8);
        _repo.GetOfficeAttendanceCountAsync(Today, Arg.Any<CancellationToken>()).Returns(4);
        _attendance.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(
            Result<SyncHealthDto>.Success(new SyncHealthDto(Now0800, 5, 30, false, 0)));

        await _sut.RunAutoCloseJobAsync(Today, CancellationToken.None);

        meal.Status.Should().Be(MealStatus.AutoClosed);
        meal.OfficeCount.Should().Be(4);
        meal.AutoClosedAt.Should().Be(Now0800);
        meal.AttendanceIncomplete.Should().BeFalse();
        _repo.Received(1).AddConfirmation(Arg.Is<TeamMealConfirmation>(c =>
            c.TeamId == 3 && c.MealCount == 8 && c.Source == MealConfirmationSource.Attendance));
    }

    [Fact]
    public async Task RunAutoCloseJobAsync_ConfirmedTeam_KeepsLeaderValue()
    {
        var meal = Meal();
        var confirmed = new TeamMealConfirmation
        {
            DailyMealId = meal.DailyMealId, TeamId = 3, MealCount = 15, Source = MealConfirmationSource.Confirmed,
            ConfirmedAt = Now0800,
        };
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { Team() });
        _repo.FindConfirmationAsync(meal.DailyMealId, 3, Arg.Any<CancellationToken>()).Returns(confirmed);
        _repo.GetOfficeAttendanceCountAsync(Today, Arg.Any<CancellationToken>()).Returns(2);
        _attendance.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(
            Result<SyncHealthDto>.Success(new SyncHealthDto(Now0800, 5, 30, false, 0)));

        await _sut.RunAutoCloseJobAsync(Today, CancellationToken.None);

        confirmed.MealCount.Should().Be(15);
        _repo.DidNotReceiveWithAnyArgs().AddConfirmation(default!);
    }

    [Fact]
    public async Task RunAutoCloseJobAsync_AttendanceSyncOverdue_FlagsAttendanceIncomplete()
    {
        var meal = Meal();
        _repo.GetOrCreateAsync(Today, Arg.Any<CancellationToken>()).Returns(meal);
        _repo.GetConfirmableTeamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Team>());
        _repo.GetOfficeAttendanceCountAsync(Today, Arg.Any<CancellationToken>()).Returns(0);
        _attendance.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(
            Result<SyncHealthDto>.Success(new SyncHealthDto(null, null, 30, true, 0), Messages.AttendanceSyncOverdue));

        await _sut.RunAutoCloseJobAsync(Today, CancellationToken.None);

        meal.AttendanceIncomplete.Should().BeTrue();
    }
}

public class MealValidatorTests
{
    private readonly ConfirmTeamMealDtoValidator _confirm = new();
    private readonly SetGuestMealDtoValidator _guest = new();
    private readonly SetMealPriceDtoValidator _price = new();

    [Fact]
    public void Confirm_NegativeMealCount_HasError() =>
        _confirm.TestValidate(new ConfirmTeamMealDto(new DateOnly(2026, 10, 21), -1))
            .ShouldHaveValidationErrorFor(x => x.MealCount);

    [Fact]
    public void Confirm_Valid_HasNoError() =>
        _confirm.TestValidate(new ConfirmTeamMealDto(new DateOnly(2026, 10, 21), 10))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Guest_NegativeCount_HasError() =>
        _guest.TestValidate(new SetGuestMealDto(new DateOnly(2026, 10, 21), -1, null))
            .ShouldHaveValidationErrorFor(x => x.GuestCount);

    [Fact]
    public void Guest_Valid_HasNoError() =>
        _guest.TestValidate(new SetGuestMealDto(new DateOnly(2026, 10, 21), 3, "Khách công ty mẹ"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Price_Negative_HasError() =>
        _price.TestValidate(new SetMealPriceDto(new DateOnly(2026, 10, 21), -1m))
            .ShouldHaveValidationErrorFor(x => x.MealPrice);

    [Fact]
    public void Price_Valid_HasNoError() =>
        _price.TestValidate(new SetMealPriceDto(new DateOnly(2026, 10, 21), 20000m))
            .ShouldNotHaveAnyValidationErrors();
}
