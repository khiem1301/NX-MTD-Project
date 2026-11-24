using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID860–UTCID919: TeamService per UNIT_TEST_CASES_REBASELINE_v3.1 sheet TeamService (P0-4).</summary>
public class TeamServiceTests
{
    private const int A = 1, B = 2, C = 3, D = 4, Office1 = 5;
    private const int E1 = 1, E2 = 2, E3 = 3, E4 = 4, E5 = 5, E6 = 6, E7 = 7, E8 = 8, E9 = 9;
    private const int WorkerRole = 9, LeaderRole = 8, DeputyRole = 7;
    private const string SpecSettledSuffix = "đã quyết toán việc tổ may";

    private static readonly DateTime Now = new(2026, 11, 24, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IDailyWorkAllocationService _daily = Substitute.For<IDailyWorkAllocationService>();

    private readonly Dictionary<int, Team> _teamsById = new();
    private readonly Dictionary<int, Employee> _people = new();
    private readonly Dictionary<int, TeamDetailDto> _details = new();
    private readonly Dictionary<int, int> _activeCounts = new();
    private readonly HashSet<string> _duplicateCodes = new(StringComparer.Ordinal);
    private IReadOnlyList<EmployeeLookupDto> _candidates = Array.Empty<EmployeeLookupDto>();
    private Func<int, IReadOnlyList<int>, Result> _settle = (_, _) => Result.Success();
    private Team? _created;

    public TeamServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.Role.Returns(Roles.Admin);
        _user.EmployeeId.Returns(1);
        _user.AccountId.Returns(1);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _uow.SaveChangesAsync().Returns(1);
        _audit.GetHistoryAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogDto> { new(1, AuditActions.Update, null, null, null, 1, "Admin", Now) });

