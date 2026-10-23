using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KH-12 / UC05, UC20: hộp thư thông báo và thông báo nhanh tới toàn xưởng / tổ / vai trò.</summary>
public class NotificationCenterTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 8, 0, 0);
    private const int Me = 10;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Notification> _notifications = new();
    private readonly List<Employee> _employees = new();
    private readonly List<Announcement> _announcements = new();
    private readonly List<Team> _teams = new();
    private readonly List<Role> _roles = new();

    public NotificationCenterTests()
    {
        _clock.Now.Returns(Now);
        _user.EmployeeId.Returns(Me);
        _roles.Add(new Role { RoleId = 1, RoleCode = RoleCodes.Worker, RoleName = "Công nhân" });
        _roles.Add(new Role { RoleId = 2, RoleCode = RoleCodes.Manager, RoleName = "Quản đốc" });
        _teams.Add(new Team { TeamId = 5, TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active });
        _teams.Add(new Team { TeamId = 6, TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        Employee(Me, roleId: 2, teamId: null);          // người gửi
        Employee(11, roleId: 1, teamId: 5);
        Employee(12, roleId: 1, teamId: 5);
        Employee(13, roleId: 1, teamId: 6);
        Employee(14, roleId: 1, teamId: 5, status: RecordStatus.Resigned);   // đã nghỉ: không nhận
        Setup(_notifications); Setup(_employees); Setup(_announcements); Setup(_teams); Setup(_roles);
        _uow.Repository<Notification>().AddRangeAsync(Arg.Do<IEnumerable<Notification>>(n => _notifications.AddRange(n)));
        _uow.Repository<Announcement>().AddAsync(Arg.Do<Announcement>(a =>
        {
            a.AnnouncementId = _announcements.Count + 1;
            a.Sender = _employees.First(e => e.EmployeeId == a.SenderId);
            _announcements.Add(a);
        }));
    }

    private void Employee(int id, int roleId, int? teamId, string status = RecordStatus.Active) =>
        _employees.Add(new Employee { EmployeeId = id, RoleId = roleId, TeamId = teamId, Status = status, FullName = $"NV {id}" });

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private NotificationInboxService Inbox() => new(_uow, _user, _clock);
    private AnnouncementService Announcements() => new(_uow, _audit, _clock);

    private Notification Note(int recipient, bool read = false, string title = "T")
    {
        var n = new Notification { NotificationId = _notifications.Count + 1, RecipientId = recipient, SourceType = NotificationSources.System, Title = title, Message = "m", IsRead = read, CreatedAt = Now };
        _notifications.Add(n);
        return n;
    }

    // ---------- Hộp thư ----------

    [Fact]
    public async Task The_inbox_shows_only_my_notifications_newest_first()
    {
        Note(Me, title: "cũ"); Note(99, title: "của người khác"); Note(Me, title: "mới");

        var page = (await Inbox().ListMineAsync(new NotificationQuery(), default)).Data!;

        page.Items.Select(n => n.Title).Should().Equal("mới", "cũ");
    }

    [Fact]
    public async Task The_inbox_can_be_limited_to_unread_and_counts_unread()
    {
        Note(Me, read: true); Note(Me); Note(Me); Note(99);

        var unread = (await Inbox().ListMineAsync(new NotificationQuery { UnreadOnly = true }, default)).Data!;
        var count = (await Inbox().UnreadCountAsync(default)).Data!;

        unread.Items.Should().HaveCount(2);
        count.Count.Should().Be(2);
    }

    [Fact]
    public async Task Marking_read_works_for_my_notification_and_not_for_someone_elses()
    {
        var mine = Note(Me);
        var other = Note(99);

        (await Inbox().MarkReadAsync(mine.NotificationId, default)).IsSuccess.Should().BeTrue();
        (await Inbox().MarkReadAsync(other.NotificationId, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);

        mine.IsRead.Should().BeTrue();
        mine.ReadAt.Should().Be(Now);
        other.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Mark_all_read_only_touches_my_notifications()
    {
        var a = Note(Me); var b = Note(Me); var other = Note(99);

        await Inbox().MarkAllReadAsync(default);

        a.IsRead.Should().BeTrue();
        b.IsRead.Should().BeTrue();
        other.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task An_urgent_announcement_is_flagged_in_the_inbox()
    {
        var announcement = new Announcement { AnnouncementId = 1, Content = "x", Priority = AnnouncementPriorities.Urgent, SenderId = Me };
        _announcements.Add(announcement);
        var n = Note(Me); n.AnnouncementId = 1; n.Announcement = announcement;
        Note(Me);

        var items = (await Inbox().ListMineAsync(new NotificationQuery(), default)).Data!.Items;

        items.Single(i => i.NotificationId == n.NotificationId).IsUrgent.Should().BeTrue();
        items.Count(i => i.IsUrgent).Should().Be(1);
    }

    // ---------- Gửi thông báo nhanh ----------

    private static SendAnnouncementDto Send(string scope, int[]? teams = null, int[]? roles = null, string priority = AnnouncementPriorities.Normal) =>
        new(" Họp lúc 15h ", priority, scope, teams, roles);

    [Fact]
    public async Task A_workshop_wide_announcement_reaches_every_active_employee_except_the_sender()
    {
        var result = await Announcements().SendAsync(Send(AnnouncementScopes.All), Me, default);

        result.IsSuccess.Should().BeTrue();
        _notifications.Select(n => n.RecipientId).Should().BeEquivalentTo(new[] { 11, 12, 13 });
        _notifications.Should().OnlyContain(n => n.SourceType == NotificationSources.Announcement && n.PushStatus == NotificationPushStatuses.Skipped
            && n.Message == "Họp lúc 15h" && n.Title == "Thông báo" && n.AnnouncementId == 1);
        result.Message.Should().Contain("3");
    }

    [Fact]
    public async Task A_team_announcement_only_reaches_the_active_members_of_those_teams()
    {
        await Announcements().SendAsync(Send(AnnouncementScopes.Team, teams: new[] { 5 }), Me, default);

        _notifications.Select(n => n.RecipientId).Should().BeEquivalentTo(new[] { 11, 12 });
        _announcements.Single().AnnouncementTargets.Select(t => t.TeamId).Should().Equal(5);
    }

    [Fact]
    public async Task A_role_announcement_only_reaches_employees_with_those_roles()
    {
        await Announcements().SendAsync(Send(AnnouncementScopes.Role, roles: new[] { 2 }), 11, default);

        _notifications.Select(n => n.RecipientId).Should().Equal(Me);
        _announcements.Single().AnnouncementTargets.Single().RoleId.Should().Be(2);
    }

    [Fact]
    public async Task An_urgent_announcement_gets_the_urgent_title()
    {
        await Announcements().SendAsync(Send(AnnouncementScopes.All, priority: AnnouncementPriorities.Urgent), Me, default);

        _notifications.Should().OnlyContain(n => n.Title == "Thông báo khẩn");
        _announcements.Single().Priority.Should().Be(AnnouncementPriorities.Urgent);
    }

    [Fact]
    public async Task Team_and_role_announcements_need_a_target_and_someone_to_receive_it()
    {
        (await Announcements().SendAsync(Send(AnnouncementScopes.Team), Me, default)).ErrorCode.Should().Be(ErrorCodes.AnnouncementInvalid);
        (await Announcements().SendAsync(Send(AnnouncementScopes.Role, roles: Array.Empty<int>()), Me, default)).ErrorCode.Should().Be(ErrorCodes.AnnouncementInvalid);
        (await Announcements().SendAsync(Send(AnnouncementScopes.Team, teams: new[] { 99 }), Me, default)).ErrorCode.Should().Be(ErrorCodes.AnnouncementInvalid);
        (await Announcements().SendAsync(Send("EVERYONE"), Me, default)).ErrorCode.Should().Be(ErrorCodes.AnnouncementInvalid);
        _notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task The_content_is_limited_to_the_notification_message_size()
    {
        var result = await Announcements().SendAsync(new SendAnnouncementDto(new string('x', 1001), AnnouncementPriorities.Normal, AnnouncementScopes.All, null, null), Me, default);

        result.ErrorCode.Should().Be(ErrorCodes.AnnouncementInvalid);
    }

    [Fact]
    public async Task Targets_list_active_teams_and_all_roles()
    {
        _teams.Add(new Team { TeamId = 7, TeamName = "Tổ cũ", TeamType = TeamTypes.Sewing, Status = RecordStatus.Discontinued });

        var targets = (await Announcements().GetTargetsAsync(default)).Data!;

        targets.Teams.Select(t => t.TeamId).Should().BeEquivalentTo(new[] { 5, 6 });
        targets.Roles.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_requires_content_priority_and_scope()
    {
        var v = new SendAnnouncementDtoValidator();
        v.Validate(Send(AnnouncementScopes.All)).IsValid.Should().BeTrue();
        v.Validate(Send(AnnouncementScopes.All) with { Content = " " }).IsValid.Should().BeFalse();
        v.Validate(Send(AnnouncementScopes.All, priority: "LOW")).IsValid.Should().BeFalse();
        v.Validate(Send("X")).IsValid.Should().BeFalse();
    }
}
