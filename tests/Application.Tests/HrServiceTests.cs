using System.Text.RegularExpressions;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Hr;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Security;

namespace Application.Tests;

public class HrServiceTests
{
    private static readonly byte[] RowVer = { 1 };

    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IGenericRepository<Account> _accounts = Substitute.For<IGenericRepository<Account>>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITemporaryPasswordGenerator _passwords = Substitute.For<ITemporaryPasswordGenerator>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public HrServiceTests()
    {
        _clock.Now.Returns(new DateTime(2026, 10, 24, 8, 0, 0));
        _clock.Today.Returns(new DateOnly(2026, 10, 24));
        _user.EmployeeId.Returns(1);
        _user.AccountId.Returns(1);
        _user.Role.Returns(Roles.Admin);
        _hasher.Hash(default!).ReturnsForAnyArgs(c => "hash:" + c.Arg<string>());
        _passwords.Generate().Returns("123456");
        _employees.GetRoleIdAsync(default!, default).ReturnsForAnyArgs(9);
    }

    private EmployeeScope Scope() => new(_user);

    private EmployeeService Employees() =>
        new(_employees, _teams, _hasher, _passwords, _audit, _uow, _user, _clock, Scope());

    private TeamService Teams() => new(_teams, _employees, _audit, _uow, _clock, Scope());

    private static Employee Person(int id, string role, int? team = 4, string pay = PayTypes.PieceRate) => new()
    {
        EmployeeId = id, EmployeeCode = $"E{id}", FullName = $"Người {id}", TeamId = team, Status = RecordStatus.Active,
        PayType = pay, StartDate = new DateOnly(2026, 1, 1), Role = new Role { RoleCode = role },
        Account = new Account { AccountId = id, SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active },
    };

    private static Team TeamOf(string type = TeamTypes.Sewing, int id = 4) =>
        new() { TeamId = id, TeamName = "Tổ may 1", TeamType = type, Status = RecordStatus.Active };

    private static CreateEmployeeDto NewEmployee(string role = "Worker", int? att = null) =>
        new("cn2", "Công nhân 2", null, null, null, role, 4, "PieceRate", new DateOnly(2026, 10, 24), att);

    // ---- Đổi mật khẩu ----
    [Fact]
    public async Task ChangePassword_WrongCurrent_ReturnsConflictCode()
    {
        var account = new Account { AccountId = 1, PasswordHash = "old", SecurityStamp = Guid.NewGuid() };
        _accounts.GetByIdAsync(1).Returns(account);
        _hasher.Verify(default!, default!).ReturnsForAnyArgs(false);
        var service = new ProfileService(_employees, _accounts, _auth, _hasher, _audit, _uow, _user);

        var result = await service.ChangePasswordAsync(new ChangePasswordDto("sai", "moi", "moi"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AuthWrongCurrentPassword);
        result.Kind.Should().Be(ResultKind.Conflict);
        account.PasswordHash.Should().Be("old");
    }

    [Fact]
    public async Task ChangePassword_Success_RotatesStampClearsTempAndReturnsTokens()
    {
        var oldStamp = Guid.NewGuid();
        var account = new Account { AccountId = 1, PasswordHash = "old", IsTemporaryPassword = true, SecurityStamp = oldStamp };
        _accounts.GetByIdAsync(1).Returns(account);
        _hasher.Verify(default!, default!).ReturnsForAnyArgs(true);
        _auth.IssueForAccountAsync(1, default).ReturnsForAnyArgs(Result<LoginResultDto>.Success(null!));
        var service = new ProfileService(_employees, _accounts, _auth, _hasher, _audit, _uow, _user);

        var result = await service.ChangePasswordAsync(new ChangePasswordDto("cu", "moi", "moi"), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.PasswordChanged);
        account.SecurityStamp.Should().NotBe(oldStamp);
        account.IsTemporaryPassword.Should().BeFalse();
        account.PasswordHash.Should().Be("hash:moi");
    }

    [Fact]
    public void ChangePasswordValidator_Mismatch_Msg05() =>
        new ChangePasswordDtoValidator().Validate(new ChangePasswordDto("a", "b", "c")).Errors
            .Should().Contain(e => e.ErrorMessage == Messages.PasswordMismatch);

    // ---- Tạo nhân viên ----
    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        _employees.CodeExistsAsync("CN2", default).ReturnsForAnyArgs(true);

        var result = await Employees().CreateAsync(NewEmployee(), default);

        result.ErrorCode.Should().Be(ErrorCodes.EmployeeCodeDuplicate);
    }

