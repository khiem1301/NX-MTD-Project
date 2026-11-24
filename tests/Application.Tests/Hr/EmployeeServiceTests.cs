using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Security;

namespace Application.Tests;

/// <summary>
/// UTCID820–UTCID859: EmployeeService (List / Get / Create / ImportFromRj / AdminUpdate / ManagerUpdate / Resign).
/// Assert khóa theo sheet EmployeeService trong UNIT_TEST_CASES_REBASELINE_v3.1 — không nới Expected cho Actual lệch spec.
/// </summary>
public class EmployeeServiceTests
{
    private const int Office1 = 21;
    private const int Office2 = 22;
    private const int S3 = 30;
    private const int TeamA = 10;
    private const int TeamB = 11;
    private const int OtherWorkshop = 90;
    private const int E1 = 1;
    private const int E2 = 2;
    private const int E3 = 3;
    private const int E4 = 4;
    private const int E5 = 5;
    private const int E6 = 6;
    private const int E8 = 8;
    private const int E9 = 9;
    private const string TempPassword = "123456";
    private const string ExistingEmail = "a@x.vn";

    private static readonly byte[] RowVer = [1];
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITemporaryPasswordGenerator _passwords = Substitute.For<ITemporaryPasswordGenerator>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDailyWorkAllocationService _daily = Substitute.For<IDailyWorkAllocationService>();

    private readonly Dictionary<int, Team> _teamById = [];
    private readonly Dictionary<int, Employee> _tracked = [];
    private readonly Dictionary<int, EmployeeDetailDto> _details = [];
    private readonly Dictionary<int, List<AuditLogDto>> _histories = [];
    private readonly List<EmployeeListItemDto> _catalog = [];
    private readonly List<RjEmployeeSnapshot> _snaps = [];
    private readonly HashSet<string> _codes = new(StringComparer.Ordinal);
    private readonly HashSet<int> _takenAttendance = [];
    private readonly List<Employee> _added = [];
    private readonly List<Employee> _txAdded = [];
    private readonly List<AuditHit> _audits = [];
    private readonly List<SettleHit> _settles = [];

    private Result _settleResult = Result.Success();
    private bool _inTx;
    private bool _throwOnAccount;
    private int _nextId = 500;

