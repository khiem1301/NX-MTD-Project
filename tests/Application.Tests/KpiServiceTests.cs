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

public class KpiServiceTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 31);
    private static readonly DateOnly Today = new(2026, 8, 31);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly List<KpiTarget> _kpiTargets = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Product> _products = [];
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<VwKpiDailyResult> _dailyResults = [];
    private readonly List<KpiDailyTarget> _days = [];
    private readonly KpiService _sut;

    public KpiServiceTests()
    {
        SetupQueryable(_kpiTargets);
        SetupQueryable(_orders);
        SetupQueryable(_products);
        SetupQueryable(_teams);
        SetupQueryable(_orderLines);
        SetupQueryable(_dailyResults);
        SetupQueryable(_days).When(r => r.RemoveRange(Arg.Any<IEnumerable<KpiDailyTarget>>()))
            .Do(c => c.Arg<IEnumerable<KpiDailyTarget>>().ToList().ForEach(d => _days.Remove(d)));

        // Giả lập EF gán navigation (Order/Product/SewingTeam) sau khi lưu, để Select chiếu DTO không bị null.
        _uow.Repository<KpiTarget>().AddAsync(Arg.Do<KpiTarget>(e =>
        {
            e.Order = _orders.First(o => o.OrderId == e.OrderId);
            e.Product = _products.First(p => p.ProductId == e.ProductId);
            e.SewingTeam = _teams.First(t => t.TeamId == e.SewingTeamId);
            _kpiTargets.Add(e);
        })).Returns(Task.CompletedTask);

        _uow.SaveChangesAsync().Returns(Task.FromResult(1));

        _clock.Now.Returns(new DateTime(2026, 9, 30, 7, 0, 0));
        _clock.Today.Returns(Today);
        _settings.GetIntAsync(SettingKeys.KpiDueWarningDays, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(c => c.ArgAt<int>(1));
        _settings.GetTimeAsync(SettingKeys.KpiEditCutoffTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<TimeOnly>(1));
        _sut = new KpiService(_uow, new OutputQueryService(_uow), _audit, Substitute.For<INotificationService>(), _clock, _settings,
            NullLogger<KpiService>.Instance);
    }

    private IGenericRepository<T> SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
        return repo;
    }

    private static CustomerOrder Order(int id = 1, string code = "DH001") => new() { OrderId = id, OrderCode = code, Status = OrderStatuses.InProduction };

    private static Product Product(int id = 1, string code = "MH001", string name = "Áo thun") =>
        new() { ProductId = id, ProductCode = code, ProductName = name };

    private static Team SewingTeam(int id = 1, string name = "Tổ may 1", string status = RecordStatus.Active) =>
        new() { TeamId = id, TeamCode = "MAY1", TeamName = name, TeamType = TeamTypes.Sewing, Status = status };

    private static OrderLine Line(int orderId, int productId) =>
        new() { OrderLineId = orderId * 1000 + productId, OrderId = orderId, ProductId = productId, ProductVariant = TestVariant.Make(productId), OrderedQty = 1000 };

    private static KpiTarget Target(int id, int orderId, int productId, int teamId, DateOnly start, DateOnly end,
        int targetQty = 1000, short workingDays = 25, string status = KpiStatuses.Active, string? result = null,
        CustomerOrder? order = null, Product? product = null, Team? team = null) => new()
    {
        KpiTargetId = id, OrderId = orderId, ProductId = productId, SewingTeamId = teamId,
        StartDate = start, EndDate = end, TargetFinishedQty = targetQty, WorkingDays = workingDays,
        Status = status, Result = result, CreatedAt = new DateTime(2026, 10, 1),
        Order = order!, Product = product!, SewingTeam = team!,
    };

    private static SaveKpiTargetDto Dto(int orderId = 1, int productId = 1, int teamId = 1, DateOnly? start = null,
        DateOnly? end = null, int targetQty = 1000) =>
        new(orderId, productId, teamId, start ?? Start, end ?? End, targetQty);

    private void GivenSetup()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(SewingTeam());
        _orderLines.Add(Line(1, 1));
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_OrderNotFound_ReturnsNotFound()
    {
        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task CreateAsync_TeamNotSewing_ReturnsInvalidReference()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(new Team { TeamId = 1, TeamCode = "CAT1", TeamName = "Tổ cắt 1", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        _orderLines.Add(Line(1, 1));

        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidReference);
        result.Message.Should().Be(Messages.KpiTeamInvalid);
    }

    [Fact]
    public async Task CreateAsync_TeamInactive_ReturnsInvalidReference()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(SewingTeam(status: RecordStatus.Locked));
        _orderLines.Add(Line(1, 1));

        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidReference);
        result.Message.Should().Be(Messages.KpiTeamInvalid);
    }

    [Fact]
    public async Task CreateAsync_ClosedOrder_ReturnsInvalidReference()
    {
        _orders.Add(new CustomerOrder { OrderId = 1, OrderCode = "DH001", Status = OrderStatuses.Cancelled });
        _products.Add(Product());
        _teams.Add(SewingTeam());

        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidReference);
        result.Message.Should().Be(Messages.KpiOrderClosed);
    }

    [Fact]
    public async Task CreateAsync_ProductNotInOrder_ReturnsInvalidReference()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(SewingTeam());
        // không thêm OrderLine -> mã hàng chưa thuộc đơn này

        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidReference);
        result.Message.Should().Be(Messages.KpiProductNotInOrder);
    }

    [Theory]
    [InlineData("2026-10-31", "2026-11-15")]   // ngày bắt đầu trùng ngày kết thúc của chỉ tiêu cũ
    [InlineData("2026-09-15", "2026-10-01")]   // ngày kết thúc trùng ngày bắt đầu của chỉ tiêu cũ
    [InlineData("2026-10-10", "2026-10-20")]   // nằm trong kỳ cũ
    [InlineData("2026-09-01", "2026-11-30")]   // bao trùm kỳ cũ
    public async Task CreateAsync_PeriodSharingAnyDayWithSameOrderProductTeam_IsRejected(string start, string end)
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, order: _orders[0], product: _products[0], team: _teams[0]));

        var result = await _sut.CreateAsync(Dto(start: DateOnly.Parse(start), end: DateOnly.Parse(end), targetQty: 100), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiPeriodOverlap);
        result.Message.Should().Contain("01/10/2026").And.Contain("31/10/2026");
    }

    [Theory]
    [InlineData(KpiStatuses.Active)]
    [InlineData(KpiStatuses.Closed)]
    public async Task CreateAsync_NextPeriodOfSameOrderProductTeam_IsAllowed(string status)
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, status: status, order: _orders[0], product: _products[0], team: _teams[0]));

        var result = await _sut.CreateAsync(Dto(start: End.AddDays(1), end: End.AddDays(20), targetQty: 100), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatuses.Stopped)]
    [InlineData(OrderStatuses.Delivered)]
    [InlineData(OrderStatuses.Completed)]
    [InlineData(OrderStatuses.Cancelled)]
    public async Task ClosedOrder_RejectsCreateAndEdit_AndCapacityTellsTheForm(string status)
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, order: _orders[0], product: _products[0], team: _teams[0]));
        _orders[0].Status = status;

        (await _sut.CreateAsync(Dto(start: End.AddDays(1), end: End.AddDays(5), targetQty: 100), CancellationToken.None)).Message
            .Should().Be(Messages.KpiOrderClosed);
        (await _sut.UpdateAsync(1, Dto(targetQty: 200), CancellationToken.None)).Message.Should().Be(Messages.KpiOrderClosed);
        (await _sut.GetOrderCapacityAsync(1, 1, null, CancellationToken.None)).Data!.OrderClosed.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatuses.New)]
    [InlineData(OrderStatuses.InProduction)]
    [InlineData(OrderStatuses.InDelivery)]
    [InlineData(OrderStatuses.PartiallyDelivered)]
    public async Task RunningOrder_AllowsCreate(string status)
    {
        GivenSetup();
        _orders[0].Status = status;

        (await _sut.CreateAsync(Dto(targetQty: 100), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await _sut.GetOrderCapacityAsync(1, 1, null, CancellationToken.None)).Data!.OrderClosed.Should().BeFalse();
    }

    [Fact]
    public async Task OrderCapacity_OrderNotFound_ReturnsNotFound() =>
        (await _sut.GetOrderCapacityAsync(99, 1, null, CancellationToken.None)).Kind.Should().Be(ResultKind.NotFound);

    [Fact]
    public async Task OrderCapacity_ReturnsDueDateAndWarningDaysFromSettings()
    {
        GivenSetup();
        _orders[0].DueDate = new DateOnly(2026, 11, 20);
        _settings.GetIntAsync(SettingKeys.KpiDueWarningDays, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(5);

        var capacity = (await _sut.GetOrderCapacityAsync(1, 1, null, CancellationToken.None)).Data!;

        capacity.DueDate.Should().Be(new DateOnly(2026, 11, 20));
        capacity.DueWarningDays.Should().Be(5);
    }

    [Fact]
    public async Task CreateAsync_StartBeforeOrderReceived_IsRejected()
    {
        GivenSetup();
        _orders[0].ReceivedDate = Start.AddDays(1);

        var result = await _sut.CreateAsync(Dto(targetQty: 100), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiPeriodInvalid);
        result.Message.Should().Contain("02/10/2026");
    }

    [Fact]
    public async Task CreateAsync_StartInPast_IsRejected()
    {
        GivenSetup();

        var result = await _sut.CreateAsync(Dto(start: Today.AddDays(-1), targetQty: 100), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiPeriodInvalid);
        result.Message.Should().Be(Messages.KpiStartInPast);
    }

    [Fact]
    public async Task CreateAsync_EndAfterDueDate_IsOnlyAWarningAndStillSaves()
    {
        GivenSetup();
        _orders[0].DueDate = Start;

        (await _sut.CreateAsync(Dto(targetQty: 100), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_RunningTargetWithPastStart_CanChangeQtyButNotMoveDatesIntoThePast()
    {
        GivenSetup();
        _clock.Today.Returns(new DateOnly(2026, 10, 15));
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 500, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.UpdateAsync(1, Dto(targetQty: 600), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var moved = await _sut.UpdateAsync(1, Dto(end: new DateOnly(2026, 10, 14), targetQty: 600), CancellationToken.None);
        moved.Message.Should().Be(Messages.KpiEndInPast);

        var movedStart = await _sut.UpdateAsync(1, Dto(start: new DateOnly(2026, 10, 2), targetQty: 600), CancellationToken.None);
        movedStart.Message.Should().Be(Messages.KpiStartInPast);
    }

    [Fact]
    public async Task CreateAsync_SamePeriodOnAnotherTeam_IsAllowed()
    {
        GivenSetup();
        _teams.Add(SewingTeam(2, "Tổ may 2"));
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.CreateAsync(Dto(teamId: 2, targetQty: 100), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_AfterCancelled_CanCreateAgain()
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 5000, status: KpiStatuses.Cancelled, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.CreateAsync(Dto(targetQty: 100), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_SameTeamOnAnotherOrderOrProduct_IsNotADuplicate()
    {
        GivenSetup();
        _orders.Add(Order(2, "DH002")); _products.Add(Product(2, "MH002"));
        _orderLines.Add(Line(2, 2));
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 1000, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.CreateAsync(Dto(orderId: 2, productId: 2, targetQty: 1000), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_TargetAboveOrderQty_IsRejectedWithMaxQty()
    {
        GivenSetup();

        var result = await _sut.CreateAsync(Dto(targetQty: 1001), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiExceedsOrderQty);
        result.Message.Should().Be(string.Format(Messages.KpiExceedsOrderQty, 1000, 0, 1000));
        _kpiTargets.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_TargetsOfManyTeams_ShareTheOrderQty_WithoutCheckingBtp()
    {
        GivenSetup();
        _teams.Add(SewingTeam(2, "Tổ may 2"));
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 700, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.CreateAsync(Dto(teamId: 2, targetQty: 301), CancellationToken.None)).ErrorCode.Should().Be(ErrorCodes.KpiExceedsOrderQty);
        (await _sut.CreateAsync(Dto(teamId: 2, targetQty: 300), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task OrderCapacity_SumsAllLines_ClosedCountsActual_CancelledAndEditedDoNotCount()
    {
        GivenSetup();
        _orderLines.Add(new OrderLine { OrderLineId = 2, OrderId = 1, ProductId = 1, OrderedQty = 500 });
        _orderLines.Add(new OrderLine { OrderLineId = 3, OrderId = 1, ProductId = 9, OrderedQty = 7000 });   // mã hàng khác
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 400));
        _kpiTargets.Add(Target(2, 1, 1, 2, Start, End, targetQty: 600, status: KpiStatuses.Closed));
        _kpiTargets.Add(Target(3, 1, 1, 3, Start, End, targetQty: 900, status: KpiStatuses.Cancelled));
        _dailyResults.Add(DailyResult(2, Start, 250));

        (await _sut.GetOrderCapacityAsync(1, 1, null, CancellationToken.None)).Data.Should().Be(new KpiOrderCapacityDto(1500, 650, 850, default(DateOnly), 3, "InProduction", false));
        (await _sut.GetOrderCapacityAsync(1, 1, 1, CancellationToken.None)).Data!.MaxTarget.Should().Be(1250);
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesActiveTargetAndReturnsDto()
    {
        GivenSetup();

        var result = await _sut.CreateAsync(Dto(targetQty: 1000, start: new DateOnly(2026, 10, 1), end: new DateOnly(2026, 10, 27)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(nameof(KpiStatuses.Active));
        result.Data.Result.Should().BeNull();
        result.Data.WorkingDays.Should().Be(23);      // 27 ngày trừ 4 Chủ nhật
        result.Data.DailyTarget.Should().Be(43.48m);
        result.Data.OrderCode.Should().Be("DH001");
        result.Data.ProductCode.Should().Be("MH001");
        result.Data.SewingTeamName.Should().Be("Tổ may 1");
        _kpiTargets.Should().ContainSingle(k => k.Status == KpiStatuses.Active);
        await _uow.Received(2).SaveChangesAsync();   // lưu chỉ tiêu (lấy Id) rồi lưu AuditLog, cùng transaction
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(KpiTarget) && e.Action == AuditActions.Insert && e.OldValues == null));
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        var result = await _sut.UpdateAsync(99, Dto(), CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.KpiNotFound);
    }

    [Fact]
    public async Task UpdateAsync_NotActive_ReturnsInvalidStatus()
    {
        var order = Order();
        var product = Product();
        var team = SewingTeam();
        _orders.Add(order);
        _products.Add(product);
        _teams.Add(team);
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, status: KpiStatuses.Closed, order: order, product: product, team: team));

        var result = await _sut.UpdateAsync(1, Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        result.Message.Should().Be(Messages.KpiInvalidStatusForEdit);
    }

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesFieldsAndReturnsDto()
    {
        GivenSetup();
        var existing = Target(1, 1, 1, 1, Start, End, targetQty: 1000, workingDays: 25, order: _orders[0], product: _products[0], team: _teams[0]);
        _kpiTargets.Add(existing);

        var result = await _sut.UpdateAsync(1, Dto(targetQty: 900), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TargetFinishedQty.Should().Be(900);
        result.Data.WorkingDays.Should().Be(27);      // tháng 10/2026 có 4 Chủ nhật
        existing.TargetFinishedQty.Should().Be(900);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(KpiTarget) && e.Action == AuditActions.Update && e.RecordId == 1
            && e.OldValues != null && e.NewValues != null));
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnTriple_IsNotADuplicateAndItsOwnQtyIsFreed()
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 1000, order: _orders[0], product: _products[0], team: _teams[0]));

        (await _sut.UpdateAsync(1, Dto(targetQty: 1000), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ShorterPeriod_DropsDaysOutsideAndCountsKeptDays()
    {
        GivenSetup();
        var existing = Target(1, 1, 1, 1, Start, End, targetQty: 1000, order: _orders[0], product: _products[0], team: _teams[0]);
        _kpiTargets.Add(existing);
        _days.Add(new KpiDailyTarget { KpiTargetId = 1, WorkDate = new DateOnly(2026, 10, 5), TargetQty = 100 });
        _days.Add(new KpiDailyTarget { KpiTargetId = 1, WorkDate = new DateOnly(2026, 10, 25), TargetQty = 900 });

        var result = await _sut.UpdateAsync(1, Dto(end: new DateOnly(2026, 10, 20), targetQty: 500), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _days.Should().ContainSingle(d => d.WorkDate == new DateOnly(2026, 10, 5));
        existing.WorkingDays.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_PeriodOverlappingAnotherTargetOfSameTeam_IsRejected()
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, order: _orders[0], product: _products[0], team: _teams[0]));
        _kpiTargets.Add(Target(2, 1, 1, 1, End.AddDays(1), End.AddDays(10), targetQty: 100, order: _orders[0], product: _products[0], team: _teams[0]));

        var result = await _sut.UpdateAsync(1, Dto(end: End.AddDays(1), targetQty: 100), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiPeriodOverlap);
    }

    [Fact]
    public async Task UpdateAsync_ShorterPeriodDroppingLockedDay_IsRejected()
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 1000, order: _orders[0], product: _products[0], team: _teams[0]));
        _days.Add(new KpiDailyTarget { KpiTargetId = 1, WorkDate = new DateOnly(2026, 10, 25), TargetQty = 100 });
        _clock.Now.Returns(new DateTime(2026, 10, 25, 8, 0, 0));

        var result = await _sut.UpdateAsync(1, Dto(end: new DateOnly(2026, 10, 20)), CancellationToken.None);

        result.Message.Should().Be(string.Format(Messages.KpiDayLocked, new TimeOnly(8, 0)));
        _days.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAsync_TotalBelowDailyPlan_IsRejected()
    {
        GivenSetup();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 1000, order: _orders[0], product: _products[0], team: _teams[0]));
        _days.Add(new KpiDailyTarget { KpiTargetId = 1, WorkDate = new DateOnly(2026, 10, 5), TargetQty = 600 });

        var result = await _sut.UpdateAsync(1, Dto(targetQty: 599), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiPlanInvalid);
        result.Message.Should().Be(string.Format(Messages.KpiTotalBelowPlan, 600));
    }

    // ---------- CancelAsync ----------

    [Fact]
    public async Task CancelAsync_NotFound_ReturnsNotFound()
    {
        var result = await _sut.CancelAsync(99, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.KpiNotFound);
    }

    [Fact]
    public async Task CancelAsync_NotActive_ReturnsInvalidStatus()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, status: KpiStatuses.Closed));

        var result = await _sut.CancelAsync(1, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        result.Message.Should().Be(Messages.KpiInvalidStatusForCancel);
    }

    [Fact]
    public async Task CancelAsync_Active_SetsCancelled()
    {
        var target = Target(1, 1, 1, 1, Start, End);
        _kpiTargets.Add(target);

        var result = await _sut.CancelAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        target.Status.Should().Be(KpiStatuses.Cancelled);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(KpiTarget) && e.Action == AuditActions.Update && e.RecordId == 1));
    }

    [Fact]
    public async Task CancelAsync_NotActive_LogsNothing()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, status: KpiStatuses.Closed));

        await _sut.CancelAsync(1, CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNotFound()
    {
        var result = await _sut.GetByIdAsync(99, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.KpiNotFound);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ComputesDailyTarget()
    {
        var order = Order();
        var product = Product();
        var team = SewingTeam();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 300, workingDays: 20, order: order, product: product, team: team));

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.DailyTarget.Should().Be(15m);
    }

    // ---------- Thực tế vs chỉ tiêu (MI-17) ----------

    [Fact]
    public async Task GetByIdAsync_WithQcOutput_ReturnsActualAndPercentOfWholePeriodTarget()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 400, order: Order(), product: Product(), team: SewingTeam()));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 10, 5), 100));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 10, 6), 150));
        _dailyResults.Add(DailyResult(2, new DateOnly(2026, 10, 6), 999));   // chỉ tiêu khác: không lẫn vào

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result.Data!.ActualFinishedQty.Should().Be(250);
        result.Data.PercentAchieved.Should().Be(62.5m);   // 250 / 400
    }

    [Fact]
    public async Task GetByIdAsync_NoQcOutputYet_ActualAndPercentAreZero()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 400, order: Order(), product: Product(), team: SewingTeam()));

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result.Data!.ActualFinishedQty.Should().Be(0);
        result.Data.PercentAchieved.Should().Be(0m);
    }

    [Fact]
    public async Task GetPagedAsync_FillsActualPerTargetFromOneGroupedQuery()
    {
        var order = Order();
        var product = Product();
        var team = SewingTeam();
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, targetQty: 100, order: order, product: product, team: team));
        _kpiTargets.Add(Target(2, 1, 1, 1, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), targetQty: 200,
            order: order, product: product, team: team));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 10, 5), 120));    // vượt chỉ tiêu: > 100%
        _dailyResults.Add(DailyResult(2, new DateOnly(2026, 11, 5), 50));

        var result = await _sut.GetPagedAsync(new KpiTargetQuery(), CancellationToken.None);

        var byId = result.Data!.Items.ToDictionary(i => i.KpiTargetId);
        byId[1].ActualFinishedQty.Should().Be(120);
        byId[1].PercentAchieved.Should().Be(120m);
        byId[2].ActualFinishedQty.Should().Be(50);
        byId[2].PercentAchieved.Should().Be(25m);
    }

    private static VwKpiDailyResult DailyResult(int kpiTargetId, DateOnly workDate, int finishedQty) => new()
    {
        KpiTargetId = kpiTargetId, SewingTeamId = 1, OrderId = 1, ProductId = 1, WorkDate = workDate, FinishedQty = finishedQty,
    };

    // ---------- GetDailyResultsAsync (MI-18 "KPI theo ngày") ----------

    [Fact]
    public async Task GetDailyResultsAsync_KpiTargetNotFound_ReturnsNotFound()
    {
        var result = await _sut.GetDailyResultsAsync(99, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.KpiNotFound);
    }

    [Fact]
    public async Task GetDailyResultsAsync_NoRows_ReturnsEmptyList()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End));

        var result = await _sut.GetDailyResultsAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDailyResultsAsync_HasRows_ReturnsOrderedByWorkDate()
    {
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 10, 20), 50));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 10, 10), 40));
        _dailyResults.Add(DailyResult(2, new DateOnly(2026, 10, 15), 999)); // thuộc KpiTarget khác, phải bị loại

        var result = await _sut.GetDailyResultsAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].WorkDate.Should().Be(new DateOnly(2026, 10, 10));
        result.Data[0].FinishedQty.Should().Be(40);
        result.Data[1].WorkDate.Should().Be(new DateOnly(2026, 10, 20));
        result.Data[1].FinishedQty.Should().Be(50);
    }
}

public class SaveKpiTargetDtoValidatorTests
{
    private readonly SaveKpiTargetDtoValidator _validator = new();

    [Fact]
    public void Valid_HasNoError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 1000))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EndDateBeforeStartDate_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1), 1000))
            .ShouldHaveValidationErrorFor(x => x.EndDate);

    [Fact]
    public void NonPositiveTargetQty_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 0))
            .ShouldHaveValidationErrorFor(x => x.TargetFinishedQty);

    [Fact]
    public void MissingOrder_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(0, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 1000))
            .ShouldHaveValidationErrorFor(x => x.OrderId);
}
