using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class KpiCalendarServiceTests
{
    private const int MyKpi = 1;
    private const int OtherKpi = 2;
    private const int MyTeam = 1;
    private const int OtherTeam = 2;

    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly List<KpiTarget> _targets = [];
    private readonly List<KpiDailyTarget> _days = [];
    private readonly List<CutHandoverLine> _handoverLines = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<QcBatch> _qcBatches = [];
    private readonly List<DefectRecord> _defects = [];
    private readonly KpiCalendarService _sut;

    public KpiCalendarServiceTests()
    {
        _clock.Today.Returns(Today);
        NowIs(7, 0);
        _settings.GetTimeAsync(SettingKeys.KpiEditCutoffTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<TimeOnly>(1));
        SetupQueryable(_targets);
        SetupQueryable(_handoverLines);
        SetupQueryable(_issueLines);
        SetupQueryable(_qcBatches);
        SetupQueryable(_defects);

        var days = SetupQueryable(_days);
        days.AddAsync(Arg.Do<KpiDailyTarget>(_days.Add)).Returns(Task.CompletedTask);
        days.When(r => r.RemoveRange(Arg.Any<IEnumerable<KpiDailyTarget>>()))
            .Do(c => c.Arg<IEnumerable<KpiDailyTarget>>().ToList().ForEach(d => _days.Remove(d)));

        _uow.SaveChangesAsync().Returns(Task.FromResult(1));

        _targets.Add(Target(MyKpi, MyTeam));
        _sut = new KpiCalendarService(_uow, _clock, _audit, _settings);
    }

    private void NowIs(int hour, int minute) => _clock.Now.Returns(Today.ToDateTime(new TimeOnly(hour, minute)));

    private IGenericRepository<T> SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
        return repo;
    }

    private static KpiTarget Target(int id, int teamId, int targetQty = 1000, string status = KpiStatuses.Active) => new()
    {
        KpiTargetId = id, OrderId = 1, ProductId = 1, SewingTeamId = teamId,
        StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31), TargetFinishedQty = targetQty, WorkingDays = 27,
        Status = status, Order = new CustomerOrder { OrderCode = "DH001" }, Product = new Product { ProductCode = "MH001", ProductName = "Áo" },
        SewingTeam = new Team { TeamName = $"Tổ may {teamId}" },
    };

    private static OrderLine Line() => new() { OrderId = 1, ProductId = 1 };

    private void GivenCut(int qty) => _handoverLines.Add(new CutHandoverLine
    {
        ConfirmedQty = qty, ProductComponentMaterialId = null, OrderLine = Line(),
        CutHandover = new CutHandover { Status = HandoverStatuses.Confirmed },
    });

    private void GivenIssued(int teamId, int qty) => _issueLines.Add(new MaterialIssueLine
    {
        IssuedQty = qty, ConfirmedQty = qty, ProductComponentMaterialId = null, OrderLineId = 1001, OrderLine = Line(),
        MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = IssueStatuses.Received, ReceivingTeamId = teamId },
    });

    private void GivenFinished(int teamId, DateOnly date, int qty) =>
        _qcBatches.Add(new QcBatch { SewingTeamId = teamId, WorkDate = date, FinishedQty = qty, OrderLine = Line() });

    private void GivenPlan(int kpiId, DateOnly date, int qty) =>
        _days.Add(new KpiDailyTarget { KpiTargetId = kpiId, WorkDate = date, TargetQty = qty });

    private static SaveKpiDailyPlanDto Plan(params (DateOnly Date, int Qty)[] days) =>
        new(days.Select(d => new KpiDailyTargetInputDto(d.Date, d.Qty)).ToList());

    private Task<Result<KpiCalendarDto>> Save(params (DateOnly Date, int Qty)[] days) =>
        _sut.SaveDailyPlanAsync(MyKpi, Plan(days), CancellationToken.None);

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNotFound()
    {
        var result = await _sut.GetAsync(99, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.KpiNotFound);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheOrderDueDate()
    {
        _targets[0].Order.DueDate = new DateOnly(2026, 10, 20);

        (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!.DueDate.Should().Be(new DateOnly(2026, 10, 20));
    }

    [Fact]
    public async Task SaveDailyPlanAsync_DayAfterDueDate_IsOnlyAWarningAndStillSaves()
    {
        GivenCut(100);
        _targets[0].Order.DueDate = Today;

        (await Save((Tomorrow, 10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ReturnsEveryDayOfThePeriod_WithPlanAndActualUpToToday()
    {
        GivenPlan(MyKpi, Yesterday, 50);
        GivenPlan(MyKpi, Tomorrow, 60);
        GivenFinished(MyTeam, Yesterday, 40);
        GivenFinished(OtherTeam, Yesterday, 999);   // tổ khác: không lẫn vào

        var calendar = (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!;

        calendar.Days.Should().HaveCount(31);
        calendar.FinishedQty.Should().Be(40);
        calendar.CanEdit.Should().BeTrue();
        calendar.Days.Single(d => d.WorkDate == Yesterday).Should().Be(new KpiCalendarDayDto(Yesterday, 50, 40));
        calendar.Days.Single(d => d.WorkDate == Today).Should().Be(new KpiCalendarDayDto(Today, 0, 0));
        calendar.Days.Single(d => d.WorkDate == Tomorrow).Should().Be(new KpiCalendarDayDto(Tomorrow, 60, null));
    }

    [Fact]
    public async Task GetAsync_Btp_IsWarehousePlusWhatTheTeamHolds()
    {
        GivenCut(400);
        GivenIssued(MyTeam, 300);                     // kho còn 100
        GivenFinished(MyTeam, Yesterday, 120);
        _defects.Add(new DefectRecord
        {
            SewingTeamId = MyTeam, DefectType = DefectTypes.Scrap, Status = DefectStatuses.Recorded, Quantity = 10, OrderLine = Line(),
        });

        var btp = (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!.Btp;

        btp.Should().Be(new KpiBtpDto(100, 170, 0, 270));   // tổ giữ 300 − 120 đạt − 10 hủy
    }

    [Fact]
    public async Task GetAsync_Btp_SubtractsOnlyWhatOtherTeamsMustTakeFromTheWarehouse()
    {
        _targets.Add(Target(OtherKpi, OtherTeam));
        _targets.Add(Target(3, 3, status: KpiStatuses.Cancelled));
        GivenCut(500);
        GivenIssued(OtherTeam, 100);                  // kho còn 400; tổ khác tự có 100
        GivenPlan(OtherKpi, Tomorrow, 250);           // còn phải làm 250, cần lấy thêm 150 từ kho
        GivenPlan(3, Tomorrow, 900);                  // KPI đã hủy: không giữ chỗ

        var btp = (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!.Btp;

        btp.Should().Be(new KpiBtpDto(400, 0, 150, 250));
    }

    [Fact]
    public async Task GetAsync_Btp_AnotherPeriodOfTheSameTeam_CompetesForWhatTheTeamHolds()
    {
        _targets.Add(Target(OtherKpi, MyTeam));
        GivenCut(100);
        GivenIssued(MyTeam, 100);                     // tổ giữ 100, kho hết
        GivenPlan(OtherKpi, Tomorrow, 80);            // chỉ tiêu kỳ khác của chính tổ này còn phải làm 80

        var btp = (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!.Btp;

        btp.Should().Be(new KpiBtpDto(0, 100, 80, 20));
    }

    [Theory]
    [InlineData(7, 59, "2026-10-15")]
    [InlineData(8, 0, "2026-10-16")]
    [InlineData(16, 0, "2026-10-16")]
    public async Task GetAsync_FirstEditableDate_MovesToTomorrowFromTheCutoff(int hour, int minute, string expected)
    {
        NowIs(hour, minute);

        var calendar = (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!;

        calendar.FirstEditableDate.Should().Be(DateOnly.Parse(expected));
        calendar.EditCutoff.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public async Task SaveDailyPlanAsync_TodayBeforeCutoff_IsSaved()
    {
        GivenCut(100);
        NowIs(7, 59);

        (await Save((Today, 10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDailyPlanAsync_TodayFromCutoff_IsRejected_ButUnchangedTodayAndFutureDaysAreSaved()
    {
        GivenCut(100);
        GivenPlan(MyKpi, Today, 10);
        NowIs(8, 0);

        (await Save((Today, 11))).Message.Should().Be(string.Format(Messages.KpiDayLocked, new TimeOnly(8, 0)));
        (await Save((Today, 10), (Tomorrow, 20))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDailyPlanAsync_CutoffComesFromSettings()
    {
        _settings.GetTimeAsync(SettingKeys.KpiEditCutoffTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TimeOnly(9, 30));
        GivenCut(100);
        NowIs(9, 0);

        (await Save((Today, 10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDailyPlanAsync_WithinBtp_ReplacesPlanAndCountsWorkingDays()
    {
        GivenCut(300);
        GivenPlan(MyKpi, Tomorrow, 10);

        var result = await Save((Today, 100), (Tomorrow, 200));

        result.IsSuccess.Should().BeTrue();
        _days.Select(d => (d.WorkDate, d.TargetQty)).Should().BeEquivalentTo([(Today, 100), (Tomorrow, 200)]);
        _targets[0].WorkingDays.Should().Be(2);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(KpiTarget) && e.RecordId == MyKpi));
    }

    [Fact]
    public async Task SaveDailyPlanAsync_ClearAll_FallsBackToDaysInPeriodWithoutSundays()
    {
        GivenPlan(MyKpi, Tomorrow, 10);

        (await Save()).IsSuccess.Should().BeTrue();

        _days.Should().BeEmpty();
        _targets[0].WorkingDays.Should().Be(27);      // tháng 10/2026 có 4 Chủ nhật
    }

    [Fact]
    public async Task SaveDailyPlanAsync_AboveAvailableBtp_IsRejectedAndKeepsPlan()
    {
        GivenCut(300);
        GivenPlan(MyKpi, Tomorrow, 10);

        var result = await Save((Tomorrow, 301));

        result.ErrorCode.Should().Be(ErrorCodes.KpiExceedsStock);
        result.Message.Should().Contain("(301)").And.Contain("(300)");
        _days.Should().ContainSingle(d => d.TargetQty == 10);
    }

    [Fact]
    public async Task SaveDailyPlanAsync_FinishedQtyNoLongerNeedsBtp()
    {
        GivenCut(300);
        GivenIssued(MyTeam, 300);
        GivenFinished(MyTeam, Yesterday, 200);        // tổ còn giữ 100
        GivenPlan(MyKpi, Yesterday, 200);

        (await Save((Yesterday, 200), (Tomorrow, 100))).IsSuccess.Should().BeTrue();
        (await Save((Yesterday, 200), (Tomorrow, 101))).ErrorCode.Should().Be(ErrorCodes.KpiExceedsStock);
    }

    [Fact]
    public async Task SaveDailyPlanAsync_Reducing_IsAllowedEvenIfBtpIsAlreadyShort()
    {
        GivenCut(100);
        GivenPlan(MyKpi, Tomorrow, 500);

        (await Save((Tomorrow, 400))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDailyPlanAsync_SumAboveTotalTarget_IsRejected()
    {
        GivenCut(5000);

        var result = await Save((Today, 600), (Tomorrow, 401));

        result.ErrorCode.Should().Be(ErrorCodes.KpiPlanInvalid);
        result.Message.Should().Be(string.Format(Messages.KpiPlanExceedsTotal, 1001, 1000));
    }

    [Fact]
    public async Task SaveDailyPlanAsync_DateOutsidePeriod_IsRejected()
    {
        var result = await Save((new DateOnly(2026, 11, 1), 10));

        result.Message.Should().Be(Messages.KpiDailyTargetOutOfPeriod);
    }

    [Theory]
    [InlineData(50)]    // sửa số
    [InlineData(0)]     // bỏ ngày đã qua
    public async Task SaveDailyPlanAsync_ChangingPastDay_IsRejected(int qty)
    {
        GivenCut(1000);
        GivenPlan(MyKpi, Yesterday, 40);

        var result = qty > 0 ? await Save((Yesterday, qty)) : await Save();

        result.Message.Should().Be(string.Format(Messages.KpiDayLocked, new TimeOnly(8, 0)));
    }

    [Fact]
    public async Task SaveDailyPlanAsync_AddingNewPastDay_IsRejected()
    {
        GivenCut(1000);

        (await Save((Yesterday, 10))).Message.Should().Be(string.Format(Messages.KpiDayLocked, new TimeOnly(8, 0)));
    }

    [Theory]
    [InlineData(OrderStatuses.Stopped)]
    [InlineData(OrderStatuses.Cancelled)]
    public async Task ClosedOrder_MakesTheCalendarReadOnlyAndRejectsSaving(string status)
    {
        GivenCut(100);
        _targets[0].Order.Status = status;

        (await _sut.GetAsync(MyKpi, CancellationToken.None)).Data!.CanEdit.Should().BeFalse();
        (await Save((Tomorrow, 10))).Message.Should().Be(Messages.KpiOrderClosed);
        _days.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveDailyPlanAsync_NotActive_ReturnsInvalidStatus()
    {
        _targets[0].Status = KpiStatuses.Closed;

        (await Save((Tomorrow, 10))).ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
    }
}

public class SaveKpiDailyPlanDtoValidatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 15);
    private readonly SaveKpiDailyPlanDtoValidator _validator = new();

    [Fact]
    public void Empty_HasNoError() =>
        _validator.TestValidate(new SaveKpiDailyPlanDto([])).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Null_HasErrorWithoutThrowing() =>
        _validator.TestValidate(new SaveKpiDailyPlanDto(null!)).ShouldHaveValidationErrorFor(x => x.DailyTargets);

    [Fact]
    public void DuplicateDate_HasError() =>
        _validator.TestValidate(new SaveKpiDailyPlanDto([new(Day, 1), new(Day, 2)]))
            .ShouldHaveValidationErrorFor(x => x.DailyTargets);

    [Fact]
    public void NonPositiveQty_HasError() =>
        _validator.TestValidate(new SaveKpiDailyPlanDto([new(Day, 0)])).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(1_000_001, false)]
    [InlineData(int.MaxValue, false)]   // cộng nhiều ngày sẽ tràn int
    [InlineData(-1, false)]
    public void Qty_Boundaries(int qty, bool valid) =>
        _validator.Validate(new SaveKpiDailyPlanDto([new(Day, qty)])).IsValid.Should().Be(valid);

    [Fact]
    public void NullRow_HasErrorWithoutThrowing() =>
        _validator.Validate(new SaveKpiDailyPlanDto([null!])).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(366, true)]
    [InlineData(367, false)]
    public void RowCount_IsLimitedTo366(int count, bool valid) =>
        _validator.Validate(new SaveKpiDailyPlanDto(Enumerable.Range(0, count).Select(i => new KpiDailyTargetInputDto(Day.AddDays(i), 1)).ToList()))
            .IsValid.Should().Be(valid);
}
