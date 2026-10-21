using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>Dữ liệu giả + người dùng giả dùng chung cho test luồng may + QC (KI-01…KI-11).</summary>
public abstract class ProductionTestBase
{
    /// <summary>Thứ Tư 21/10/2026 10:00.</summary>
    protected static readonly DateTime Now = new(2026, 10, 21, 10, 0, 0);
    protected static readonly DateOnly Today = new(2026, 10, 21);

    protected readonly IUnitOfWork Uow = Substitute.For<IUnitOfWork>();
    protected readonly ICurrentUser User = Substitute.For<ICurrentUser>();
    protected readonly IClock Clock = Substitute.For<IClock>();
    protected readonly ISystemSettingReader Settings = Substitute.For<ISystemSettingReader>();
    protected readonly IAuditLogService Audit = Substitute.For<IAuditLogService>();
    protected readonly INotificationService Notifier = Substitute.For<INotificationService>();

    protected ProductionTestBase()
    {
        Clock.Now.Returns(Now);
        Clock.Today.Returns(Today);
        Settings.GetTimeAsync(SettingKeys.LunchStartTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TimeOnly(11, 30));
        Settings.GetTimeAsync(SettingKeys.WorkEndTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TimeOnly(16, 30));
        User.EmployeeId.Returns(900);
        AsTeamLeader();
    }

    protected ProductionGuard Guard() => new(Uow, User, Clock, Settings);

    /// <summary>Gắn danh sách giả làm nguồn truy vấn của một bảng; lọc / sum / any chạy như EF thật.</summary>
    protected void SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        Uow.Repository<T>().Returns(repo);
    }

    protected IGenericRepository<T> Repo<T>() where T : class => Uow.Repository<T>();

    protected void AsTeamLeader(int teamId = 5, string role = Roles.TeamLeader)
    {
        User.Role.Returns(role);
        User.TeamId.Returns(teamId);
        User.TeamTypeCode.Returns(TeamTypes.Sewing);
    }

    protected void AsCuttingTeamLeader()
    {
        User.Role.Returns(Roles.TeamLeader);
        User.TeamId.Returns(2);
        User.TeamTypeCode.Returns(TeamTypes.Cutting);
    }

    protected void AsManager()
    {
        User.Role.Returns(Roles.Manager);
        User.TeamId.Returns((int?)null);
        User.TeamTypeCode.Returns((string?)null);
    }

    protected void AsQaQc()
    {
        User.Role.Returns(Roles.QaQc);
        User.TeamId.Returns((int?)null);
        User.TeamTypeCode.Returns((string?)null);
    }
}
