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
    private readonly List<CutHandoverLine> _handoverLines = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<QcBatch> _qcBatches = [];
    private readonly List<DefectRecord> _defects = [];
    private readonly KpiService _sut;

    public KpiServiceTests()
    {
        SetupQueryable(_kpiTargets);
        SetupQueryable(_orders);
        SetupQueryable(_products);
        SetupQueryable(_teams);
        SetupQueryable(_orderLines);
        SetupQueryable(_dailyResults);
        SetupQueryable(_handoverLines);
        SetupQueryable(_issueLines);
        SetupQueryable(_qcBatches);
        SetupQueryable(_defects);

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

    /// <summary>BTP (bộ đủ) tổ cắt đã bàn giao và kho đã xác nhận cho đơn + mã hàng; mặc định đủ lớn để các test khác không bị chặn.</summary>
    private void GivenBtp(int orderId, int productId, int qty) => _handoverLines.Add(new CutHandoverLine
    {
        ConfirmedQty = qty, ProductComponentMaterialId = null,
        CutHandover = new CutHandover { Status = HandoverStatuses.Confirmed },
        OrderLine = new OrderLine { OrderId = orderId, ProductId = productId },
    });

    private static CustomerOrder Order(int id = 1, string code = "DH001") => new() { OrderId = id, OrderCode = code };

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
        GivenBtp(1, 1, 1000);

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
    public async Task CreateAsync_TargetAboveBtpInWarehouse_IsRejectedWithRemainingQty()
    {
        _orders.Add(Order());
        _products.Add(Product());
        _teams.Add(SewingTeam());
        _orderLines.Add(Line(1, 1));
        GivenBtp(1, 1, 600);

        var result = await _sut.CreateAsync(Dto(targetQty: 700, workingDays: 25), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.KpiExceedsStock);
        result.Message.Should().Contain("kho còn 600").And.Contain("tối đa 600");
        _kpiTargets.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_OtherActiveTargetsOfSameOrderAndProduct_CountAgainstTheBtp()
    {
        var order = Order(); var product = Product(); var team = SewingTeam();
        _orders.Add(order); _products.Add(product); _teams.Add(team);
        _orderLines.Add(Line(1, 1));
        GivenBtp(1, 1, 1000);
        _kpiTargets.Add(Target(9, 1, 1, 1, Start.AddMonths(-1), End.AddMonths(-1), targetQty: 700, order: order, product: product, team: team));
        _kpiTargets.Add(Target(8, 1, 1, 1, Start.AddMonths(-2), End.AddMonths(-2), targetQty: 5000, status: KpiStatuses.Cancelled,
            order: order, product: product, team: team));   // đã hủy: không tính

        (await _sut.CreateAsync(Dto(targetQty: 400, workingDays: 25), CancellationToken.None)).ErrorCode.Should().Be(ErrorCodes.KpiExceedsStock);
        (await _sut.CreateAsync(Dto(targetQty: 300, workingDays: 25), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    private MaterialIssueLine IssueToTeam(int teamId, int qty, string status = IssueStatuses.Received) => new()
    {
        IssuedQty = qty, ConfirmedQty = status == IssueStatuses.Received ? qty : null, ProductComponentMaterialId = null,
        OrderLineId = 1001, OrderLine = new OrderLine { OrderId = 1, ProductId = 1 },
        MaterialIssue = new MaterialIssue { WarehouseType = WarehouseTypes.SemiFinished, Status = status, ReceivingTeamId = teamId },
    };

    [Fact]
    public async Task Capacity_BtpAlreadyIssuedToAnotherTeam_NoLongerCountsAsInTheWarehouse()
    {
        _orders.Add(Order()); _products.Add(Product()); _teams.Add(SewingTeam()); _teams.Add(SewingTeam(2, "Tổ may 2"));
        _orderLines.Add(Line(1, 1));
        GivenBtp(1, 1, 600);
        _issueLines.Add(IssueToTeam(2, 550));          // đã cấp hết cho tổ may 2 → kho chỉ còn 50

        var capacity = (await _sut.GetCapacityAsync(1, 1, 1, null, CancellationToken.None)).Data!;

        capacity.WarehouseBtp.Should().Be(50);
        capacity.TeamHeld.Should().Be(0);
        capacity.MaxTarget.Should().Be(50);
        (await _sut.CreateAsync(Dto(teamId: 1, targetQty: 51, workingDays: 25), CancellationToken.None)).ErrorCode
            .Should().Be(ErrorCodes.KpiExceedsStock);
    }

    [Fact]
    public async Task Capacity_AddsWhatTheTeamHoldsAndHasNotFinished()
    {
        _orders.Add(Order()); _products.Add(Product()); _teams.Add(SewingTeam());
        _orderLines.Add(Line(1, 1));
        GivenBtp(1, 1, 400);
        _issueLines.Add(IssueToTeam(1, 300));          // tổ nhận 300, kho còn 100
        _qcBatches.Add(new QcBatch { SewingTeamId = 1, FinishedQty = 120, OrderLine = new OrderLine { OrderId = 1, ProductId = 1 } });
        _defects.Add(new DefectRecord
        {
            SewingTeamId = 1, DefectType = DefectTypes.Scrap, Status = DefectStatuses.Recorded, Quantity = 10,
            OrderLine = new OrderLine { OrderId = 1, ProductId = 1 },
        });

        var capacity = (await _sut.GetCapacityAsync(1, 1, 1, null, CancellationToken.None)).Data!;

        capacity.TeamHeld.Should().Be(170);            // 300 − 120 đạt − 10 hủy
        capacity.WarehouseBtp.Should().Be(100);
        capacity.MaxTarget.Should().Be(270);
    }

    [Fact]
    public async Task Capacity_SubtractsTheRemainingOfOtherActiveTargets_ButNotTheOneBeingEdited()
    {
        var order = Order(); var product = Product(); var team = SewingTeam();
        _orders.Add(order); _products.Add(product); _teams.Add(team);
        _orderLines.Add(Line(1, 1));
        GivenBtp(1, 1, 1000);
        _kpiTargets.Add(Target(9, 1, 1, 1, Start.AddMonths(-1), End.AddMonths(-1), targetQty: 300, order: order, product: product, team: team));

        (await _sut.GetCapacityAsync(1, 1, 1, null, CancellationToken.None)).Data!.MaxTarget.Should().Be(700);
        (await _sut.GetCapacityAsync(1, 1, 1, 9, CancellationToken.None)).Data!.MaxTarget.Should().Be(1000);
    }

    [Fact]
    public async Task CreateAsync_PartialComponentsAndUnconfirmedHandovers_DoNotCountAsBtp()
    {
        _orders.Add(Order()); _products.Add(Product()); _teams.Add(SewingTeam());
        _orderLines.Add(Line(1, 1));
        _handoverLines.Add(new CutHandoverLine   // chi tiết lẻ (cắt bù) không phải bộ đủ
        {
            ConfirmedQty = 500, ProductComponentMaterialId = 7, CutHandover = new CutHandover { Status = HandoverStatuses.Confirmed },
            OrderLine = new OrderLine { OrderId = 1, ProductId = 1 },
        });
        _handoverLines.Add(new CutHandoverLine   // phiếu còn chờ kho nhận
        {
            ConfirmedQty = 500, ProductComponentMaterialId = null, CutHandover = new CutHandover { Status = HandoverStatuses.Pending },
            OrderLine = new OrderLine { OrderId = 1, ProductId = 1 },
        });

        (await _sut.CreateAsync(Dto(targetQty: 1, workingDays: 25), CancellationToken.None)).ErrorCode.Should().Be(ErrorCodes.KpiExceedsStock);
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
        GivenBtp(1, 1, 1500);
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
