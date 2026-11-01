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

/// <summary>KH-14 / UC15, UC16, UC50: bậc thợ — mức bậc, đề xuất của tổ trưởng, quyết định của Admin / Quản đốc.</summary>
public class GradeServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 5);
    private const int Leader = 20;
    private const int TeamId = 5;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<GradeLevelCode> _codes = new()
    {
        new() { GradeCode = "A", GradeName = "Bậc A", Status = GradeCodes.Active },
        new() { GradeCode = "B", GradeName = "Bậc B", Status = GradeCodes.Active },
        new() { GradeCode = "C", GradeName = "Bậc C", Status = GradeCodes.Active },
        new() { GradeCode = "D", GradeName = "Bậc D", Status = GradeCodes.Active },
        new() { GradeCode = "X", GradeName = "Bậc cũ", Status = GradeCodes.Inactive },
    };
    private readonly List<GradeLevel> _levels = new();
    private readonly List<GradeEvaluation> _evals = new();
    private readonly List<Employee> _employees = new();
    private readonly List<DefectRecord> _defects = new();
    private readonly Employee _worker;

    public GradeServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.EmployeeId.Returns(Leader);
        var workerRole = new Role { RoleCode = RoleCodes.Worker };
        _worker = new Employee { EmployeeId = 40, EmployeeCode = "CN1", FullName = "Công nhân 1", TeamId = TeamId, Status = RecordStatus.Active, Role = workerRole, CurrentGradeCode = "C" };
        _employees.Add(_worker);
        _employees.Add(new Employee { EmployeeId = 41, EmployeeCode = "CN2", FullName = "Công nhân 2", TeamId = TeamId, Status = RecordStatus.Active, Role = workerRole });
        _employees.Add(new Employee { EmployeeId = 42, EmployeeCode = "CN3", FullName = "Khác tổ", TeamId = 9, Status = RecordStatus.Active, Role = workerRole });
        _employees.Add(new Employee { EmployeeId = Leader, EmployeeCode = "TT", FullName = "Tổ trưởng", TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.TeamLeader } });
        Setup(_codes); Setup(_levels); Setup(_evals); Setup(_employees); Setup(_defects);
        _uow.Repository<GradeLevelCode>().AddAsync(Arg.Do<GradeLevelCode>(c => _codes.Add(c)));
        _uow.Repository<GradeLevelCode>().When(r => r.Remove(Arg.Any<GradeLevelCode>())).Do(c => _codes.Remove(c.Arg<GradeLevelCode>()));
        _uow.Repository<GradeLevel>().When(r => r.Remove(Arg.Any<GradeLevel>())).Do(c => _levels.Remove(c.Arg<GradeLevel>()));
        _uow.Repository<GradeEvaluation>().AddAsync(Arg.Do<GradeEvaluation>(e =>
        {
            e.GradeEvaluationId = _evals.Count + 1;
            e.Worker = _employees.First(x => x.EmployeeId == e.WorkerId);
            e.ProposedByNavigation = _employees.First(x => x.EmployeeId == e.ProposedBy);
            _evals.Add(e);
        }));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private GradeService Service() => new(_uow, _user, _notifier, _audit, _clock);

    private GradeEvaluation Eval(string status = GradeEvaluationStatuses.Submitted, string proposed = "B")
    {
        var e = new GradeEvaluation
        {
            GradeEvaluationId = _evals.Count + 1, WorkerId = 40, Worker = _worker, ProposedBy = Leader,
            ProposedByNavigation = _employees.First(x => x.EmployeeId == Leader), TeamId = TeamId, CurrentGradeCode = "C",
            ProposedGradeCode = proposed, Reason = "Làm tốt", EvaluationDate = Today.AddDays(-2), Status = status, CreatedAt = Now,
        };
        _evals.Add(e);
        return e;
    }

    private void Defect(int worker, int daysAgo, string status = DefectStatuses.Recorded) =>
        _defects.Add(new DefectRecord { DefectRecordId = _defects.Count + 1, ResponsibleWorkerId = worker, WorkDate = Today.AddDays(-daysAgo), Status = status });

    // ---------- Mức bậc ----------

    [Fact]
    public async Task Changing_a_grade_amount_adds_a_new_dated_row_and_keeps_the_history()
    {
        _levels.Add(new GradeLevel { GradeLevelId = 1, GradeCode = "A", MonthlyAmount = 600000, EffectiveDate = new(2026, 1, 1) });
        _ = _uow.Repository<GradeLevel>().AddAsync(Arg.Do<GradeLevel>(g => { g.GradeLevelId = 2; _levels.Add(g); }));

        var added = await Service().AddLevelAsync(new SaveGradeLevelDto(" a ", 700000, new(2026, 12, 1)), default);

        added.IsSuccess.Should().BeTrue();
        added.Data!.GradeCode.Should().Be("A");
        added.Data.IsCurrent.Should().BeFalse();   // chưa tới ngày hiệu lực

        var levels = (await Service().GetLevelsAsync(default)).Data!;
        levels.Should().HaveCount(2);
        levels.Single(l => l.MonthlyAmount == 600000).IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task A_grade_level_needs_a_valid_code_and_a_unique_effective_date()
    {
        (await Service().AddLevelAsync(new SaveGradeLevelDto("E", 1, Today), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        _levels.Add(new GradeLevel { GradeCode = "B", EffectiveDate = Today });
        (await Service().AddLevelAsync(new SaveGradeLevelDto("B", 1, Today), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
    }

    // ---------- Đề xuất ----------

    [Fact]
    public async Task A_leader_proposes_a_grade_with_the_recent_defect_count_computed_by_the_system()
    {
        Defect(40, 3); Defect(40, 10, DefectStatuses.Locked); Defect(40, 45);   // quá 30 ngày: không tính
        Defect(40, 2, DefectStatuses.Cancelled); Defect(41, 1);                  // đã hủy / người khác: không tính

        var result = await Service().ProposeAsync(new ProposeGradeDto(40, " b ", " Ít lỗi "), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        var e = _evals.Single();
        e.ProposedGradeCode.Should().Be("B");
        e.CurrentGradeCode.Should().Be("C");
        e.RecentDefectCount.Should().Be(2);
        e.Status.Should().Be(GradeEvaluationStatuses.Submitted);
        e.TeamId.Should().Be(TeamId);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Proposals_are_limited_to_active_workers_of_the_leaders_own_team()
    {
        (await Service().ProposeAsync(new ProposeGradeDto(42, "B", "x"), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().ProposeAsync(new ProposeGradeDto(Leader, "B", "x"), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        _worker.Status = RecordStatus.Resigned;
        (await Service().ProposeAsync(new ProposeGradeDto(40, "B", "x"), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        _evals.Should().BeEmpty();
    }

    [Fact]
    public async Task A_proposal_must_differ_from_the_current_grade_and_use_a_known_code()
    {
        (await Service().ProposeAsync(new ProposeGradeDto(40, "C", "x"), TeamId, default)).Message.Should().Be(Messages.GradeSameAsCurrent);
        (await Service().ProposeAsync(new ProposeGradeDto(40, "Z", "x"), TeamId, default)).Message.Should().Be(Messages.GradeCodeInvalid);
    }

    [Fact]
    public async Task Only_one_pending_proposal_per_worker_and_one_per_leader_per_day()
    {
        Eval(GradeEvaluationStatuses.Submitted);
        (await Service().ProposeAsync(new ProposeGradeDto(40, "A", "x"), TeamId, default)).Message.Should().Be(Messages.GradePendingExists);

        _evals.Clear();
        var decided = Eval(GradeEvaluationStatuses.NotApplied); decided.EvaluationDate = Today;
        (await Service().ProposeAsync(new ProposeGradeDto(40, "A", "x"), TeamId, default)).Message.Should().Be(Messages.GradeProposedToday);
    }

    [Fact]
    public async Task Team_workers_show_current_grade_defects_and_pending_flag()
    {
        Defect(40, 1); Eval(GradeEvaluationStatuses.Submitted);

        var workers = (await Service().GetTeamWorkersAsync(TeamId, default)).Data!;

        workers.Select(w => w.WorkerCode).Should().Equal("CN1", "CN2");
        var w1 = workers.First();
        w1.CurrentGradeCode.Should().Be("C");
        w1.RecentDefectCount.Should().Be(1);
        w1.HasPending.Should().BeTrue();
        workers.Last().HasPending.Should().BeFalse();
    }

    // ---------- Quản đốc đặt bậc trực tiếp ----------

    [Fact]
    public async Task A_direct_set_applies_the_grade_at_once_and_records_history()
    {
        Defect(40, 3);

        var result = await Service().SetGradeAsync(new SetGradeDto(40, " a ", " Thợ giỏi ", null), default);

        result.IsSuccess.Should().BeTrue();
        _worker.CurrentGradeCode.Should().Be("A");
        var e = _evals.Single();
        e.Status.Should().Be(GradeEvaluationStatuses.Applied);
        e.CurrentGradeCode.Should().Be("C");
        e.ProposedGradeCode.Should().Be("A");
        e.EffectiveDate.Should().Be(Today);
        e.ConsideredAt.Should().Be(Now);
        e.ProposedBy.Should().Be(Leader);   // người đặt (ở đây là người đang đăng nhập)
        e.RecentDefectCount.Should().Be(1);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(a => a.TableName == nameof(Employee) && a.Reason == "Thợ giỏi"));
    }

    [Fact]
    public async Task A_direct_set_closes_the_workers_pending_proposal_and_tells_its_author()
    {
        var pending = Eval();   // đề xuất bậc B đang chờ

        var result = await Service().SetGradeAsync(new SetGradeDto(40, "A", "Đặt trực tiếp", new DateOnly(2026, 12, 1)), default);

        result.IsSuccess.Should().BeTrue();
        pending.Status.Should().Be(GradeEvaluationStatuses.NotApplied);
        pending.DecisionNote.Should().Be(Messages.GradeSupersededNote);
        _evals.Last().EffectiveDate.Should().Be(new DateOnly(2026, 12, 1));
        _notifier.Received(1).NotifyEmployees(Arg.Is<IReadOnlyCollection<int>>(ids => ids.Contains(40) && ids.Contains(Leader)),
            Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task A_direct_set_rejects_a_bad_grade_the_same_grade_and_non_workers()
    {
        (await Service().SetGradeAsync(new SetGradeDto(40, "E", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().SetGradeAsync(new SetGradeDto(40, "C", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().SetGradeAsync(new SetGradeDto(Leader, "A", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        _worker.Status = RecordStatus.Resigned;
        (await Service().SetGradeAsync(new SetGradeDto(40, "A", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        _evals.Should().BeEmpty();
        _worker.CurrentGradeCode.Should().Be("C");
    }

    [Fact]
    public async Task A_second_direct_set_on_the_same_day_is_refused_with_a_clear_message()
    {
        (await Service().SetGradeAsync(new SetGradeDto(40, "A", "x", null), default)).IsSuccess.Should().BeTrue();

        var again = await Service().SetGradeAsync(new SetGradeDto(40, "B", "x", null), default);

        again.IsSuccess.Should().BeFalse();
        again.Message.Should().Be(Messages.GradeSetToday);
        _worker.CurrentGradeCode.Should().Be("A");
    }

    [Fact]
    public async Task All_active_workers_of_every_team_can_be_listed_for_a_direct_set()
    {
        _worker.Team = new Team { TeamName = "Tổ may 1" };
        Eval();

        var result = await Service().GetAllWorkersAsync(default);

        result.Data!.Select(w => w.WorkerId).Should().BeEquivalentTo(new[] { 40, 41, 42 });
        result.Data!.Single(w => w.WorkerId == 40).HasPending.Should().BeTrue();
    }

    [Fact]
    public void The_direct_set_validator_requires_a_reason_and_a_known_grade()
    {
        var validator = new SetGradeDtoValidator();
        validator.Validate(new SetGradeDto(40, "A", "Lý do", null)).IsValid.Should().BeTrue();
        validator.Validate(new SetGradeDto(40, "A", " ", null)).IsValid.Should().BeFalse();
        validator.Validate(new SetGradeDto(40, "e", "x", null)).IsValid.Should().BeTrue();   // bậc mới do Admin thêm, chỉ kiểm định dạng
        validator.Validate(new SetGradeDto(40, "TOOLONG", "x", null)).IsValid.Should().BeFalse();
        validator.Validate(new SetGradeDto(40, "A-1", "x", null)).IsValid.Should().BeFalse();
        validator.Validate(new SetGradeDto(0, "A", "x", null)).IsValid.Should().BeFalse();
    }

    // ---------- Danh mục bậc ----------

    [Fact]
    public async Task Admin_adds_a_new_grade_and_it_can_then_be_assigned()
    {
        var added = await Service().CreateCodeAsync(new SaveGradeCodeDto("e", "Bậc E – Xuất sắc"), default);
        added.IsSuccess.Should().BeTrue();
        _codes.Should().Contain(c => c.GradeCode == "E" && c.Status == GradeCodes.Active);   // mã chuẩn hóa chữ hoa

        (await Service().SetGradeAsync(new SetGradeDto(40, "E", "Thưởng bậc", null), default)).IsSuccess.Should().BeTrue();
        _worker.CurrentGradeCode.Should().Be("E");
    }

    [Fact]
    public async Task A_duplicate_or_malformed_code_is_refused()
    {
        (await Service().CreateCodeAsync(new SaveGradeCodeDto("A", "trùng"), default)).Message.Should().Be(Messages.GradeCodeDuplicate);
        (await Service().CreateCodeAsync(new SaveGradeCodeDto("A-1", "sai"), default)).Message.Should().Be(Messages.GradeCodeMalformed);
    }

    [Fact]
    public async Task An_inactive_grade_can_no_longer_be_assigned_or_proposed()
    {
        (await Service().SetGradeAsync(new SetGradeDto(40, "X", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().ProposeAsync(new ProposeGradeDto(40, "X", "x"), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);

        var update = await Service().UpdateCodeAsync("D", new UpdateGradeCodeDto("Bậc D", "Inactive"), default);
        update.IsSuccess.Should().BeTrue();
        (await Service().SetGradeAsync(new SetGradeDto(40, "D", "x", null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
    }

    [Fact]
    public async Task A_grade_in_use_cannot_be_deleted_but_an_unused_one_can()
    {
        // C đang gán cho công nhân 1 → chỉ ngừng dùng được; X chưa ai dùng → xóa được.
        (await Service().DeleteCodeAsync("C", default)).Message.Should().Be(Messages.GradeCodeInUse);
        _codes.Should().Contain(c => c.GradeCode == "C");

        (await Service().DeleteCodeAsync("X", default)).IsSuccess.Should().BeTrue();
        (await Service().DeleteCodeAsync("Q", default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Only_a_future_grade_amount_can_be_edited_or_deleted()
    {
        var past = new GradeLevel { GradeLevelId = 1, GradeCode = "A", MonthlyAmount = 100, EffectiveDate = Today.AddDays(-10) };
        var future = new GradeLevel { GradeLevelId = 2, GradeCode = "A", MonthlyAmount = 100, EffectiveDate = Today.AddDays(10) };
        _levels.AddRange([past, future]);

        (await Service().UpdateLevelAsync(1, new UpdateGradeLevelDto(999, Today.AddDays(5)), default)).Message.Should().Be(Messages.GradeLevelLocked);
        (await Service().DeleteLevelAsync(1, default)).Message.Should().Be(Messages.GradeLevelLocked);

        (await Service().UpdateLevelAsync(2, new UpdateGradeLevelDto(999, Today.AddDays(20)), default)).IsSuccess.Should().BeTrue();
        future.MonthlyAmount.Should().Be(999);
        (await Service().DeleteLevelAsync(2, default)).IsSuccess.Should().BeTrue();
        _levels.Should().ContainSingle().Which.GradeLevelId.Should().Be(1);
    }

    // ---------- Quyết định ----------

    [Fact]
    public async Task Applying_updates_the_workers_grade_and_notifies_the_worker_and_leader()
    {
        var e = Eval();

        var result = await Service().ApplyAsync(e.GradeEvaluationId, new ApplyGradeDto(new DateOnly(2026, 12, 1), " theo bảng đánh giá "), default);

        result.IsSuccess.Should().BeTrue();
        e.Status.Should().Be(GradeEvaluationStatuses.Applied);
        e.EffectiveDate.Should().Be(new DateOnly(2026, 12, 1));
        e.ConsideredAt.Should().Be(Now);
        e.DecisionNote.Should().Be("theo bảng đánh giá");
        _worker.CurrentGradeCode.Should().Be("B");
        _audit.Received(1).Add(Arg.Is<AuditEntry>(a => a.TableName == nameof(Employee) && a.RecordId == 40));
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Contains(40) && ids.Contains(Leader)), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task The_effective_date_defaults_to_today()
    {
        var e = Eval();

        await Service().ApplyAsync(e.GradeEvaluationId, new ApplyGradeDto(null, null), default);

        e.EffectiveDate.Should().Be(Today);
    }

    [Fact]
    public async Task Rejecting_keeps_the_grade_and_stores_the_reason()
    {
        var e = Eval();

        var result = await Service().RejectAsync(e.GradeEvaluationId, new ReasonDto(" lỗi còn nhiều "), default);

        result.IsSuccess.Should().BeTrue();
        e.Status.Should().Be(GradeEvaluationStatuses.NotApplied);
        e.DecisionNote.Should().Be("lỗi còn nhiều");
        _worker.CurrentGradeCode.Should().Be("C");
    }

    [Fact]
    public async Task A_decided_proposal_cannot_be_changed_again()
    {
        var applied = Eval(GradeEvaluationStatuses.Applied);
        var rejected = Eval(GradeEvaluationStatuses.NotApplied);

        (await Service().ApplyAsync(applied.GradeEvaluationId, new ApplyGradeDto(null, null), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().RejectAsync(rejected.GradeEvaluationId, new ReasonDto("x"), default)).ErrorCode.Should().Be(ErrorCodes.GradeInvalid);
        (await Service().ApplyAsync(999, new ApplyGradeDto(null, null), default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task A_leader_only_sees_the_proposals_of_their_own_team()
    {
        Eval();
        var other = Eval(); other.TeamId = 9;

        var mine = (await Service().GetPagedAsync(new GradeEvaluationQuery(), TeamId, default)).Data!;
        var all = (await Service().GetPagedAsync(new GradeEvaluationQuery(), null, default)).Data!;

        mine.Items.Should().HaveCount(1);
        all.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Validators_require_name_amount_reason_and_worker()
    {
        var level = new SaveGradeLevelDtoValidator();
        level.Validate(new SaveGradeLevelDto("A", 1000, Today)).IsValid.Should().BeTrue();
        level.Validate(new SaveGradeLevelDto("A", -1, Today)).IsValid.Should().BeFalse();

        var propose = new ProposeGradeDtoValidator();
        propose.Validate(new ProposeGradeDto(1, "A", "lý do")).IsValid.Should().BeTrue();
        propose.Validate(new ProposeGradeDto(0, "A", "lý do")).IsValid.Should().BeFalse();
        propose.Validate(new ProposeGradeDto(1, "A", " ")).IsValid.Should().BeFalse();
    }
}
