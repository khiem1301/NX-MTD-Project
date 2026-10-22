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

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly List<KpiTarget> _kpiTargets = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Product> _products = [];
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<VwKpiDailyResult> _dailyResults = [];
    private readonly KpiService _sut;

    public KpiServiceTests()
    {
        SetupQueryable(_kpiTargets);
        SetupQueryable(_orders);
        SetupQueryable(_products);
        SetupQueryable(_teams);
        SetupQueryable(_orderLines);
        SetupQueryable(_dailyResults);

        // Giả lập EF gán navigation (Order/Product/SewingTeam) sau khi lưu, để Select chiếu DTO không bị null.
        _uow.Repository<KpiTarget>().AddAsync(Arg.Do<KpiTarget>(e =>
        {
            e.Order = _orders.First(o => o.OrderId == e.OrderId);
            e.Product = _products.First(p => p.ProductId == e.ProductId);
            e.SewingTeam = _teams.First(t => t.TeamId == e.SewingTeamId);
            _kpiTargets.Add(e);
        })).Returns(Task.CompletedTask);

        _uow.SaveChangesAsync().Returns(Task.FromResult(1));

        _sut = new KpiService(_uow, new OutputQueryService(_uow), _audit, NullLogger<KpiService>.Instance);
    }

    private void SetupQueryable<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private static CustomerOrder Order(int id = 1, string code = "DH001") => new() { OrderId = id, OrderCode = code };

    private static Product Product(int id = 1, string code = "MH001", string name = "Áo thun") =>
        new() { ProductId = id, ProductCode = code, ProductName = name };

    private static Team SewingTeam(int id = 1, string name = "Tổ may 1", string status = RecordStatus.Active) =>
        new() { TeamId = id, TeamCode = "MAY1", TeamName = name, TeamType = TeamTypes.Sewing, Status = status };

    private static OrderLine Line(int orderId, int productId) =>
        new() { OrderLineId = orderId * 1000 + productId, OrderId = orderId, ProductId = productId, Color = "Đen", SizeCode = "M", OrderedQty = 1000 };

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
        DateOnly? end = null, int targetQty = 1000, short workingDays = 25) =>
        new(orderId, productId, teamId, start ?? Start, end ?? End, targetQty, workingDays);

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

    [Fact]
    public async Task CreateAsync_DuplicatePeriod_ReturnsDuplicateFailure()
    {
        var order = Order();
        var product = Product();
        var team = SewingTeam();
        _orders.Add(order);
        _products.Add(product);
        _teams.Add(team);
        _orderLines.Add(Line(1, 1));
        _kpiTargets.Add(Target(1, 1, 1, 1, Start, End, order: order, product: product, team: team));

        var result = await _sut.CreateAsync(Dto(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.KpiDuplicatePeriod);
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesActiveTargetAndReturnsDto()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(SewingTeam());
        _orderLines.Add(Line(1, 1));

        var result = await _sut.CreateAsync(Dto(targetQty: 1000, workingDays: 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(nameof(KpiStatuses.Active));
        result.Data.Result.Should().BeNull();
        result.Data.DailyTarget.Should().Be(40m);
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
        var order = Order();
        var product = Product();
        var team = SewingTeam();
        _orders.Add(order);
        _products.Add(product);
        _teams.Add(team);
        _orderLines.Add(Line(1, 1));
        var existing = Target(1, 1, 1, 1, Start, End, targetQty: 1000, workingDays: 25, order: order, product: product, team: team);
        _kpiTargets.Add(existing);

        var result = await _sut.UpdateAsync(1, Dto(targetQty: 1200, workingDays: 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TargetFinishedQty.Should().Be(1200);
        result.Data.WorkingDays.Should().Be(30);
        existing.TargetFinishedQty.Should().Be(1200);
        existing.WorkingDays.Should().Be(30);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(KpiTarget) && e.Action == AuditActions.Update && e.RecordId == 1
            && e.OldValues != null && e.NewValues != null));
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

    // ---------- CloseExpiredAsync (KpiAutoCloseJob) ----------

    [Fact]
    public async Task CloseExpiredAsync_NoExpired_ReturnsZero()
    {
        var target = Target(1, 1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));
        _kpiTargets.Add(target);

        var closed = await _sut.CloseExpiredAsync(new DateOnly(2026, 10, 21), CancellationToken.None);

        closed.Should().Be(0);
        target.Status.Should().Be(KpiStatuses.Active);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CloseExpiredAsync_TotalMeetsTarget_SetsAchievedAndClosed()
    {
        var target = Target(1, 1, 1, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), targetQty: 500, workingDays: 25);
        _kpiTargets.Add(target);
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 9, 10), 300));
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 9, 20), 250));

        var closed = await _sut.CloseExpiredAsync(new DateOnly(2026, 10, 21), CancellationToken.None);

        closed.Should().Be(1);
        target.Status.Should().Be(KpiStatuses.Closed);
        target.Result.Should().Be(KpiResults.Achieved);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(KpiTarget) && e.Action == AuditActions.Update && e.RecordId == 1));
    }

    [Fact]
    public async Task CloseExpiredAsync_TotalBelowTarget_SetsNotAchievedAndClosed()
    {
        var target = Target(1, 1, 1, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), targetQty: 500, workingDays: 25);
        _kpiTargets.Add(target);
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 9, 10), 100));

        var closed = await _sut.CloseExpiredAsync(new DateOnly(2026, 10, 21), CancellationToken.None);

        closed.Should().Be(1);
        target.Status.Should().Be(KpiStatuses.Closed);
        target.Result.Should().Be(KpiResults.NotAchieved);
    }

    [Fact]
    public async Task CloseExpiredAsync_NoMatchingViewRows_TreatsAsZeroNotAchieved()
    {
        var target = Target(1, 1, 1, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), targetQty: 500, workingDays: 25);
        _kpiTargets.Add(target);
        // không có dòng vw_KpiDailyResult nào (không QC ngày nào trong kỳ)

        var closed = await _sut.CloseExpiredAsync(new DateOnly(2026, 10, 21), CancellationToken.None);

        closed.Should().Be(1);
        target.Result.Should().Be(KpiResults.NotAchieved);
    }

    [Fact]
    public async Task CloseExpiredAsync_MultipleExpiredTargets_ClosesAllIndependently()
    {
        var achieved = Target(1, 1, 1, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), targetQty: 100, workingDays: 10);
        var notAchieved = Target(2, 2, 2, 2, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), targetQty: 100, workingDays: 10);
        _kpiTargets.Add(achieved);
        _kpiTargets.Add(notAchieved);
        _dailyResults.Add(DailyResult(1, new DateOnly(2026, 9, 10), 150));

        var closed = await _sut.CloseExpiredAsync(new DateOnly(2026, 10, 21), CancellationToken.None);

        closed.Should().Be(2);
        achieved.Result.Should().Be(KpiResults.Achieved);
        notAchieved.Result.Should().Be(KpiResults.NotAchieved);
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
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 1000, 25))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EndDateBeforeStartDate_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1), 1000, 25))
            .ShouldHaveValidationErrorFor(x => x.EndDate);

    [Fact]
    public void NonPositiveTargetQty_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 0, 25))
            .ShouldHaveValidationErrorFor(x => x.TargetFinishedQty);

    [Fact]
    public void NonPositiveWorkingDays_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(1, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 1000, 0))
            .ShouldHaveValidationErrorFor(x => x.WorkingDays);

    [Fact]
    public void MissingOrder_HasError() =>
        _validator.TestValidate(new SaveKpiTargetDto(0, 1, 1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 1000, 25))
            .ShouldHaveValidationErrorFor(x => x.OrderId);
}
