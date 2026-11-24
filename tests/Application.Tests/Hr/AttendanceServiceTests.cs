using Application.Tests;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests.Hr;

/// <summary>
/// UTCID920–UTCID931: AttendanceService.AlertSyncOverdueAsync.
/// Assert khóa theo sheet AttendanceService (P2-1) — không nới Expected cho Actual lệch spec.
/// </summary>
public class AttendanceServiceTests
{
    private const string SpecOverdueLog = "Attendance sync overdue";

    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly IAttendanceRepository _attendance = Substitute.For<IAttendanceRepository>();
    private readonly IMealRegistrationSeeder _mealSeeder = Substitute.For<IMealRegistrationSeeder>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IExcelAttendanceParser _excel = Substitute.For<IExcelAttendanceParser>();
    private readonly ListLogger _logger = new();
    private readonly List<Notification> _notifications = [];

    public AttendanceServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _attendance.CountUnmappedCodesAsync(Arg.Any<CancellationToken>()).Returns(0);
        // Khóa chưa cấu hình → reader trả đúng fallback mà service truyền vào.
        _settings.GetIntAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<int>(1));
        _uow.SaveChangesAsync().Returns(1);

        var notifRepo = Substitute.For<IGenericRepository<Notification>>();
        notifRepo.Query().Returns(_ => _notifications.AsTestAsyncQueryable());
        _uow.Repository<Notification>().Returns(notifRepo);
    }

    private AttendanceService Sut() => new(
        _attendance, _mealSeeder, _settings, _uow, _notifier, _clock, _excel, _logger);

    /// <param name="configuredThreshold">null = setting không cấu hình (dùng fallback của service).</param>
    private void GivenLastSucceeded(DateTime? lastAt, int? configuredThreshold = 30)
    {
        _attendance.GetLastSucceededAtAsync(Arg.Any<CancellationToken>()).Returns(lastAt);
        if (configuredThreshold is int threshold)
        {
            _settings.GetIntAsync(SettingKeys.AttendanceAlertMinutes, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(threshold);
        }
    }

    private void SeedOverdueNotice(DateTime createdAt) =>
        _notifications.Add(new Notification
        {
            RefTable = NotificationRefs.AttendanceSyncOverdue,
            CreatedAt = createdAt,
            Title = Messages.NotifyAttendanceSyncOverdueTitle,
            Message = "đã báo",
            SourceType = NotificationSources.System,
            PushStatus = NotificationPushStatuses.Skipped,
        });

    [Fact]
    public async Task UTCID920_AlertSyncOverdueAsync_LastSuccess45Min_NotifiesAdminSavesAndLogs()
    {
        // Arrange — Threshold=30; LastSucceededAt=Now−45; chưa có notification AttendanceSyncOverdue
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — NotifyRole(Admin) 1 lần; SaveChanges 1 lần; LogWarning "Attendance sync overdue"
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin,
            Arg.Is<NotificationMessage>(m => m.RefTable == NotificationRefs.AttendanceSyncOverdue),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
        using (new AssertionScope())
        {
            _logger.Entries.Should().ContainSingle();
            var entry = _logger.Entries.Single();
            entry.Level.Should().Be(LogLevel.Warning);
            entry.Message.Should().Contain(SpecOverdueLog);
        }
    }

    [Fact]
    public async Task UTCID921_AlertSyncOverdueAsync_LastSuccess10Min_ReturnsWithoutNotify()
    {
        // Arrange — LastSucceededAt=Now−10 phút (trong ngưỡng)
        GivenLastSucceeded(Now.AddMinutes(-10), configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — return sớm; Notifier và SaveChanges không gọi
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
        await _uow.DidNotReceive().SaveChangesAsync();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID922_AlertSyncOverdueAsync_LastSuccessExactly30Min_DoesNotNotify()
    {
        // Arrange — LastSucceededAt=Now−30 phút đúng (biên `>`)
        GivenLastSucceeded(Now.AddMinutes(-30), configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — không overdue (`>`); không notify
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID923_AlertSyncOverdueAsync_LastSuccess31Min_NotifiesAdmin()
    {
        // Arrange — LastSucceededAt=Now−31 phút
        GivenLastSucceeded(Now.AddMinutes(-31), configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — overdue; notify Admin
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID924_AlertSyncOverdueAsync_ThresholdUnset_UsesDefault30AndNotifies()
    {
        // Arrange — setting threshold không cấu hình; LastSucceededAt=Now−31 phút
        GivenLastSucceeded(Now.AddMinutes(-31), configuredThreshold: null);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — dùng mặc định 30 → notify
        await _settings.Received(1).GetIntAsync(
            SettingKeys.AttendanceAlertMinutes, 30, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID925_AlertSyncOverdueAsync_NeverSynced_FirstCall_NotifiesAdminOnce()
    {
        // Arrange — chưa từng sync (lastAt=null); chưa có notification; gọi lần 1
        GivenLastSucceeded(null, configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — overdue; notify Admin 1 lần
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin,
            Arg.Is<NotificationMessage>(m => m.RefTable == NotificationRefs.AttendanceSyncOverdue),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(
            Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID926_AlertSyncOverdueAsync_NeverSynced_SecondCall_DoesNotNotifyAgain()
    {
        // Arrange — lastAt=null; đã có notification từ lần 1 (since=MinValue); gọi lần 2
        GivenLastSucceeded(null, configuredThreshold: 30);
        SeedOverdueNotice(Now);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — không notify lại (rủi ro #1: im đến khi có sync thành công)
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID927_AlertSyncOverdueAsync_NotifiedThisEpisode_DoesNotNotify()
    {
        // Arrange — LastSucceededAt=Now−45; notification CreatedAt=Now−5 (> since)
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);
        SeedOverdueNotice(Now.AddMinutes(-5));

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — không notify (đã báo đợt này)
        await _notifier.DidNotReceiveWithAnyArgs().NotifyRoleAsync(default!, default!, default);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID928_AlertSyncOverdueAsync_OldNotificationBeforeNewEpisode_NotifiesAgain()
    {
        // Arrange — notification cũ CreatedAt=Now−3 giờ, trước LastSucceededAt=Now−45
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);
        SeedOverdueNotice(Now.AddHours(-3));

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — notify mới (đợt overdue mới)
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID929_AlertSyncOverdueAsync_NotifierThrows_PropagatesAndSkipsSave()
    {
        // Arrange — overdue; mock Notifier ném exception
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);
        _notifier.NotifyRoleAsync(Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("notify failed")));

        // Act
        var act = () => Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — exception propagate; SaveChanges không gọi (hàm trả Task, không Result)
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("notify failed");
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID930_AlertSyncOverdueAsync_SaveThrows_PropagatesAfterNotify()
    {
        // Arrange — overdue; Notifier OK; SaveChanges ném exception
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);
        _uow.SaveChangesAsync().Returns(Task.FromException<int>(new InvalidOperationException("save failed")));

        // Act
        var act = () => Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — exception propagate. Notify và SaveChanges tách nhau (rủi ro #5)
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID931_AlertSyncOverdueAsync_Overdue_NotifiesAdminOnlyNotManager()
    {
        // Arrange — overdue
        GivenLastSucceeded(Now.AddMinutes(-45), configuredThreshold: 30);

        // Act
        await Sut().AlertSyncOverdueAsync(CancellationToken.None);

        // Assert — chỉ role Admin nhận; Manager không nhận (rủi ro #4). Xác nhận spec
        await _notifier.Received(1).NotifyRoleAsync(
            Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(
            RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyRoleAsync(
            RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        _notifier.DidNotReceiveWithAnyArgs().NotifyEmployees(default!, default!);
        await _notifier.DidNotReceiveWithAnyArgs().NotifyTeamLeadersAsync(default, default!, default);
    }

    private sealed class ListLogger : ILogger<AttendanceService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