    [Fact]
    public async Task Create_SecondLeaderSameTeam_FailsWithHolderName()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf());
        _employees.FindTeamRoleHolderAsync(4, RoleCodes.TeamLeader, null, default).ReturnsForAnyArgs(Person(9, RoleCodes.TeamLeader));

        var result = await Employees().CreateAsync(NewEmployee("TeamLeader"), default);

        result.ErrorCode.Should().Be(ErrorCodes.TeamLeaderExists);
        result.Message.Should().Contain("Người 9");
    }

    [Fact]
    public async Task Create_LeaderInOfficeTeam_Fails()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf(TeamTypes.Office));

        var result = await Employees().CreateAsync(NewEmployee("TeamLeader"), default);

        result.ErrorCode.Should().Be(ErrorCodes.TeamLeaderInvalidTeam);
    }

    [Fact]
    public async Task Create_AttendanceCodeTaken_Fails()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf());
        _employees.AttendanceCodeTakenAsync(1002, null, default).ReturnsForAnyArgs(true);

        var result = await Employees().CreateAsync(NewEmployee(att: 1002), default);

        result.ErrorCode.Should().Be(ErrorCodes.EmployeeAttendanceCodeTaken);
    }

    [Fact]
    public async Task Create_ReturnsSixDigitTempPassword_HashedNotPlain_AndMapsCodeAfterFirstSave()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf());
        Employee? saved = null;
        _employees.When(x => x.AddAsync(Arg.Any<Employee>())).Do(c => saved = c.Arg<Employee>());

        var result = await Employees().CreateAsync(NewEmployee(att: 1002), default);

        result.IsSuccess.Should().BeTrue();
        Regex.IsMatch(result.Data!.TemporaryPassword, @"^\d{6}$").Should().BeTrue();
        saved!.Account!.PasswordHash.Should().NotBe(result.Data.TemporaryPassword);
        saved.Account.IsTemporaryPassword.Should().BeTrue();
        // Lưu nhân viên (lấy Id) → map mã chấm công → lưu AuditLog.
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync();
            _uow.SaveChangesAsync();
            _employees.MapAttendanceCodeAsync(1002, Arg.Any<int>(), 1, Arg.Any<CancellationToken>());
            _uow.SaveChangesAsync();
        });
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert && !e.NewValues!.ToString()!.Contains("123456")));
    }

    // ---- Quản đốc ----
    [Fact]
    public async Task ManagerGet_AccountantEmployee_ReturnsNotFound()
    {
        _user.Role.Returns(Roles.Manager);
        _employees.GetDetailAsync(5, false, default).ReturnsForAnyArgs(new EmployeeDetailDto { EmployeeId = 5, Role = Roles.Accountant });

        var result = await Employees().GetAsync(5, default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task ManagerList_HidesOnlyAdminAndAccountant_AndIncludesAccountInfo()
    {
        _user.Role.Returns(Roles.Manager);
        _employees.ListAsync(default!, default, default, default)
            .ReturnsForAnyArgs(new PagedResult<EmployeeListItemDto>(Array.Empty<EmployeeListItemDto>(), 1, 20, 0));

        await Employees().ListAsync(new EmployeeQuery(), default);

        await _employees.Received(1).ListAsync(Arg.Any<EmployeeQuery>(),
            Arg.Is<IReadOnlyCollection<string>?>(h => h != null && h.Contains("ADMIN") && h.Contains("ACCOUNTANT") && !h.Contains("MANAGER")),
            true, Arg.Any<CancellationToken>());
    }

    private static ManagerUpdateEmployeeDto ManagerEdit(string role, int? team, string name = "Tên mới") =>
        new(name, null, null, null, role, team, "PieceRate", new DateOnly(2026, 1, 1), null, RowVer);

    [Fact]
    public async Task ManagerUpdate_ChangesTeamAndRole_RotatesStampAndLogsTeamChange()
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, RoleCodes.Worker, team: 4);
        var oldStamp = person.Account!.SecurityStamp;
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        _teams.GetByIdAsync(5).Returns(TeamOf(id: 5));

        var result = await Employees().ManagerUpdateAsync(7, ManagerEdit("Worker", 5), default);

        result.IsSuccess.Should().BeTrue();
        person.TeamId.Should().Be(5);
        person.FullName.Should().Be("Tên mới");
        person.Account.SecurityStamp.Should().NotBe(oldStamp);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason != null && e.Reason.StartsWith("Chuyển tổ")));
    }

    [Fact]
    public async Task ManagerUpdate_CanChangePayType()
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, RoleCodes.Worker, team: 4);
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        _teams.GetByIdAsync(4).Returns(TeamOf());

        var result = await Employees().ManagerUpdateAsync(7, ManagerEdit("Worker", 4) with { PayType = "Daily" }, default);

        result.IsSuccess.Should().BeTrue();
        person.PayType.Should().Be(PayTypes.Daily);
    }

    [Fact]
    public async Task ManagerUpdate_PromotesWorkerToTeamLeader_WhenTeamAllows()
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, RoleCodes.Worker, team: 4);
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        _teams.GetByIdAsync(4).Returns(TeamOf());

        var result = await Employees().ManagerUpdateAsync(7, ManagerEdit("TeamLeader", 4), default);

        result.IsSuccess.Should().BeTrue();
        await _employees.Received(1).GetRoleIdAsync(RoleCodes.TeamLeader, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Accountant")]
    public async Task ManagerUpdate_CannotAssignAdminOrAccountantRole(string role)
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, RoleCodes.Worker, team: 4);
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);

        var result = await Employees().ManagerUpdateAsync(7, ManagerEdit(role, null), default);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Forbidden);
        person.Role.RoleCode.Should().Be(RoleCodes.Worker);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ManagerUpdate_StillCannotTouchAnAccountantEmployee()
    {
        _user.Role.Returns(Roles.Manager);
        _employees.GetFullAsync(5, default).ReturnsForAnyArgs(Person(5, RoleCodes.Accountant, team: null));

        var result = await Employees().ManagerUpdateAsync(5, ManagerEdit("Worker", 4), default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    // ---- Cập nhật / nghỉ việc ----
    [Fact]
    public async Task AdminUpdate_ChangeTeam_RotatesStampAndLogsReason()
    {
        var person = Person(7, RoleCodes.Worker, team: 4);
        var oldStamp = person.Account!.SecurityStamp;
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        _teams.GetByIdAsync(5).Returns(TeamOf(id: 5));
        var dto = new AdminUpdateEmployeeDto("Người 7", null, null, null, "Worker", 5, "PieceRate", person.StartDate, null, RowVer);

        var result = await Employees().AdminUpdateAsync(7, dto, default);

        result.IsSuccess.Should().BeTrue();
        person.TeamId.Should().Be(5);
        person.Account.SecurityStamp.Should().NotBe(oldStamp);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.Reason != null && e.Reason.StartsWith("Chuyển tổ")));
    }

    [Fact]
    public async Task Resign_Self_Fails_AndLeader_ReturnsWarning()
    {
        (await Employees().ResignAsync(1, new ResignEmployeeDto(new DateOnly(2026, 10, 24), "x", RowVer), default)).ErrorCode
            .Should().Be(ErrorCodes.AccountCannotLockSelf);

        var leader = Person(7, RoleCodes.TeamLeader);
        leader.Team = TeamOf();
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(leader);
        var result = await Employees().ResignAsync(7, new ResignEmployeeDto(new DateOnly(2026, 10, 24), "nghỉ việc", RowVer), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("tổ trưởng");
        leader.Status.Should().Be(RecordStatus.Resigned);
    }

    // ---- Tài khoản ----
    [Fact]
    public async Task Account_LockSelf_Fails_LockRotatesStamp_ResetSetsTemp()
    {
        var service = new AccountAdminService(_employees, _hasher, _passwords, _audit, _uow, _user, _clock, Scope());
        (await service.LockAsync(1, new LockAccountDto("x"), default)).ErrorCode.Should().Be(ErrorCodes.AccountCannotLockSelf);

        var person = Person(7, RoleCodes.Worker);
        var stamp = person.Account!.SecurityStamp;
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        (await service.LockAsync(7, new LockAccountDto("vi phạm"), default)).IsSuccess.Should().BeTrue();
        person.Account.Status.Should().Be(RecordStatus.Locked);
        person.Account.SecurityStamp.Should().NotBe(stamp);
        (await service.LockAsync(7, new LockAccountDto("lần 2"), default)).ErrorCode.Should().Be(ErrorCodes.AccountInvalidStatus);

        var reset = await service.ResetPasswordAsync(7, default);
        reset.Data!.TemporaryPassword.Should().Be("123456");
        person.Account.IsTemporaryPassword.Should().BeTrue();
    }

    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Accountant)]
    public async Task ManagerAccountActions_OnHiddenRoles_ReturnNotFound(string targetRole)
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, targetRole);
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        var service = new AccountAdminService(_employees, _hasher, _passwords, _audit, _uow, _user, _clock, Scope());

        (await service.LockAsync(7, new LockAccountDto("x"), default)).Kind.Should().Be(ResultKind.NotFound);
        (await service.UnlockAsync(7, default)).Kind.Should().Be(ResultKind.NotFound);
        (await service.ResetPasswordAsync(7, default)).Kind.Should().Be(ResultKind.NotFound);
        (await Employees().ResignAsync(7, new ResignEmployeeDto(new DateOnly(2026, 10, 24), "x", RowVer), default)).Kind
            .Should().Be(ResultKind.NotFound);
        person.Account!.Status.Should().Be(RecordStatus.Active);
    }

    [Fact]
    public async Task ManagerAccountActions_OnAnotherManager_AreAllowed()
    {
        _user.Role.Returns(Roles.Manager);
        var person = Person(7, RoleCodes.Manager, team: null);
        _employees.GetFullAsync(7, default).ReturnsForAnyArgs(person);
        var service = new AccountAdminService(_employees, _hasher, _passwords, _audit, _uow, _user, _clock, Scope());

        (await service.LockAsync(7, new LockAccountDto("nghỉ phép dài"), default)).IsSuccess.Should().BeTrue();
        (await service.ResetPasswordAsync(7, default)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Accountant")]
    public async Task ManagerCreate_AdminOrAccountant_IsForbidden(string role)
    {
        _user.Role.Returns(Roles.Manager);

        var result = await Employees().CreateAsync(NewEmployee(role), default);

        result.Kind.Should().Be(ResultKind.Forbidden);
        result.Message.Should().Be(Messages.ManagerCannotAssignRole);
    }

    // ---- Tổ ----
    [Fact]
    public async Task Transfer_FutureDate_Fails_Leader_Fails_Ok_LogsPerEmployee()
    {
        _teams.GetByIdAsync(5).Returns(TeamOf(id: 5));
        (await Teams().TransferInAsync(5, new TransferMembersDto(new[] { 7 }, new DateOnly(2026, 10, 25), null), default)).ErrorCode
            .Should().Be(ErrorCodes.TeamTransferFutureDate);

        var leader = Person(7, RoleCodes.TeamLeader);
        _employees.GetActiveByIdsAsync(default!, default).ReturnsForAnyArgs(new List<Employee> { leader });
        (await Teams().TransferInAsync(5, new TransferMembersDto(new[] { 7 }, new DateOnly(2026, 10, 24), null), default)).ErrorCode
            .Should().Be(ErrorCodes.TeamTransferLeader);

        var a = Person(8, RoleCodes.Worker);
        var b = Person(9, RoleCodes.Worker);
        _employees.GetActiveByIdsAsync(default!, default).ReturnsForAnyArgs(new List<Employee> { a, b });
        var ok = await Teams().TransferInAsync(5, new TransferMembersDto(new[] { 8, 9 }, new DateOnly(2026, 10, 24), "cần người"), default);
        ok.IsSuccess.Should().BeTrue();
        a.TeamId.Should().Be(5);
        _audit.Received(2).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Employee) && e.Reason!.Contains("hiệu lực 24/10/2026")));
    }

    [Fact]
    public async Task Appoint_DemotesHolderBeforePromoting_KeepsPayType()
    {
        var holder = Person(7, RoleCodes.TeamLeader, pay: PayTypes.PieceRate);
        var person = Person(8, RoleCodes.Worker);
        _teams.GetByIdAsync(4).Returns(TeamOf());
        _employees.GetFullAsync(8, default).ReturnsForAnyArgs(person);
        _employees.FindTeamRoleHolderAsync(4, RoleCodes.TeamLeader, 8, default).ReturnsForAnyArgs(holder);
        _employees.GetRoleIdAsync(RoleCodes.Worker, default).ReturnsForAnyArgs(9);

        var result = await Teams().AppointAsync(4, new AppointDto(8, TeamPositions.Leader), default);

        result.IsSuccess.Should().BeTrue();
        holder.RoleId.Should().Be(9);
        holder.PayType.Should().Be(PayTypes.PieceRate);
        await _uow.Received(2).SaveChangesAsync();
    }

    [Fact]
    public async Task Appoint_NotInTeam_OrOfficeTeam_Fails()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf());
        _employees.GetFullAsync(8, default).ReturnsForAnyArgs(Person(8, RoleCodes.Worker, team: 99));
        (await Teams().AppointAsync(4, new AppointDto(8, TeamPositions.Deputy), default)).ErrorCode
            .Should().Be(ErrorCodes.TeamMemberNotInTeam);

        _teams.GetByIdAsync(4).Returns(TeamOf(TeamTypes.Office));
        (await Teams().AppointAsync(4, new AppointDto(8, TeamPositions.Deputy), default)).ErrorCode
            .Should().Be(ErrorCodes.TeamLeaderInvalidTeam);
    }

    [Fact]
    public async Task Discontinue_WithMembers_Fails()
    {
        _teams.GetByIdAsync(4).Returns(TeamOf());
        _teams.CountActiveMembersAsync(4, default).ReturnsForAnyArgs(3);

        (await Teams().DiscontinueAsync(4, default)).ErrorCode.Should().Be(ErrorCodes.TeamHasActiveMembers);
    }

    [Fact]
    public void TempPasswordGenerator_AlwaysSixDigits()
    {
        var generator = new TemporaryPasswordGenerator();
        for (var i = 0; i < 1000; i++) Regex.IsMatch(generator.Generate(), @"^\d{6}$").Should().BeTrue();
    }
}
