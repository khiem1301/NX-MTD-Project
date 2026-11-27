using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-148: AI gợi ý bậc thợ cho cả tổ — tổng hợp số liệu ẩn danh, lưu tạm, sửa, xác nhận thành đề xuất.</summary>
public class GradeSuggestionServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 20);
    private const int Leader = 20;
    private const int TeamId = 5;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IGradeAdvisor _advisor = Substitute.For<IGradeAdvisor>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly FakeStore _store = new();

    private readonly List<GradeLevelCode> _codes = new()
    {
        new() { GradeCode = "A", Status = GradeCodes.Active },
        new() { GradeCode = "B", Status = GradeCodes.Active },
        new() { GradeCode = "C", Status = GradeCodes.Active },
    };
    private readonly List<GradeLevel> _levels = new();
    private readonly List<Employee> _employees = new();
    private readonly List<GradeEvaluation> _evals = new();
    private readonly List<DailyTimesheet> _sheets = new();
    private readonly List<OutputRecord> _outputs = new();
    private readonly List<DefectRecord> _defects = new();

    public GradeSuggestionServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _user.EmployeeId.Returns(Leader);
        _advisor.IsConfigured.Returns(true);
        var worker = new Role { RoleCode = RoleCodes.Worker };
        _employees.Add(new Employee { EmployeeId = 40, EmployeeCode = "CN1", FullName = "Công nhân 1", TeamId = TeamId, Status = RecordStatus.Active, Role = worker, CurrentGradeCode = "C", StartDate = new(2025, 11, 20) });
        _employees.Add(new Employee { EmployeeId = 41, EmployeeCode = "CN2", FullName = "Công nhân 2", TeamId = TeamId, Status = RecordStatus.Active, Role = worker, CurrentGradeCode = "B", StartDate = new(2026, 5, 1) });
        _employees.Add(new Employee { EmployeeId = Leader, EmployeeCode = "TT", FullName = "Tổ trưởng", TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.TeamLeader } });
        Setup(_codes); Setup(_levels); Setup(_employees); Setup(_evals); Setup(_sheets); Setup(_outputs); Setup(_defects);
        _uow.Repository<GradeEvaluation>().AddAsync(Arg.Do<GradeEvaluation>(e => { e.GradeEvaluationId = _evals.Count + 100; _evals.Add(e); }));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private GradeSuggestionService Service() =>
        new(_uow, _user, _clock, _advisor, _store, _notifier, _audit, Options.Create(new GradeSuggestionOptions { RetentionHours = 24 }));

    private void AdvisorReturns(params GradeAdvice[] advice) =>
        _advisor.SuggestAsync(Arg.Any<GradeAdvisorRequest>(), Arg.Any<CancellationToken>()).Returns(advice);

    private static GradeWorkerMetricsDto NoMetrics => new(0, 0, 0, 0, 0, 0, 0, null, null, 0, 0, null);

    private void Draft(DateTime? expiresAt = null, params (int Id, string Code, string? Current, string Proposed)[] rows) =>
        _store.Saved[TeamId] = new GradeSuggestionDto(TeamId, Today.AddDays(-89), Today, Now.AddHours(-1), expiresAt ?? Now.AddHours(23), "Tổ trưởng", null,
            rows.Select(r => new GradeSuggestionItemDto(r.Id, r.Code, r.Code, r.Current, null, NoMetrics, r.Proposed, "AI", r.Proposed, "AI", false)).ToList());

    [Fact]
    public async Task GenerateAsync_AdvisorSuggests_SendsAnonymousMetricsAndStoresDraft()
    {
        var op = new Operation { OperationId = 1, SkillLevel = 3, StandardTimeSec = 288 };
        _sheets.Add(new DailyTimesheet { EmployeeId = 40, WorkDate = Today.AddDays(-1), WorkdayValue = 1, WorkedHours = 8 });
        _sheets.Add(new DailyTimesheet { EmployeeId = 40, WorkDate = Today.AddDays(-2), WorkdayValue = 1, WorkedHours = 8 });
        _outputs.Add(new OutputRecord { WorkerId = 40, WorkDate = Today.AddDays(-1), OperationId = 1, Operation = op, Quantity = 100 });
        _defects.Add(new DefectRecord { ResponsibleWorkerId = 40, WorkDate = Today.AddDays(-1), DefectType = DefectTypes.Rework, Quantity = 2, Status = DefectStatuses.Recorded });
        GradeAdvisorRequest? sent = null;
        _advisor.SuggestAsync(Arg.Do<GradeAdvisorRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(new[] { new GradeAdvice("W1", "B", "Hiệu suất tốt") });

        var result = await Service().GenerateAsync(TeamId, new GenerateGradeSuggestionDto(30), default);

        result.IsSuccess.Should().BeTrue();
        sent!.PeriodDays.Should().Be(30);
        _store.Saved[TeamId].PeriodFrom.Should().Be(Today.AddDays(-29));
        sent.Workers.Select(w => w.Key).Should().Equal("W1", "W2");
        var m = sent.Workers[0].Metrics;
        m.WorkDays.Should().Be(2);
        m.OutputQty.Should().Be(100);
        m.EfficiencyPercent.Should().Be(50);       // 100 × 288 giây = 8 giờ chuẩn / 16 giờ làm
        m.DefectRatePercent.Should().Be(2);
        m.TenureMonths.Should().Be(12);
        var items = _store.Saved[TeamId].Items;
        items[0].ProposedGradeCode.Should().Be("B");
        items[1].ProposedGradeCode.Should().Be("B");   // AI không trả cho W2: giữ bậc hiện tại
        items[1].Reason.Should().Be(Messages.GradeAiMissingAdvice);
    }

    [Fact]
    public async Task GenerateAsync_GradesRankedByCurrentAmount_SendsCodeAndRankOnly()
    {
        _levels.Add(new GradeLevel { GradeCode = "A", MonthlyAmount = 6_000_000m, EffectiveDate = Today.AddMonths(-6) });
        _levels.Add(new GradeLevel { GradeCode = "B", MonthlyAmount = 4_000_000m, EffectiveDate = Today.AddMonths(-6) });
        _levels.Add(new GradeLevel { GradeCode = "B", MonthlyAmount = 9_000_000m, EffectiveDate = Today.AddDays(10) });   // chưa hiệu lực
        _levels.Add(new GradeLevel { GradeCode = "C", MonthlyAmount = 5_000_000m, EffectiveDate = Today.AddMonths(-1) });
        GradeAdvisorRequest? sent = null;
        _advisor.SuggestAsync(Arg.Do<GradeAdvisorRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GradeAdvice>());

        await Service().GenerateAsync(TeamId, new GenerateGradeSuggestionDto(30), default);

        sent!.Grades.Should().Equal(new GradeAdvisorGrade("A", 1), new GradeAdvisorGrade("C", 2), new GradeAdvisorGrade("B", 3));
    }

    [Fact]
    public async Task GenerateAsync_AdvisorFails_ReturnsAiUnavailableAndKeepsOldDraft()
    {
        Draft(null, (40, "CN1", "C", "B"));
        _advisor.SuggestAsync(Arg.Any<GradeAdvisorRequest>(), Arg.Any<CancellationToken>()).Returns((IReadOnlyList<GradeAdvice>?)null);

        var result = await Service().GenerateAsync(TeamId, new GenerateGradeSuggestionDto(90), default);

        result.ErrorCode.Should().Be(ErrorCodes.GradeAiUnavailable);
        _store.Saved[TeamId].Items.Single().ProposedGradeCode.Should().Be("B");
    }

    [Fact]
    public async Task UpdateItemAsync_LeaderChangesGrade_StoredDraftChangesAndIsMarkedEdited()
    {
        Draft(null, (40, "CN1", "C", "B"));

        var result = await Service().UpdateItemAsync(TeamId, 40, new UpdateGradeSuggestionItemDto(" a ", "Tay nghề rất tốt"), default);

        result.IsSuccess.Should().BeTrue();
        var item = _store.Saved[TeamId].Items.Single();
        item.ProposedGradeCode.Should().Be("A");
        item.Reason.Should().Be("Tay nghề rất tốt");
        item.AiGradeCode.Should().Be("B");
        item.IsEdited.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveItemAsync_LeaderRemovesRow_RowIsGoneAndNotConfirmed()
    {
        Draft(null, (40, "CN1", "C", "B"), (41, "CN2", "B", "A"));

        var removed = await Service().RemoveItemAsync(TeamId, 41, default);
        var confirmed = await Service().ConfirmAsync(TeamId, default);

        removed.IsSuccess.Should().BeTrue();
        _store.Saved[TeamId].Items.Select(i => i.WorkerId).Should().Equal(40);
        confirmed.Data!.Created.Should().Be(1);
        _evals.Single().WorkerId.Should().Be(40);
        (await Service().RemoveItemAsync(TeamId, 41, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task ConfirmAsync_SomeGradesUnchanged_CreatesProposalsOnlyForChangedGrades()
    {
        Draft(null, (40, "CN1", "C", "B"), (41, "CN2", "B", "B"));

        var result = await Service().ConfirmAsync(TeamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Created.Should().Be(1);
        result.Data.Unchanged.Should().Be(1);
        var e = _evals.Single();
        e.WorkerId.Should().Be(40);
        e.ProposedGradeCode.Should().Be("B");
        e.Status.Should().Be(GradeEvaluationStatuses.Submitted);
        _store.Saved[TeamId].ConfirmedAt.Should().Be(Now);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAsync_WorkerHasPendingProposal_ReplacesItInsteadOfAddingAnother()
    {
        _evals.Add(new GradeEvaluation { GradeEvaluationId = 7, WorkerId = 40, ProposedBy = 21, TeamId = TeamId, CurrentGradeCode = "C", ProposedGradeCode = "A",
            Reason = "Cũ", EvaluationDate = Today.AddDays(-5), Status = GradeEvaluationStatuses.Submitted });
        Draft(null, (40, "CN1", "C", "B"));

        var result = await Service().ConfirmAsync(TeamId, default);

        result.Data!.Replaced.Should().Be(1);
        result.Data.Created.Should().Be(0);
        var e = _evals.Single();
        e.GradeEvaluationId.Should().Be(7);
        e.ProposedGradeCode.Should().Be("B");
        e.ProposedBy.Should().Be(Leader);
        e.EvaluationDate.Should().Be(Today);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(a => a.TableName == nameof(GradeEvaluation) && a.RecordId == 7));
    }

    [Fact]
    public async Task ConfirmAsync_DraftExpired_ReturnsNotFoundAndDeletesDraft()
    {
        Draft(Now.AddMinutes(-1), (40, "CN1", "C", "B"));

        var result = await Service().ConfirmAsync(TeamId, default);

        result.Kind.Should().Be(ResultKind.NotFound);
        _store.Saved.Should().NotContainKey(TeamId);
        _evals.Should().BeEmpty();
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void GenerateValidator_LookbackDays_AllowsOnlySevenToNinetyDays(int days, bool valid) =>
        new GenerateGradeSuggestionDtoValidator().Validate(new GenerateGradeSuggestionDto(days)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("C", "A", GradeChangeDirections.Up)]
    [InlineData("A", "C", GradeChangeDirections.Down)]
    [InlineData("B", "B", GradeChangeDirections.Same)]
    [InlineData(null, "B", GradeChangeDirections.Up)]
    [InlineData("B", "X", null)]
    [InlineData("B", null, null)]
    public void DirectionOf_RanksByAmount_ReturnsUpDownOrSame(string? current, string? proposed, string? expected)
    {
        var ranks = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 3 };

        GradeSuggestionService.DirectionOf(current, proposed, ranks).Should().Be(expected);
    }

    [Fact]
    public async Task GetAsync_ProposedGradeHasHigherAmount_ItemDirectionIsUp()
    {
        _levels.Add(new GradeLevel { GradeCode = "A", MonthlyAmount = 6_000_000m, EffectiveDate = Today.AddMonths(-6) });
        _levels.Add(new GradeLevel { GradeCode = "B", MonthlyAmount = 4_000_000m, EffectiveDate = Today.AddMonths(-6) });
        _levels.Add(new GradeLevel { GradeCode = "C", MonthlyAmount = 2_000_000m, EffectiveDate = Today.AddMonths(-6) });
        Draft(null, (40, "CN1", "C", "A"), (41, "CN2", "B", "C"));

        var result = await Service().GetAsync(TeamId, default);

        result.Data!.Items.Select(i => i.Direction).Should().Equal(GradeChangeDirections.Up, GradeChangeDirections.Down);
    }

    private sealed class FakeStore : IGradeSuggestionStore
    {
        public Dictionary<int, GradeSuggestionDto> Saved { get; } = new();
        public Task<GradeSuggestionDto?> GetAsync(int teamId, CancellationToken ct) => Task.FromResult(Saved.GetValueOrDefault(teamId));
        public Task SaveAsync(GradeSuggestionDto draft, CancellationToken ct) { Saved[draft.TeamId] = draft; return Task.CompletedTask; }
        public Task DeleteAsync(int teamId, CancellationToken ct) { Saved.Remove(teamId); return Task.CompletedTask; }
    }
}