    public EmployeeServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.Role.Returns(Roles.Admin);
        _user.EmployeeId.Returns(100);
        _user.AccountId.Returns(100);
        _hasher.Hash(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
        _passwords.Generate().Returns(TempPassword);

        AddTeam(Office1, "OFFICE1", TeamTypes.Office);
        AddTeam(Office2, "OFFICE2", TeamTypes.Office);
        AddTeam(S3, "S3", TeamTypes.Sewing);
        AddTeam(TeamA, "A", TeamTypes.Sewing);
        AddTeam(TeamB, "B", TeamTypes.Sewing);
        AddTeam(OtherWorkshop, "X9", TeamTypes.Sewing);

        _teams.GetByIdAsync(Arg.Any<object>()).Returns(ci =>
        {
            var id = Convert.ToInt32(ci.Arg<object>());
            return _teamById.TryGetValue(id, out var team) ? team : null;
        });
        _employees.GetFullAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => _tracked.TryGetValue(ci.Arg<int>(), out var employee) ? employee : null);
        _employees.GetDetailAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => _details.TryGetValue(ci.Arg<int>(), out var detail) ? detail : null);
        _employees.GetRoleIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => RoleIdOf(ci.Arg<string>()));
        _employees.CodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => _codes.Contains(ci.Arg<string>()));
        _employees.AttendanceCodeTakenAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ci => _takenAttendance.Contains(ci.Arg<int>()));
        _employees.FindTeamRoleHolderAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((Employee?)null);
        _employees.ListAsync(null!, null, false, CancellationToken.None)
            .ReturnsForAnyArgs(ci => Page((EmployeeQuery)ci[0], ci[1] as IReadOnlyCollection<string>));
        _accounts.UsernameExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _audit.GetHistoryAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var id = (int)ci.Arg<long>();
                IReadOnlyList<AuditLogDto> rows = _histories.TryGetValue(id, out var found) ? found : [];
                return rows;
            });
        _audit.When(a => a.Add(Arg.Any<AuditEntry>())).Do(ci => _audits.Add(new AuditHit(ci.Arg<AuditEntry>(), _inTx)));
        _daily.SettleOnTeamLeaveAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var teamId = ci.Arg<int>();
                var ids = ci.Arg<IReadOnlyList<int>>().ToArray();
                int? teamAtCall = ids.Length > 0 && _tracked.TryGetValue(ids[0], out var person) ? person.TeamId : null;
                _settles.Add(new SettleHit(teamId, ids, teamAtCall, _inTx));
                return _settleResult;
            });

        var rj = Substitute.For<IGenericRepository<RjEmployeeSnapshot>>();
        rj.Query().Returns(_ => _snaps.AsTestAsyncQueryable());
        _uow.Repository<RjEmployeeSnapshot>().Returns(rj);
        _uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        _uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        _uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        _uow.SaveChangesAsync().Returns(1);
        _uow.When(u => u.BeginTransactionAsync()).Do(_ =>
        {
            _inTx = true;
            _txAdded.Clear();
        });
        _uow.When(u => u.CommitTransactionAsync()).Do(_ =>
        {
            _txAdded.Clear();
            _inTx = false;
        });
        _uow.When(u => u.RollbackTransactionAsync()).Do(_ =>
        {
            foreach (var row in _txAdded) _added.Remove(row);
            _txAdded.Clear();
            _inTx = false;
        });
        _employees.When(e => e.AddAsync(Arg.Any<Employee>())).Do(ci =>
        {
            var employee = ci.Arg<Employee>();
            if (_throwOnAccount && employee.Account is not null)
                throw new InvalidOperationException("account create failed");
            if (employee.EmployeeId == 0) employee.EmployeeId = ++_nextId;
            _added.Add(employee);
            if (_inTx) _txAdded.Add(employee);
            _tracked[employee.EmployeeId] = employee;
        });
    }

    private EmployeeService Sut() => new(
        _employees, _accounts, _teams, _hasher, _passwords, new UsernameGenerator(),
        _audit, _uow, _user, _clock, new EmployeeScope(_user), _daily);

    [Fact]
    public async Task UTCID820_ListAsync_WorkerActive_ReturnsFiveActiveWorkers()
    {
        // Arrange — 10 NV: 6 Worker (1 Resigned), 2 Accountant, 2 Manager
        SeedTen();

        // Act
        var result = await Sut().ListAsync(new EmployeeQuery { Role = Roles.Worker, Status = "Active" }, CancellationToken.None);

        // Assert — Success; 5 Worker Active
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Items.Should().HaveCount(5);
        result.Data.TotalItems.Should().Be(5);
        result.Data.Items.Should().OnlyContain(e => e.Role == Roles.Worker && e.Status == "Active");
    }

    [Fact]
    public async Task UTCID821_ListAsync_NullFilters_ReturnsEveryoneCallerCanSee()
    {
        // Arrange — cùng 10 NV; người gọi Quản đốc không thấy Admin / Kế toán
        SeedTen();
        _user.Role.Returns(Roles.Manager);

        // Act
        var result = await Sut().ListAsync(new EmployeeQuery { Role = null, Status = null }, CancellationToken.None);

        // Assert — toàn bộ NV mà người gọi CanSee (kể cả Worker đã nghỉ), không cắt Accountant
        result.IsSuccess.Should().BeTrue(result.Message);
        var scope = new EmployeeScope(_user);
        result.Data!.Items.Should().HaveCount(8);
        result.Data.TotalItems.Should().Be(8);
        result.Data.Items.Should().OnlyContain(e => scope.CanSee(Roles.ToRoleCode(e.Role)));
        result.Data.Items.Should().Contain(e => e.Role == Roles.Worker && e.Status == "Resigned");
        result.Data.Items.Should().NotContain(e => e.Role == Roles.Accountant);
    }

    [Fact]
    public async Task UTCID822_ListAsync_Electrician_ReturnsEmpty()
    {
        // Arrange — không có NV Electrician
        SeedTen();

        // Act
        var result = await Sut().ListAsync(new EmployeeQuery { Role = Roles.Electrician }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task UTCID823_ListAsync_UnknownRole_ValidationOrEmptyWithoutException()
    {
        // Arrange
        SeedTen();
        var sut = Sut();

        // Act
        var (thrown, result) = await Capture(() => sut.ListAsync(new EmployeeQuery { Role = "Foo" }, CancellationToken.None));

        // Assert — Failure (Validation) hoặc list rỗng; không exception
        thrown.Should().BeNull();
        result.Should().NotBeNull();
        if (result!.IsSuccess)
            result.Data!.Items.Should().BeEmpty();
        else
            result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID824_GetAsync_WithAudit_ReturnsDetailAndThreeHistory()
    {
        // Arrange — E1 có 3 audit; người gọi Admin
        See(E1, Roles.Worker, 3);

        // Act
        var result = await Sut().GetAsync(E1, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.EmployeeId.Should().Be(E1);
        result.Data.History.Should().HaveCount(3);
    }

    [Fact]
    public async Task UTCID825_GetAsync_NoAudit_ReturnsEmptyHistory()
    {
        // Arrange — E2 mới tạo, chưa có audit
        See(E2, Roles.Worker, 0);

        // Act
        var result = await Sut().GetAsync(E2, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.History.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID826_GetAsync_UnknownId_NotFound()
    {
        // Act
        var result = await Sut().GetAsync(999, CancellationToken.None);

        // Assert
        ShouldBeNotFound(result);
    }

    [Fact]
    public async Task UTCID827_GetAsync_ManagerCannotSeeAdmin_NotFound()
    {
        // Arrange — Quản đốc; E9 là Admin (ngoài phạm vi xem)
        _user.Role.Returns(Roles.Manager);
        See(E9, Roles.Admin, 1);

        // Act
        var result = await Sut().GetAsync(E9, CancellationToken.None);

        // Assert — NotFound, không lộ sự tồn tại
        ShouldBeNotFound(result);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task UTCID828_CreateAsync_Accountant_CreatesEmployeeAndAccountInOneTransaction()
    {
        // Arrange — Accountant là office creatable, gắn tổ văn phòng
        var dto = OfficeDto();

        // Act
        var result = await Sut().CreateAsync(dto, CancellationToken.None);

        // Assert — Employee + Account trong 1 TX; mật khẩu tạm trả 1 lần; không settle
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.TemporaryPassword.Should().Be(TempPassword);
        result.Data.Username.Should().NotBeNullOrWhiteSpace();
        var saved = _added.Should().ContainSingle().Subject;
        saved.Account.Should().NotBeNull();
        saved.Account!.IsTemporaryPassword.Should().BeTrue();
        saved.Account.PasswordHash.Should().NotBe(TempPassword);
        saved.TeamId.Should().Be(Office1);
        saved.RoleId.Should().Be(RoleIdOf(RoleCodes.Accountant));
        _passwords.Received(1).Generate();
        _audits.Should().ContainSingle();
        _audits[0].Entry.NewValues!.ToString().Should().NotContain(TempPassword);
        _settles.Should().BeEmpty();
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
    }

    [Fact]
    public async Task UTCID829_CreateAsync_FullName100_Succeeds()
    {
        // Arrange — MaxLength FullName = 100
        var name = new string('A', 100);

        // Act
        var result = await Sut().CreateAsync(OfficeDto(name, code: "KT100"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        _added.Should().ContainSingle().Which.FullName.Should().HaveLength(100);
    }

    [Fact]
    public async Task UTCID830_CreateAsync_FullName101_ValidationFailure()
    {
        // Arrange — FullName = 101 ký tự
        var name = new string('A', 101);

        // Act
        var result = await Sut().CreateAsync(OfficeDto(name, code: "KT101"), CancellationToken.None);

        // Assert — Failure (Validation)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            _added.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID831_CreateAsync_EmptyFullName_ValidationFailure()
    {
        // Act
        var result = await Sut().CreateAsync(OfficeDto(""), CancellationToken.None);

        // Assert — Failure (Validation)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            _added.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID832_CreateAsync_Worker_ValidationFailure()
    {
        // Act — Worker không tạo được trên web
        var result = await Sut().CreateAsync(OfficeDto("Nguyễn Văn A", Roles.Worker, S3, "CN001"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.EmployeeFloorRoleWebCreateForbidden);
        _added.Should().BeEmpty();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID833_CreateAsync_AccountantOnSewingTeam_CheckPlacementFailure()
    {
        // Arrange — S3 là tổ may
        var dto = OfficeDto("Nguyễn Văn A", Roles.Accountant, S3, "KT833");

        // Act
        var result = await Sut().CreateAsync(dto, CancellationToken.None);

        // Assert — Failure (CheckPlacement)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            _added.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID834_CreateAsync_DuplicateEmail_Failure()
    {
        // Arrange — email a@x.vn đã tồn tại
        _accounts.EmailExistsAsync(ExistingEmail, Arg.Any<CancellationToken>()).Returns(true);
        var dto = OfficeDto(code: "KT834") with { Email = ExistingEmail };

        // Act
        var result = await Sut().CreateAsync(dto, CancellationToken.None);

        // Assert — Failure (Duplicate)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse("email {0} đã tồn tại", ExistingEmail);
            result.ErrorCode.Should().BeOneOf(
                ErrorCodes.PartnerEmailDuplicate, ErrorCodes.CommonDuplicate, ErrorCodes.EmployeeCodeDuplicate);
            _added.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID835_CreateAsync_AccountCreateThrows_RollsBackWithoutOrphan()
    {
        // Arrange — bước tạo Account ném exception
        _throwOnAccount = true;

        // Act
        var act = async () => await Sut().CreateAsync(OfficeDto(code: "KT835"), CancellationToken.None);

        // Assert — Rollback; không có Employee mồ côi
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID836_CreateAsync_NullDto_ArgumentNullOrFailure()
    {
        // Arrange
        var sut = Sut();

        // Act
        var (thrown, result) = await Capture(() => sut.CreateAsync(null!, CancellationToken.None));

        // Assert — ArgumentNullException hoặc Failure. ⚠ Dereference → NullReferenceException.
        AssertNullArgOrFailure(thrown, result);
    }

    [Fact]
    public async Task UTCID837_ImportFromRjAsync_TwoWorkers_CreatesOnSewingTeamWithoutSettle()
    {
        // Arrange — 2 Worker RJ hợp lệ, tổ may S3. API nhận từng DTO.
        Snap(201, "RJ201", "Lê Văn Một");
        Snap(202, "RJ202", "Lê Văn Hai");
        var batch = new[]
        {
            new ImportEmployeeFromRjDto(201, S3, "PieceRate", Today),
            new ImportEmployeeFromRjDto(202, S3, "PieceRate", Today),
        };

        // Act
        var results = new List<Result<CreatedEmployeeDto>>();
        foreach (var dto in batch)
            results.Add(await Sut().ImportFromRjAsync(dto, CancellationToken.None));

        // Assert — tạo 2 Worker gắn S3 kèm Account; không settle
        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Data!.TemporaryPassword).Should().OnlyContain(p => p == TempPassword);
        _added.Should().HaveCount(2);
        _added.Should().OnlyContain(e =>
            e.TeamId == S3 && e.RoleId == RoleIdOf(RoleCodes.Worker) && e.Account != null && e.Status == RecordStatus.Active);
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID838_ImportFromRjAsync_EmptyList_ZeroRecordsAndNoTransaction()
    {
        // Arrange — danh sách rỗng. API nhận từng DTO nên lô rỗng không gọi import.
        var batch = Array.Empty<ImportEmployeeFromRjDto>();

        // Act
        var results = new List<Result<CreatedEmployeeDto>>();
        foreach (var dto in batch)
            results.Add(await Sut().ImportFromRjAsync(dto, CancellationToken.None));

        // Assert — Success 0 bản ghi (hoặc Validation); không mở TX
        results.Should().BeEmpty();
        _added.Should().BeEmpty();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID839_ImportFromRjAsync_AlreadyImported_Duplicate()
    {
        // Arrange — mã RJ "RJ001" đã import
        Snap(101, "RJ001", "Nguyễn Văn B");
        _takenAttendance.Add(101);

        // Act
        var result = await Sut().ImportFromRjAsync(new ImportEmployeeFromRjDto(101, S3, "PieceRate", Today), CancellationToken.None);

        // Assert — Failure (Duplicate) cho dòng đó
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.EmployeeRjAlreadyImported);
        _added.Should().BeEmpty();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID840_ImportFromRjAsync_WorkerOnOfficeTeam_CheckPlacementFailure()
    {
        // Arrange — Worker vào tổ văn phòng Office1
        Snap(301, "RJ301", "Trần Văn E");

        // Act
        var result = await Sut().ImportFromRjAsync(new ImportEmployeeFromRjDto(301, Office1, "PieceRate", Today), CancellationToken.None);

        // Assert — Failure (CheckPlacement)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            _added.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID841_AdminUpdateAsync_ChangeTeam_SettlesOldTeamBeforeAssign()
    {
        // Arrange — Worker E1 Active ở tổ may A, chuyển sang B
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().AdminUpdateAsync(E1, AdminDto(person, TeamB), CancellationToken.None);

        // Assert — Settle(A,[E1]) 1 lần trước khi gán TeamId; cùng TX; TeamId=B; Audit; Commit
        result.IsSuccess.Should().BeTrue(result.Message);
        _settles.Should().ContainSingle();
        _settles[0].OldTeamId.Should().Be(TeamA);
        _settles[0].WorkerIds.Should().Equal(E1);
        _settles[0].TeamIdAtCall.Should().Be(TeamA);
        _settles[0].InTx.Should().BeTrue();
        person.TeamId.Should().Be(TeamB);
        _audits.Should().ContainSingle(a => a.InTx && a.Entry.Action == AuditActions.Update);
        await _uow.Received(1).CommitTransactionAsync();
        await _uow.DidNotReceive().RollbackTransactionAsync();
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync();
            _daily.SettleOnTeamLeaveAsync(TeamA, Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>());
            _audit.Add(Arg.Any<AuditEntry>());
            _uow.SaveChangesAsync();
            _uow.CommitTransactionAsync();
        });
    }

    [Fact]
    public async Task UTCID842_AdminUpdateAsync_SameTeam_DoesNotSettle()
    {
        // Arrange — E1 ở tổ A, chỉ đổi điện thoại
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().AdminUpdateAsync(E1, AdminDto(person, TeamA, "0912000111"), CancellationToken.None);

        // Assert — Success; Settle không gọi
        result.IsSuccess.Should().BeTrue(result.Message);
        person.TeamId.Should().Be(TeamA);
        person.Phone.Should().Be("0912000111");
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID843_AdminUpdateAsync_FromNoTeam_DoesNotSettle()
    {
        // Arrange — E2 chưa có tổ
        var person = Track(E2, RoleCodes.Worker, null);

        // Act
        var result = await Sut().AdminUpdateAsync(E2, AdminDto(person, TeamB), CancellationToken.None);

        // Assert — Success; Settle không gọi vì oldTeamId null
        result.IsSuccess.Should().BeTrue(result.Message);
        person.TeamId.Should().Be(TeamB);
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID844_AdminUpdateAsync_ClearTeam_SettlesThenClears()
    {
        // Arrange — E1 ở tổ A, gỡ tổ
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().AdminUpdateAsync(E1, AdminDto(person, null), CancellationToken.None);

        // Assert — Settle(A,[E1]); TeamId=null
        result.IsSuccess.Should().BeTrue(result.Message);
        _settles.Should().ContainSingle();
        _settles[0].OldTeamId.Should().Be(TeamA);
        _settles[0].WorkerIds.Should().Equal(E1);
        person.TeamId.Should().BeNull();
    }

    [Fact]
    public async Task UTCID845_AdminUpdateAsync_OfficeToOffice_StillSettles()
    {
        // Arrange — E3 Accountant Office1 → Office2
        var person = Track(E3, RoleCodes.Accountant, Office1);

        // Act
        var result = await Sut().AdminUpdateAsync(E3, AdminDto(person, Office2), CancellationToken.None);

        // Assert — Settle(Office1,[E3]) vẫn được gọi; Success
        result.IsSuccess.Should().BeTrue(result.Message);
        _settles.Should().ContainSingle();
        _settles[0].OldTeamId.Should().Be(Office1);
        _settles[0].WorkerIds.Should().Equal(E3);
        person.TeamId.Should().Be(Office2);
    }

    [Fact]
    public async Task UTCID846_AdminUpdateAsync_SettleTeamOrderBusy_RollsBack()
    {
        // Arrange — E1 ở tổ A; Settle trả TeamOrderBusy
        var person = Track(E1, RoleCodes.Worker, TeamA);
        _settleResult = Result.Failure(ErrorCodes.TeamOrderBusy, Messages.OutputLockBusy);

        // Act
        var result = await Sut().AdminUpdateAsync(E1, AdminDto(person, TeamB), CancellationToken.None);

        // Assert — Failure TeamOrderBusy; Rollback; TeamId vẫn A; không Audit
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        person.TeamId.Should().Be(TeamA);
        _audits.Should().BeEmpty();
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID847_AdminUpdateAsync_Resigned_Fails()
    {
        // Arrange — E5 đã nghỉ
        var person = Track(E5, RoleCodes.Worker, TeamA, RecordStatus.Resigned);

        // Act
        var result = await Sut().AdminUpdateAsync(E5, AdminDto(person, TeamB), CancellationToken.None);

        // Assert — chỉ sửa NV Active
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.EmployeeNotActive);
        person.Status.Should().Be(RecordStatus.Resigned);
        person.TeamId.Should().Be(TeamA);
        _settles.Should().BeEmpty();
        await _uow.DidNotReceive().BeginTransactionAsync();
    }

    [Fact]
    public async Task UTCID848_AdminUpdateAsync_UnknownId_NotFound()
    {
        // Act
        var result = await Sut().AdminUpdateAsync(999, new AdminUpdateEmployeeDto(
            "Người 999", null, null, null, Roles.Worker, TeamB, "PieceRate", new DateOnly(2026, 1, 1), null, RowVer),
            CancellationToken.None);

        // Assert
        ShouldBeNotFound(result);
    }

    [Fact]
    public async Task UTCID849_AdminUpdateAsync_WorkerToOffice_CheckPlacementFailure()
    {
        // Arrange — Worker → Office1
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().AdminUpdateAsync(E1, AdminDto(person, Office1), CancellationToken.None);

        // Assert — Failure (CheckPlacement)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            person.TeamId.Should().Be(TeamA);
            _settles.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID850_AdminUpdateAsync_TeamLeader_CannotTransfer()
    {
        // Arrange — E4 là tổ trưởng tổ A. Không chuyển tổ trưởng (nhất quán TransferIn).
        var person = Track(E4, RoleCodes.TeamLeader, TeamA);

        // Act
        var result = await Sut().AdminUpdateAsync(E4, AdminDto(person, TeamB), CancellationToken.None);

        // Assert — Failure. ⚠ Hiện cho phép chuyển (rủi ro #4).
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be(ErrorCodes.TeamTransferLeader);
            person.TeamId.Should().Be(TeamA);
            _audits.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID851_ManagerUpdateAsync_ChangeTeam_SettlesBeforeAssign()
    {
        // Arrange — Quản đốc phụ trách xưởng có tổ A, B; E1 ở A
        _user.Role.Returns(Roles.Manager);
        _user.TeamId.Returns(TeamA);
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().ManagerUpdateAsync(E1, ManagerDto(person, TeamB), CancellationToken.None);

        // Assert — Settle(A,[E1]) trước gán TeamId; cùng TX
        result.IsSuccess.Should().BeTrue(result.Message);
        _settles.Should().ContainSingle();
        _settles[0].OldTeamId.Should().Be(TeamA);
        _settles[0].WorkerIds.Should().Equal(E1);
        _settles[0].TeamIdAtCall.Should().Be(TeamA);
        _settles[0].InTx.Should().BeTrue();
        person.TeamId.Should().Be(TeamB);
        await _uow.Received(1).BeginTransactionAsync();
        await _uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID852_ManagerUpdateAsync_SameTeam_DoesNotSettle()
    {
        // Arrange
        _user.Role.Returns(Roles.Manager);
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().ManagerUpdateAsync(E1, ManagerDto(person, TeamA, "0912000222"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Message);
        person.TeamId.Should().Be(TeamA);
        person.Phone.Should().Be("0912000222");
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID853_ManagerUpdateAsync_OutsideWorkshop_ForbiddenOrNotFound()
    {
        // Arrange — E8 thuộc xưởng ngoài phạm vi Quản đốc
        _user.Role.Returns(Roles.Manager);
        _user.TeamId.Returns(TeamA);
        var person = Track(E8, RoleCodes.Worker, OtherWorkshop);

        // Act
        var result = await Sut().ManagerUpdateAsync(E8, ManagerDto(person, TeamB), CancellationToken.None);

        // Assert — Failure (scope: Forbidden/NotFound)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().BeOneOf(ResultKind.Forbidden, ResultKind.NotFound);
            person.TeamId.Should().Be(OtherWorkshop);
        }
    }

    [Fact]
    public async Task UTCID854_ManagerUpdateAsync_SettleTeamOrderBusy_RollsBack()
    {
        // Arrange
        _user.Role.Returns(Roles.Manager);
        var person = Track(E1, RoleCodes.Worker, TeamA);
        _settleResult = Result.Failure(ErrorCodes.TeamOrderBusy, Messages.OutputLockBusy);

        // Act
        var result = await Sut().ManagerUpdateAsync(E1, ManagerDto(person, TeamB), CancellationToken.None);

        // Assert — Failure TeamOrderBusy; Rollback; TeamId không đổi
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.TeamOrderBusy);
        person.TeamId.Should().Be(TeamA);
        await _uow.Received(1).RollbackTransactionAsync();
        await _uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task UTCID855_ManagerUpdateAsync_Resigned_Fails()
    {
        // Arrange
        _user.Role.Returns(Roles.Manager);
        var person = Track(E5, RoleCodes.Worker, TeamA, RecordStatus.Resigned);

        // Act
        var result = await Sut().ManagerUpdateAsync(E5, ManagerDto(person, TeamB), CancellationToken.None);

        // Assert — chỉ sửa NV Active
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.EmployeeNotActive);
        person.TeamId.Should().Be(TeamA);
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID856_ResignAsync_Accountant_ResignedAndRotatesStamp()
    {
        // Arrange — E6 Accountant Active, không có tổ
        var person = Track(E6, RoleCodes.Accountant, null);
        var stamp = person.Account!.SecurityStamp;

        // Act
        var result = await Sut().ResignAsync(E6, ResignDto(), CancellationToken.None);

        // Assert — Status=Resigned; RotateStamp
        result.IsSuccess.Should().BeTrue(result.Message);
        person.Status.Should().Be(RecordStatus.Resigned);
        person.ResignDate.Should().Be(Today);
        person.Account.SecurityStamp.Should().NotBe(stamp);
    }

    [Fact]
    public async Task UTCID857_ResignAsync_WorkerOnSewingTeam_SettlesAndClearsTeam()
    {
        // Arrange — E1 Worker Active ở tổ may A, còn handover PENDING
        var person = Track(E1, RoleCodes.Worker, TeamA);

        // Act
        var result = await Sut().ResignAsync(E1, ResignDto(), CancellationToken.None);

        // Assert — Settle(A,[E1]) và TeamId=null. ⚠ Hiện không settle, vẫn giữ TeamId (rủi ro #3).
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            _settles.Should().ContainSingle();
            if (_settles.Count == 1)
            {
                _settles[0].OldTeamId.Should().Be(TeamA);
                _settles[0].WorkerIds.Should().Equal(E1);
            }
            person.TeamId.Should().BeNull();
        }
    }

    [Fact]
    public async Task UTCID858_ResignAsync_AlreadyResigned_InvalidStatus()
    {
        // Arrange — E5 đã Resigned
        var person = Track(E5, RoleCodes.Worker, TeamA, RecordStatus.Resigned);

        // Act
        var result = await Sut().ResignAsync(E5, ResignDto(), CancellationToken.None);

        // Assert — Failure InvalidStatus (NV không còn Active)
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.EmployeeNotActive);
        person.Status.Should().Be(RecordStatus.Resigned);
        _settles.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID859_ResignAsync_UnknownId_NotFound()
    {
        // Act
        var result = await Sut().ResignAsync(999, ResignDto(), CancellationToken.None);

        // Assert
        ShouldBeNotFound(result);
    }

    private void SeedTen()
    {
        var id = 1;
        for (var i = 0; i < 5; i++) _catalog.Add(Item(id++, Roles.Worker, "Active"));
        _catalog.Add(Item(id++, Roles.Worker, "Resigned"));
        _catalog.Add(Item(id++, Roles.Accountant, "Active"));
        _catalog.Add(Item(id++, Roles.Accountant, "Active"));
        _catalog.Add(Item(id++, Roles.Manager, "Active"));
        _catalog.Add(Item(id++, Roles.Manager, "Active"));
    }

    private static EmployeeListItemDto Item(int id, string role, string status) =>
        new(id, $"E{id}", $"Người {id}", role, role, null, null, "PieceRate", null, null, status, "Active", new DateOnly(2026, 1, 1));

    private PagedResult<EmployeeListItemDto> Page(EmployeeQuery query, IReadOnlyCollection<string>? hidden)
    {
        IEnumerable<EmployeeListItemDto> rows = _catalog;
        if (hidden is not null)
            rows = rows.Where(e => !hidden.Contains(Roles.ToRoleCode(e.Role)));
        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
            rows = rows.Where(e => e.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Role))
            rows = rows.Where(e => e.Role == query.Role);
        var items = rows.ToList();
        return new PagedResult<EmployeeListItemDto>(items, query.SafePage, query.SafePageSize, items.Count);
    }

    private void See(int id, string role, int historyCount)
    {
        _details[id] = new EmployeeDetailDto
        {
            EmployeeId = id,
            EmployeeCode = $"E{id}",
            FullName = $"Người {id}",
            Role = role,
            RoleName = role,
            PayType = "PieceRate",
            StartDate = new DateOnly(2026, 1, 1),
            Status = "Active",
            RowVer = RowVer,
        };
        _histories[id] = Enumerable.Range(1, historyCount)
            .Select(n => new AuditLogDto(n, AuditActions.Update, null, null, null, 100, "Admin", Now))
            .ToList();
    }

    private Employee Track(int id, string roleCode, int? teamId, string status = RecordStatus.Active)
    {
        var employee = new Employee
        {
            EmployeeId = id,
            EmployeeCode = $"E{id}",
            FullName = $"Người {id}",
            Gender = "M",
            Phone = "0900000000",
            RoleId = RoleIdOf(roleCode),
            Role = new Role { RoleId = RoleIdOf(roleCode), RoleCode = roleCode, RoleName = roleCode },
            TeamId = teamId,
            Team = teamId is int tid && _teamById.TryGetValue(tid, out var team) ? team : null,
            PayType = PayTypes.PieceRate,
            StartDate = new DateOnly(2026, 1, 1),
            Status = status,
            RowVer = RowVer,
            CreatedAt = Now,
            Account = new Account
            {
                AccountId = id,
                EmployeeId = id,
                Username = $"user{id}",
                PasswordHash = "hash",
                SecurityStamp = Guid.NewGuid(),
                Status = RecordStatus.Active,
                CreatedAt = Now,
            },
        };
        _tracked[id] = employee;
        return employee;
    }

    private void AddTeam(int id, string code, string type) =>
        _teamById[id] = new Team
        {
            TeamId = id, TeamCode = code, TeamType = type, Status = RecordStatus.Active, CreatedAt = Now,
        };

    private void Snap(int attendanceCode, string rjCode, string fullName) =>
        _snaps.Add(new RjEmployeeSnapshot
        {
            AttendanceCode = attendanceCode, RjEmployeeCode = rjCode, FullName = fullName, SyncedAt = Now,
        });

    private static int RoleIdOf(string roleCode) => roleCode switch
    {
        RoleCodes.Worker => 1,
        RoleCodes.Accountant => 2,
        RoleCodes.TeamLeader => 3,
        RoleCodes.Manager => 4,
        RoleCodes.Admin => 5,
        _ => 9,
    };

    private static CreateEmployeeDto OfficeDto(string fullName = "Nguyễn Văn A", string role = Roles.Accountant, int? teamId = Office1, string code = "KT001") =>
        new(code, fullName, "M", new DateOnly(1990, 5, 1), "0901000000", role, teamId, "PieceRate", new DateOnly(2026, 1, 1), null);

    private static AdminUpdateEmployeeDto AdminDto(Employee employee, int? teamId, string? phone = null) =>
        new(employee.FullName, employee.Gender, employee.DateOfBirth, phone ?? employee.Phone,
            Roles.FromRoleCode(employee.Role.RoleCode), teamId, "PieceRate", employee.StartDate, employee.AttendanceCode, RowVer);

    private static ManagerUpdateEmployeeDto ManagerDto(Employee employee, int? teamId, string? phone = null) =>
        new(employee.FullName, employee.Gender, employee.DateOfBirth, phone ?? employee.Phone,
            Roles.FromRoleCode(employee.Role.RoleCode), teamId, "PieceRate", employee.StartDate, employee.AttendanceCode, RowVer);

    private static ResignEmployeeDto ResignDto() => new(Today, "Nghỉ việc", RowVer);

    private static void ShouldBeNotFound(Result result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    private static void ShouldBeNotFound<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    private static void AssertNullArgOrFailure<T>(Exception? thrown, Result<T>? result)
    {
        if (thrown is not null)
            thrown.Should().BeOfType<ArgumentNullException>();
        else
            result!.IsSuccess.Should().BeFalse();
    }

    private static async Task<(Exception? Thrown, Result<T>? Result)> Capture<T>(Func<Task<Result<T>>> act)
    {
        try
        {
            return (null, await act());
        }
        catch (Exception ex)
        {
            return (ex, null);
        }
    }

    private sealed record AuditHit(AuditEntry Entry, bool InTx);

    private sealed record SettleHit(int OldTeamId, int[] WorkerIds, int? TeamIdAtCall, bool InTx);
}