        _teams.GetByIdAsync(Arg.Any<object>()).Returns(ci =>
        {
            var id = Convert.ToInt32(ci.Arg<object>());
            return _teamsById.TryGetValue(id, out var team) ? team : null;
        });
        _teams.GetDetailAsync(default, default, default).ReturnsForAnyArgs(ci =>
            _details.TryGetValue(ci.ArgAt<int>(0), out var detail) ? detail : null);
        _teams.CodeExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ci => _duplicateCodes.Contains(ci.ArgAt<string>(0)));
        _teams.CountActiveMembersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => _activeCounts.TryGetValue(ci.Arg<int>(), out var count) ? count : 0);
        _teams.When(t => t.AddAsync(Arg.Any<Team>())).Do(ci =>
        {
            var team = ci.Arg<Team>();
            if (team.TeamId == 0) team.TeamId = 50;
            _created = team;
        });

        _employees.GetFullAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => _people.TryGetValue(ci.Arg<int>(), out var person) ? person : null);
        _employees.GetActiveByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ActivePeople(ci.ArgAt<IReadOnlyCollection<int>>(0)));
        _employees.LookupUnassignedAsync(default!, default).ReturnsForAnyArgs(_ => _candidates);
        _employees.GetRoleIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<string>() switch
            {
                RoleCodes.Worker => WorkerRole,
                RoleCodes.TeamLeader => LeaderRole,
                RoleCodes.TeamDeputy => DeputyRole,
                _ => 0,
            });

        _daily.SettleOnTeamLeaveAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => _settle(ci.ArgAt<int>(0), ci.ArgAt<IReadOnlyList<int>>(1)));
    }

    private TeamService Sut() => new(_teams, _employees, _audit, _uow, _clock, new EmployeeScope(_user), _daily);

    private Team SeedTeam(int id, string type, string code, string status = RecordStatus.Active) 
    {
        var team = new Team
        {
            TeamId = id, TeamCode = code, TeamType = type, Status = status, CreatedAt = Now,
        };
        _teamsById[id] = team;
        return team;
    }

    private Employee SeedPerson(int id, string roleCode, int? teamId, string status = RecordStatus.Active)
    {
        var person = new Employee
        {
            EmployeeId = id,
            EmployeeCode = $"E{id}",
            FullName = $"E{id}",
            TeamId = teamId,
            Status = status,
            PayType = PayTypes.PieceRate,
            StartDate = new DateOnly(2026, 1, 1),
            RoleId = roleCode switch
            {
                RoleCodes.TeamLeader => LeaderRole,
                RoleCodes.TeamDeputy => DeputyRole,
                _ => WorkerRole,
            },
            Role = new Role { RoleCode = roleCode, RoleName = roleCode },
            Account = new Account
            {
                AccountId = id, EmployeeId = id, Username = $"E{id}", PasswordHash = "x",
                SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active,
            },
        };
        _people[id] = person;
        return person;
    }

    private List<Employee> ActivePeople(IReadOnlyCollection<int> ids) =>
        ids.Where(id => _people.TryGetValue(id, out var person) && person.Status == RecordStatus.Active)
            .Distinct()
            .Select(id => _people[id])
            .ToList();

    private void SeedList(params TeamListItemDto[] rows) =>
        _teams.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(rows);

    private static TeamListItemDto Item(int id, string code, string type, string status = "Active") =>
        new(id, code, type, status, null, null, 0);

    private static TeamMemberDto Member(int id, string role) =>
        new(id, $"E{id}", $"E{id}", role, role, "PieceRate", null);

    private static bool SameIds(IReadOnlyList<int> ids, params int[] expected) =>
        ids.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x));

    private async Task AssertCommittedOnce()
    {
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
    }

    private async Task AssertRolledBack()
    {
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    private async Task AssertNoSettle() =>
        await _daily.DidNotReceiveWithAnyArgs().SettleOnTeamLeaveAsync(default, default!, default);

    private static void AssertNotFound(Result result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID860_ListAsync_TypeSewing_ReturnsTwoSewingTeams()
    {
        // Arrange — 4 tổ: 2 Sewing, 1 Cutting, 1 Office (1 Discontinued). Query Type=Sewing.
        SeedList(
            Item(A, "MAY-A", "Sewing"),
            Item(B, "MAY-B", "Sewing"),
            Item(C, "CAT-C", "Cutting", "Discontinued"),
            Item(Office1, "VP-1", "Office"));

        // Act — query Type=Sewing
        var result = await Sut().ListAsync(includeDiscontinued: true, type: "Sewing", default);

        // Assert — Success; 2 tổ may
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(2, "query Type=Sewing returns the 2 sewing teams");
        result.Data.Should().OnlyContain(t => t.TeamType == "Sewing");
    }

    [Fact]
    public async Task UTCID861_ListAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        SeedList();

        // Act
        var result = await Sut().ListAsync(includeDiscontinued: false, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task UTCID862_ListAsync_UnknownTypeFoo_ValidationOrEmpty_NoException()
    {
        // Arrange
        SeedList(Item(A, "MAY-A", "Sewing"), Item(Office1, "VP-1", "Office"));
        Result<IReadOnlyList<TeamListItemDto>>? result = null;

        // Act — query Type=Foo
        var ex = await Record.ExceptionAsync(async () => result = await Sut().ListAsync(false, type: "Foo", default));

        // Assert — Failure (Validation) hoặc list rỗng; không exception
        ex.Should().BeNull();
        result.Should().NotBeNull();
        if (result!.IsSuccess)
            result.Data.Should().BeEmpty("query Type=Foo → list rỗng khi không Failure Validation");
        else
            result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID863_GetAsync_TeamWithLeaderAndMembers_ReturnsDetail()
    {
        // Arrange — tổ A: 5 thành viên, 1 tổ trưởng
        _details[A] = new TeamDetailDto(A, "MAY-A", "Sewing", "Active", new[]
        {
            Member(E6, Roles.TeamLeader),
            Member(E1, Roles.Worker),
            Member(E2, Roles.Worker),
            Member(3, Roles.Worker),
            Member(4, Roles.Worker),
        }, Array.Empty<AuditLogDto>());

        // Act
        var result = await Sut().GetAsync(A, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Members.Should().HaveCount(5);
        result.Data.Members.Count(m => m.Role == Roles.TeamLeader).Should().Be(1);
        result.Data.History.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UTCID864_GetAsync_TeamWithoutMembers_LeaderNull()
    {
        // Arrange
        _details[C] = new TeamDetailDto(C, "CAT-C", "Cutting", "Active", Array.Empty<TeamMemberDto>(), Array.Empty<AuditLogDto>());

        // Act
        var result = await Sut().GetAsync(C, default);

        // Assert — Members rỗng; Leader=null
        result.IsSuccess.Should().BeTrue();
        result.Data!.Members.Should().BeEmpty();
        result.Data.Members.Should().NotContain(m => m.Role == Roles.TeamLeader);
    }

    [Fact]
    public async Task UTCID865_GetAsync_UnknownTeam_NotFound()
    {
        // Act
        var result = await Sut().GetAsync(999, default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID866_CreateAsync_SewingTeam_ActiveAndNoSettle()
    {
        // Act
        var result = await Sut().CreateAsync(new SaveTeamDto("Tổ may 5", "Sewing"), default);

        // Assert — Success; tổ Active; Settle không gọi
        result.IsSuccess.Should().BeTrue();
        _created.Should().NotBeNull();
        _created!.Status.Should().Be(RecordStatus.Active);
        _created.TeamType.Should().Be(TeamTypes.Sewing);
        _created.TeamCode.Should().Be("Tổ may 5".Trim().ToUpperInvariant());
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID867_CreateAsync_NameLength100Succeeds_101FailsValidation()
    {
        // Act
        var ok = await Sut().CreateAsync(new SaveTeamDto(new string('A', 100), "Sewing"), default);

        // Assert — Name = 100 ký tự → Success
        ok.IsSuccess.Should().BeTrue();
        _created.Should().NotBeNull();
        _created!.TeamCode.Should().HaveLength(100);
        _created.Status.Should().Be(RecordStatus.Active);

        // Act — Name 101 ký tự → Failure (Validation)
        var bad = await Sut().CreateAsync(new SaveTeamDto(new string('B', 101), "Sewing"), default);

        // Assert
        bad.IsSuccess.Should().BeFalse("Name 101 ký tự → Failure (Validation)");
        bad.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID868_CreateAsync_DuplicateName_Fails()
    {
        // Arrange
        _duplicateCodes.Add("Tổ may 5".Trim().ToUpperInvariant());

        // Act
        var result = await Sut().CreateAsync(new SaveTeamDto("Tổ may 5", "Sewing"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamCodeDuplicate);
        await _teams.DidNotReceive().AddAsync(Arg.Any<Team>());
    }

    [Fact]
    public async Task UTCID869_CreateAsync_UnknownTypeFoo_Validation()
    {
        // Act
        var result = await Sut().CreateAsync(new SaveTeamDto("MAY5", "Foo"), default);

        // Assert
        result.IsSuccess.Should().BeFalse("Type=Foo → Failure (Validation)");
        result.Kind.Should().Be(ResultKind.Validation);
        await _teams.DidNotReceive().AddAsync(Arg.Any<Team>());
    }

    [Fact]
    public async Task UTCID870_UpdateAsync_Rename_SucceedsWithoutSettle()
    {
        // Arrange
        var team = SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().UpdateAsync(A, new SaveTeamDto("Tổ may A1", "Sewing"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.TeamCode.Should().Be("Tổ may A1".Trim().ToUpperInvariant());
        team.TeamType.Should().Be(TeamTypes.Sewing);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID871_UpdateAsync_ChangeTypeWhenNoActiveMembers_Succeeds()
    {
        // Arrange
        var team = SeedTeam(C, TeamTypes.Sewing, "TO-C");
        _activeCounts[C] = 0;

        // Act
        var result = await Sut().UpdateAsync(C, new SaveTeamDto("TO-C", "Cutting"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.TeamType.Should().Be(TeamTypes.Cutting);
    }

    [Fact]
    public async Task UTCID872_UpdateAsync_ChangeTypeWithActiveMembers_Fails()
    {
        // Arrange
        var team = SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        _activeCounts[A] = 5;

        // Act
        var result = await Sut().UpdateAsync(A, new SaveTeamDto("MAY-A", "Office"), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamHasActiveMembers);
        team.TeamType.Should().Be(TeamTypes.Sewing);
    }

    [Fact]
    public async Task UTCID873_UpdateAsync_UnknownTeam_NotFound()
    {
        // Act
        var result = await Sut().UpdateAsync(999, new SaveTeamDto("MAY-X", "Sewing"), default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID874_DiscontinueAsync_NoActiveMembers_SetsDiscontinued()
    {
        // Arrange
        var team = SeedTeam(C, TeamTypes.Cutting, "CAT-C");
        _activeCounts[C] = 0;

        // Act
        var result = await Sut().DiscontinueAsync(C, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Status.Should().Be(RecordStatus.Discontinued);
    }

    [Fact]
    public async Task UTCID875_DiscontinueAsync_OnlyResignedMember_Succeeds()
    {
        // Arrange — 1 Resigned, 0 Active. Chỉ tính Active.
        var team = SeedTeam(D, TeamTypes.Sewing, "MAY-D");
        SeedPerson(E9, RoleCodes.Worker, D, RecordStatus.Resigned);
        _activeCounts[D] = 0;

        // Act
        var result = await Sut().DiscontinueAsync(D, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Status.Should().Be(RecordStatus.Discontinued);
    }

    [Fact]
    public async Task UTCID876_DiscontinueAsync_ActiveMembersRemain_Fails()
    {
        // Arrange
        var team = SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        _activeCounts[A] = 5;

        // Act
        var result = await Sut().DiscontinueAsync(A, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamHasActiveMembers);
        team.Status.Should().Be(RecordStatus.Active);
    }

    [Fact]
    public async Task UTCID877_DiscontinueAsync_AlreadyDiscontinued_InvalidStatusOrIdempotent()
    {
        // Arrange
        var team = SeedTeam(C, TeamTypes.Cutting, "CAT-C", RecordStatus.Discontinued);

        // Act
        var result = await Sut().DiscontinueAsync(C, default);

        // Assert — Failure InvalidStatus hoặc idempotent
        if (result.IsSuccess)
            result.IsSuccess.Should().BeTrue();
        else
            result.ErrorCode.Should().Be(ErrorCodes.TeamNotActive);
        team.Status.Should().Be(RecordStatus.Discontinued);
    }

    [Fact]
    public async Task UTCID878_DiscontinueAsync_UnknownTeam_NotFound()
    {
        // Act
        var result = await Sut().DiscontinueAsync(999, default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID879_ReactivateAsync_DiscontinuedTeam_BecomesActive()
    {
        // Arrange
        var team = SeedTeam(C, TeamTypes.Cutting, "CAT-C", RecordStatus.Discontinued);

        // Act
        var result = await Sut().ReactivateAsync(C, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Status.Should().Be(RecordStatus.Active);
    }

    [Fact]
    public async Task UTCID880_ReactivateAsync_DiscontinuedEmptyTeam_NoSettle()
    {
        // Arrange
        var team = SeedTeam(C, TeamTypes.Cutting, "CAT-C", RecordStatus.Discontinued);

        // Act
        var result = await Sut().ReactivateAsync(C, default);

        // Assert — Members vẫn rỗng; Settle không gọi
        result.IsSuccess.Should().BeTrue();
        team.Status.Should().Be(RecordStatus.Active);
        _people.Values.Should().NotContain(p => p.TeamId == C);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID881_ReactivateAsync_AlreadyActive_InvalidStatusOrIdempotent()
    {
        // Arrange
        var team = SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().ReactivateAsync(A, default);

        // Assert — Failure InvalidStatus hoặc idempotent
        if (!result.IsSuccess)
            result.ErrorCode.Should().Be(ErrorCodes.TeamNotActive);
        team.Status.Should().Be(RecordStatus.Active);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID882_ReactivateAsync_UnknownTeam_NotFound()
    {
        // Act
        var result = await Sut().ReactivateAsync(999, default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID883_TransferInAsync_TwoSewingSources_SettlesEachGroupAndSuffix()
    {
        // Arrange — E1, E2 tổ may A; E3 tổ may C; đích B Sewing
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var e2 = SeedPerson(E2, RoleCodes.Worker, A);
        var e3 = SeedPerson(E3, RoleCodes.Worker, C);
        var stamps = new[] { e1.Account!.SecurityStamp, e2.Account!.SecurityStamp, e3.Account!.SecurityStamp };

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1, E2, E3 }, Today, "bổ sung"), default);

        // Assert — 1 TX; Settle(A,[E1,E2]) 1 lần; Settle(C,[E3]) 1 lần; TeamId=B; stamp + Audit + LogTransfer
        result.IsSuccess.Should().BeTrue();
        await AssertCommittedOnce();
        await _daily.Received(1).SettleOnTeamLeaveAsync(A,
            Arg.Is<IReadOnlyList<int>>(ids => SameIds(ids, E1, E2)), Arg.Any<CancellationToken>());
        await _daily.Received(1).SettleOnTeamLeaveAsync(C,
            Arg.Is<IReadOnlyList<int>>(ids => SameIds(ids, E3)), Arg.Any<CancellationToken>());
        e1.TeamId.Should().Be(B);
        e2.TeamId.Should().Be(B);
        e3.TeamId.Should().Be(B);
        e1.Account.SecurityStamp.Should().NotBe(stamps[0]);
        e2.Account.SecurityStamp.Should().NotBe(stamps[1]);
        e3.Account.SecurityStamp.Should().NotBe(stamps[2]);
        _audit.Received(3).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Employee) && e.Reason != null && e.Reason.Contains("24/11/2026")));
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Team) && e.RecordId == B && e.Summary != null && e.Summary.StartsWith("Nhận chuyển đến:")));
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Team) && e.RecordId == A && e.Summary != null && e.Summary.StartsWith("Chuyển sang tổ")));
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Team) && e.RecordId == C && e.Summary != null && e.Summary.StartsWith("Chuyển sang tổ")));
        result.Message.Should().Contain(SpecSettledSuffix);
    }

    [Fact]
    public async Task UTCID884_TransferInAsync_FromOffice_SettlesWithoutSuffix()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(Office1, TeamTypes.Office, "VP-1");
        var e4 = SeedPerson(E4, RoleCodes.Worker, Office1);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E4 }, Today, null), default);

        // Assert — Settle(Office1,[E4]) gọi; Message không có suffix
        result.IsSuccess.Should().BeTrue();
        e4.TeamId.Should().Be(B);
        await _daily.Received(1).SettleOnTeamLeaveAsync(Office1,
            Arg.Is<IReadOnlyList<int>>(ids => SameIds(ids, E4)), Arg.Any<CancellationToken>());
        result.Message.Should().NotContain(SpecSettledSuffix);
        result.Message.Should().NotContain(Messages.TeamTransferSettledSuffix);
    }

    [Fact]
    public async Task UTCID885_TransferInAsync_EffectiveDateToday_Succeeds()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1 }, Today, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e1.TeamId.Should().Be(B);
    }

    [Fact]
    public async Task UTCID886_TransferInAsync_FutureDate_ValidationAndNoSettle()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1 }, Today.AddDays(1), null), default);

        // Assert — ngày tương lai; không Settle
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamTransferFutureDate);
        e1.TeamId.Should().Be(A);
        await AssertNoSettle();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID887_TransferInAsync_PastDate_SucceedsAndAuditsTheDate()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var past = new DateOnly(2026, 11, 1);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1 }, past, null), default);

        // Assert — ngày chỉ ghi vào audit
        result.IsSuccess.Should().BeTrue();
        e1.TeamId.Should().Be(B);
        _audit.Received().Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(Employee) && e.RecordId == E1 && e.Reason != null && e.Reason.Contains("01/11/2026")));
    }

    [Fact]
    public async Task UTCID888_TransferInAsync_UnassignedEmployee_NoSettle()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        var e5 = SeedPerson(E5, RoleCodes.Worker, null);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E5 }, Today, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e5.TeamId.Should().Be(B);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID889_TransferInAsync_EmptyEmployeeIds_Validation()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(Array.Empty<int>(), Today, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse("EmployeeIds=[] → Failure (Validation)");
        result.Kind.Should().Be(ResultKind.Validation);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID890_TransferInAsync_WorkerIntoOffice_Succeeds()
    {
        // Arrange — spec: không chặn loại tổ đích
        SeedTeam(Office1, TeamTypes.Office, "VP-1");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(Office1, new TransferMembersDto(new[] { E1 }, Today, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e1.TeamId.Should().Be(Office1);
    }

    [Fact]
    public async Task UTCID891_TransferInAsync_IncludesLeader_RejectsEveryone()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var e6 = SeedPerson(E6, RoleCodes.TeamLeader, A);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1, E6 }, Today, null), default);

        // Assert — không ai bị đổi tổ
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamTransferLeader);
        e1.TeamId.Should().Be(A);
        e6.TeamId.Should().Be(A);
        await AssertNoSettle();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID892_TransferInAsync_Deputy_Fails()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e7 = SeedPerson(E7, RoleCodes.TeamDeputy, C);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E7 }, Today, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamTransferLeader);
        e7.TeamId.Should().Be(C);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID893_TransferInAsync_DuplicateIds_ValidationOrDedupe()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1, E1 }, Today, null), default);

        // Assert — Failure (Validation: trùng) hoặc tự loại trùng. NotFound không phải kết quả khóa.
        if (result.IsSuccess)
        {
            e1.TeamId.Should().Be(B);
            await _daily.Received(1).SettleOnTeamLeaveAsync(A,
                Arg.Is<IReadOnlyList<int>>(ids => SameIds(ids, E1)), Arg.Any<CancellationToken>());
        }
        else
        {
            result.Kind.Should().Be(ResultKind.Validation, "duplicate EmployeeIds are Validation or deduped; NotFound is not the locked expected");
            e1.TeamId.Should().Be(A);
        }
    }

    [Fact]
    public async Task UTCID894_TransferInAsync_UnknownEmployee_NotFound()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { 999 }, Today, null), default);

        // Assert
        AssertNotFound(result);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID895_TransferInAsync_UnknownTarget_NotFound()
    {
        // Arrange
        SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(999, new TransferMembersDto(new[] { E1 }, Today, null), default);

        // Assert
        AssertNotFound(result);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID896_TransferInAsync_DiscontinuedTarget_InvalidStatus()
    {
        // Arrange
        SeedTeam(C, TeamTypes.Sewing, "MAY-C", RecordStatus.Discontinued);
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().TransferInAsync(C, new TransferMembersDto(new[] { E1 }, Today, null), default);

        // Assert — InvalidStatus (TEAM_NOT_ACTIVE)
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamNotActive);
        e1.TeamId.Should().Be(A);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID897_TransferInAsync_SecondSettleBusy_RollsBackBoth()
    {
        // Arrange — Settle(A) Success, Settle(C) TeamOrderBusy
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var e3 = SeedPerson(E3, RoleCodes.Worker, C);
        _settle = (teamId, _) => teamId == C
            ? Result.Failure(ErrorCodes.TeamOrderBusy, Messages.OutputLockBusy)
            : Result.Success();

        // Act
        var result = await Sut().TransferInAsync(B, new TransferMembersDto(new[] { E1, E3 }, Today, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        e1.TeamId.Should().Be(A);
        e3.TeamId.Should().Be(C);
        await AssertRolledBack();
    }

    [Fact]
    public async Task UTCID898_RemoveMembersAsync_SewingWorkers_SettlesThenClearsTeam()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var e2 = SeedPerson(E2, RoleCodes.Worker, A);

        // Act
        var result = await Sut().RemoveMembersAsync(A, new TeamMembersDto(new[] { E1, E2 }, null), default);

        // Assert — Settle(A,[E1,E2]) rồi TeamId=null; Message có suffix tổ may
        result.IsSuccess.Should().BeTrue();
        await AssertCommittedOnce();
        await _daily.Received(1).SettleOnTeamLeaveAsync(A,
            Arg.Is<IReadOnlyList<int>>(ids => SameIds(ids, E1, E2)), Arg.Any<CancellationToken>());
        e1.TeamId.Should().BeNull();
        e2.TeamId.Should().BeNull();
        result.Message.Should().Contain(SpecSettledSuffix);
    }

    [Fact]
    public async Task UTCID899_RemoveMembersAsync_OfficeWorker_ClearsTeamWithoutSuffix()
    {
        // Arrange
        SeedTeam(Office1, TeamTypes.Office, "VP-1");
        var e4 = SeedPerson(E4, RoleCodes.Worker, Office1);

        // Act
        var result = await Sut().RemoveMembersAsync(Office1, new TeamMembersDto(new[] { E4 }, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e4.TeamId.Should().BeNull();
        result.Message.Should().NotContain(SpecSettledSuffix);
        result.Message.Should().NotContain(Messages.TeamTransferSettledSuffix);
    }

    [Fact]
    public async Task UTCID900_RemoveMembersAsync_EmptyEmployeeIds_Validation()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().RemoveMembersAsync(A, new TeamMembersDto(Array.Empty<int>(), null), default);

        // Assert
        result.IsSuccess.Should().BeFalse("EmployeeIds=[] → Failure (Validation)");
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID901_RemoveMembersAsync_IncludesLeader_RemovesNobody()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        var e6 = SeedPerson(E6, RoleCodes.TeamLeader, A);

        // Act
        var result = await Sut().RemoveMembersAsync(A, new TeamMembersDto(new[] { E1, E6 }, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamRemoveLeader);
        e1.TeamId.Should().Be(A);
        e6.TeamId.Should().Be(A);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID902_RemoveMembersAsync_EmployeeFromOtherTeam_NotInTeam()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e3 = SeedPerson(E3, RoleCodes.Worker, C);

        // Act
        var result = await Sut().RemoveMembersAsync(A, new TeamMembersDto(new[] { E3 }, null), default);

        // Assert — NotFound hoặc không thuộc tổ
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().BeOneOf(ErrorCodes.CommonNotFound, ErrorCodes.TeamMemberNotInTeam);
        e3.TeamId.Should().Be(C);
    }

    [Fact]
    public async Task UTCID903_RemoveMembersAsync_SettleBusy_RollsBack()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        _settle = (_, _) => Result.Failure(ErrorCodes.TeamOrderBusy, Messages.OutputLockBusy);

        // Act
        var result = await Sut().RemoveMembersAsync(A, new TeamMembersDto(new[] { E1 }, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        e1.TeamId.Should().Be(A);
        await AssertRolledBack();
    }

    [Fact]
    public async Task UTCID904_RemoveMembersAsync_UnknownTeam_NotFound()
    {
        // Act
        var result = await Sut().RemoveMembersAsync(999, new TeamMembersDto(new[] { E1 }, null), default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID905_AddMembersAsync_UnassignedWorkers_AssignsTeamWithoutSettle()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e5 = SeedPerson(E5, RoleCodes.Worker, null);
        var e8 = SeedPerson(E8, RoleCodes.Worker, null);

        // Act
        var result = await Sut().AddMembersAsync(A, new TeamMembersDto(new[] { E5, E8 }, null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e5.TeamId.Should().Be(A);
        e8.TeamId.Should().Be(A);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID906_AddMembersAsync_EmptyEmployeeIds_Validation()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().AddMembersAsync(A, new TeamMembersDto(Array.Empty<int>(), null), default);

        // Assert
        result.IsSuccess.Should().BeFalse("EmployeeIds=[] → Failure (Validation)");
        result.Kind.Should().Be(ResultKind.Validation);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID907_AddMembersAsync_EmployeeAlreadyOnTeam_Fails()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().AddMembersAsync(B, new TeamMembersDto(new[] { E1 }, null), default);

        // Assert — phải dùng TransferIn
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamMemberHasTeam);
        e1.TeamId.Should().Be(A);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID908_AddMembersAsync_DiscontinuedTeam_InvalidStatus()
    {
        // Arrange
        SeedTeam(C, TeamTypes.Sewing, "MAY-C", RecordStatus.Discontinued);
        var e5 = SeedPerson(E5, RoleCodes.Worker, null);

        // Act
        var result = await Sut().AddMembersAsync(C, new TeamMembersDto(new[] { E5 }, null), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamNotActive);
        e5.TeamId.Should().BeNull();
    }

    [Fact]
    public async Task UTCID909_AppointAsync_ReplacesLeaderInsideTransaction()
    {
        // Arrange — E6 Leader, E1 Worker cùng tổ A
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e6 = SeedPerson(E6, RoleCodes.TeamLeader, A);
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);
        _employees.FindTeamRoleHolderAsync(A, RoleCodes.TeamLeader, E1, Arg.Any<CancellationToken>()).Returns(e6);

        // Act
        var result = await Sut().AppointAsync(A, new AppointDto(E1, TeamPositions.Leader), default);

        // Assert — E6 về Worker, E1 lên Leader
        result.IsSuccess.Should().BeTrue();
        await AssertCommittedOnce();
        e6.RoleId.Should().Be(WorkerRole);
        e1.RoleId.Should().Be(LeaderRole);
        e6.TeamId.Should().Be(A);
        e1.TeamId.Should().Be(A);
        e6.PayType.Should().Be(PayTypes.PieceRate);
        e1.PayType.Should().Be(PayTypes.PieceRate);
    }

    [Fact]
    public async Task UTCID910_AppointAsync_NoCurrentLeader_PromotesOnlyTarget()
    {
        // Arrange
        SeedTeam(B, TeamTypes.Sewing, "MAY-B");
        var e2 = SeedPerson(E2, RoleCodes.Worker, B);

        // Act
        var result = await Sut().AppointAsync(B, new AppointDto(E2, TeamPositions.Leader), default);

        // Assert — chỉ nâng E2
        result.IsSuccess.Should().BeTrue();
        e2.RoleId.Should().Be(LeaderRole);
        e2.TeamId.Should().Be(B);
        await _uow.Received(1).SaveChangesAsync();
        await AssertCommittedOnce();
    }

    [Fact]
    public async Task UTCID911_AppointAsync_EmployeeNotInTeam_Fails()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e3 = SeedPerson(E3, RoleCodes.Worker, C);

        // Act
        var result = await Sut().AppointAsync(A, new AppointDto(E3, TeamPositions.Leader), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamMemberNotInTeam);
        e3.RoleId.Should().Be(WorkerRole);
        e3.TeamId.Should().Be(C);
    }

    [Fact]
    public async Task UTCID912_AppointAsync_UnknownTeam_NotFound()
    {
        // Arrange
        SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().AppointAsync(999, new AppointDto(E1, TeamPositions.Leader), default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID913_DismissAsync_LeaderBecomesWorkerAndStays()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e6 = SeedPerson(E6, RoleCodes.TeamLeader, A);

        // Act
        var result = await Sut().DismissAsync(A, new DismissDto(E6), default);

        // Assert — về Worker, vẫn ở tổ A; Settle không gọi
        result.IsSuccess.Should().BeTrue();
        e6.RoleId.Should().Be(WorkerRole);
        e6.TeamId.Should().Be(A);
        await AssertNoSettle();
    }

    [Fact]
    public async Task UTCID914_DismissAsync_DeputyBecomesWorkerAndStays()
    {
        // Arrange
        SeedTeam(C, TeamTypes.Sewing, "MAY-C");
        var e7 = SeedPerson(E7, RoleCodes.TeamDeputy, C);

        // Act
        var result = await Sut().DismissAsync(C, new DismissDto(E7), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        e7.RoleId.Should().Be(WorkerRole);
        e7.TeamId.Should().Be(C);
    }

    [Fact]
    public async Task UTCID915_DismissAsync_Worker_Fails()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");
        var e1 = SeedPerson(E1, RoleCodes.Worker, A);

        // Act
        var result = await Sut().DismissAsync(A, new DismissDto(E1), default);

        // Assert — không giữ chức vụ
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamDismissInvalidRole);
        e1.RoleId.Should().Be(WorkerRole);
        e1.TeamId.Should().Be(A);
    }

    [Fact]
    public async Task UTCID916_DismissAsync_UnknownEmployee_NotFound()
    {
        // Arrange
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().DismissAsync(A, new DismissDto(999), default);

        // Assert
        AssertNotFound(result);
    }

    [Fact]
    public async Task UTCID917_CandidatesAsync_ActiveUnassignedOnly()
    {
        // Arrange — E5, E8 Active TeamId=null; E9 Resigned TeamId=null (lookup không trả E9)
        _candidates = new[]
        {
            new EmployeeLookupDto(E5, "E5", "E5", Roles.Worker, null, "PieceRate"),
            new EmployeeLookupDto(E8, "E8", "E8", Roles.Worker, null, "PieceRate"),
        };

        // Act
        var result = await Sut().CandidatesAsync(default);

        // Assert — [E5, E8], không có E9
        result.IsSuccess.Should().BeTrue();
        result.Data!.Select(x => x.EmployeeId).Should().BeEquivalentTo(new[] { E5, E8 });
        result.Data.Should().NotContain(x => x.EmployeeId == E9);
    }

    [Fact]
    public async Task UTCID918_CandidatesAsync_NoFreeEmployees_EmptyList()
    {
        // Arrange
        _candidates = Array.Empty<EmployeeLookupDto>();
        SeedTeam(A, TeamTypes.Sewing, "MAY-A");

        // Act
        var result = await Sut().CandidatesAsync(default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task UTCID919_CandidatesAsync_UnknownTeam_NotFound()
    {
        // Arrange — teamId=999
        _candidates = Array.Empty<EmployeeLookupDto>();

        // Act
        var result = await Sut().CandidatesAsync(teamId: 999, default);

        // Assert
        AssertNotFound(result);
    }
}
