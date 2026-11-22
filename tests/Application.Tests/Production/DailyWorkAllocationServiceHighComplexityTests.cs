using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID470–UTCID501: DailyWorkAllocationService high-complexity cases.</summary>
[Trait("Category", "HighComplexity")]
public class DailyWorkAllocationServiceHighComplexityTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 22);
    private const int TeamId = 3;
    private const int CuttingTeamId = 1;
    private const int OrderId = 1;
    private const int Line100 = 100;
    private const int Line101 = 101;
    private const int Line102 = 102;
    private const int WorkerW1 = 1;
    private const int Op10 = 10;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IPieceRateService _rates = Substitute.For<IPieceRateService>();

    private readonly List<Team> _teams = [];
    private readonly List<Employee> _employees = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<Operation> _operations = [];
    private readonly List<PieceRate> _ratesData = [];
    private readonly List<WorkAllocation> _allocations = [];
    private readonly List<VwOperationAllocationProgress> _progress = [];
    private readonly List<OutputRecord> _outputs = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<OrderLineTeam> _allotted = [];
    private readonly List<OperationComponent> _components = [];

    public DailyWorkAllocationServiceHighComplexityTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _uow.SaveChangesAsync().Returns(1);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var order = new CustomerOrder
        {
            OrderId = OrderId, OrderCode = "DH2611-001", Status = OrderStatuses.InProduction,
            ReceivedDate = new DateOnly(2026, 11, 10),
        };
        _teams.Add(new Team { TeamId = TeamId, TeamCode = "MAY3", TeamType = TeamTypes.Sewing });
        _teams.Add(new Team { TeamId = CuttingTeamId, TeamCode = "CAT1", TeamType = TeamTypes.Cutting });
        _employees.Add(new Employee
        {
            EmployeeId = WorkerW1, EmployeeCode = "W1", FullName = "Worker 1", TeamId = TeamId,
            Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker },
        });
        foreach (var lineId in new[] { Line100, Line101, Line102 })
        {
            _orderLines.Add(new OrderLine
            {
                OrderLineId = lineId, OrderId = OrderId, Order = order, ProductId = 1,
                ProductVariant = TestVariant.Make(new Product { ProductId = 1, ProductCode = "AO-01" }, "Đỏ", "M"),
            });
        }
        _allotted.Add(new OrderLineTeam { OrderLineId = Line100, SewingTeamId = TeamId });
        _allotted.Add(new OrderLineTeam { OrderLineId = Line101, SewingTeamId = TeamId });
        _operations.Add(new Operation
        {
            OperationId = Op10, ProductId = 1, OperationCode = "OP10", OperationName = "May",
            StationSequence = 10, Status = RecordStatus.Active, TeamType = TeamTypes.Sewing,
        });
        _ratesData.Add(TestRates.Approved(Op10));
        _progress.Add(new VwOperationAllocationProgress { SewingTeamId = TeamId, OrderLineId = Line100, OperationId = Op10, RequiredQty = 100, UnallocatedQty = 50 });
        _progress.Add(new VwOperationAllocationProgress { SewingTeamId = TeamId, OrderLineId = Line101, OperationId = Op10, RequiredQty = 100, UnallocatedQty = 50 });
        _assignments.Add(new OperationAssignment { SewingTeamId = TeamId, OrderId = OrderId, OperationId = Op10, WorkerId = WorkerW1, ValidTo = null });
        Setup(_teams); Setup(_employees); Setup(_orderLines); Setup(_operations); Setup(_ratesData);
        Setup(_allocations); Setup(_progress); Setup(_outputs); Setup(_assignments); Setup(_allotted); Setup(_components);

        var rateSvc = new PieceRateService(_uow, _audit, _notifier, _clock);
        _rates.FindEffectiveManyAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => rateSvc.FindEffectiveManyAsync(ci.ArgAt<IReadOnlyList<int>>(0), ci.ArgAt<DateOnly>(1), ci.ArgAt<CancellationToken>(2)));
        _rates.FindEffectiveAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => rateSvc.FindEffectiveAsync(ci.ArgAt<int>(0), ci.ArgAt<DateOnly>(1), ci.ArgAt<CancellationToken>(2)));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private DailyWorkAllocationService Sut() => new(_uow, _notifier, _locks, _clock, _rates);

    private void ConfirmedHandover(int qty, DateOnly day, int worker = WorkerW1, int line = Line100) =>
        _allocations.Add(new WorkAllocation
        {
            WorkAllocationId = _allocations.Count + 1, WorkDate = day, MovementType = PieceMovementTypes.Handover,
            SewingTeamId = TeamId, WorkerId = worker, OrderLineId = line, OperationId = Op10,
            Quantity = qty, ConfirmedQty = qty, Status = HandoverStatuses.Confirmed, ConfirmMode = ConfirmModes.Worker, CreatedAt = Now,
        });

    private void PendingHandover(int qty, DateOnly day) => _allocations.Add(new WorkAllocation
    {
        WorkAllocationId = _allocations.Count + 1, WorkDate = day, MovementType = PieceMovementTypes.Handover,
        SewingTeamId = TeamId, WorkerId = WorkerW1, OrderLineId = Line100, OperationId = Op10,
        Quantity = qty, Status = HandoverStatuses.Pending, ConfirmMode = ConfirmModes.Worker, CreatedAt = Now,
    });

    private void Output(int qty, DateOnly day, int worker = WorkerW1, int line = Line100) =>
        _outputs.Add(new OutputRecord { WorkDate = day, SewingTeamId = TeamId, WorkerId = worker, OrderLineId = line, OperationId = Op10, Quantity = qty });

    private static SaveDailyAllocationDto Save(int qty, string? reason = null, DateOnly? day = null,
        int worker = WorkerW1, int line = Line100) =>
        new(day ?? Today, [new SaveDailyAllocationItem(worker, line, Op10, qty)], reason);

    [Fact]
    public async Task UTCID470_GetBoardAsync_ComputesRemaining()
    {
        // Arrange
        ConfirmedHandover(20, Today.AddDays(-1));
        ConfirmedHandover(30, Today);
        Output(25, Today);
        ConfirmedHandover(60, Today.AddDays(-10));
        Output(60, Today.AddDays(-10));

        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, OrderId, Line100, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var row = result.Data!.Rows.Should().ContainSingle().Subject;
        row.RemainingQty.Should().Be(25);
        row.UnallocatedQty.Should().Be(50);
        row.RateApproved.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID471_GetBoardAsync_NullLineId_AllottedLinesOnly()
    {
        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, OrderId, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Select(r => r.OrderLineId).Distinct().Should().BeEquivalentTo([Line100, Line101]);
        result.Data.Rows.Should().NotContain(r => r.OrderLineId == Line102);
    }

    [Fact]
    public async Task UTCID472_GetBoardAsync_NegativeNet_FloorsRemainingToZero()
    {
        // Arrange
        ConfirmedHandover(10, Today);
        Output(20, Today);

        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, OrderId, Line100, default);

        // Assert
        result.Data!.Rows.Should().ContainSingle().Which.RemainingQty.Should().Be(0);
    }

    [Fact]
    public async Task UTCID473_GetBoardAsync_NoAssignment_EmptyRows()
    {
        // Arrange
        _assignments.Clear();
        _allocations.Clear();

        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, OrderId, Line100, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID474_GetBoardAsync_PastDate_SuccessViewOnly()
    {
        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today.AddDays(-1), OrderId, Line100, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID475_GetBoardAsync_NullConfirmedQty_SafeRemaining()
    {
        // Arrange
        _allocations.Add(new WorkAllocation
        {
            WorkDate = Today, MovementType = PieceMovementTypes.Handover, SewingTeamId = TeamId,
            WorkerId = WorkerW1, OrderLineId = Line100, OperationId = Op10, Quantity = 30,
            ConfirmedQty = null, Status = HandoverStatuses.Confirmed, ConfirmMode = ConfirmModes.Leader,
        });

        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, OrderId, Line100, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().ContainSingle().Which.RemainingQty.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task UTCID476_GetBoardAsync_UnknownTeam_Fails()
    {
        // Act
        var result = await Sut().GetBoardAsync(999, Today, OrderId, Line100, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID477_GetBoardAsync_UnknownOrder_FailsOrEmpty()
    {
        // Act
        var result = await Sut().GetBoardAsync(TeamId, Today, 999, Line100, default);

        // Assert
        result.IsSuccess.Should().BeFalse("unknown order should be validated as NotFound per business spec");
    }

    [Fact]
    public async Task UTCID478_GetBoardAsync_MissingOperationInSort_FailsOrSkips()
    {
        // Arrange
        ConfirmedHandover(10, Today);
        _operations.Add(new Operation { OperationId = 777, ProductId = 1, OperationCode = "X", StationSequence = 5, Status = RecordStatus.Active, TeamType = TeamTypes.Sewing });
        _assignments.Add(new OperationAssignment { SewingTeamId = TeamId, OrderId = OrderId, OperationId = 777, WorkerId = WorkerW1 });

        // Act
        Func<Task> act = async () => await Sut().GetBoardAsync(TeamId, Today, OrderId, Line100, default);

        // Assert
        await act.Should().NotThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UTCID479_SaveAsync_GiveMore_CreatesPending_LocksNotifier()
    {
        // Arrange
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(30), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Added.Should().Be(1);
        await _locks.Received(1).AcquireAsync($"work-allocation:{TeamId}:{Line100}", Arg.Any<CancellationToken>());
        _notifier.Received(1).NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>());
        await _uow.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID480_SaveAsync_LowerPending_First()
    {
        // Arrange
        PendingHandover(30, Today);
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(20, "Đổi việc"), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _allocations.Single(a => a.Status == HandoverStatuses.Pending).Quantity.Should().Be(20);
    }

    [Fact]
    public async Task UTCID481_SaveAsync_LowerMixedPendingAndConfirmed()
    {
        // Arrange
        ConfirmedHandover(20, Today);
        PendingHandover(10, Today);
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(15, "Điều chuyển"), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Adjusted.Should().Be(1);
    }

    [Fact]
    public async Task UTCID482_SaveAsync_GiveExactUnallocated_Success()
    {
        // Arrange
        _progress[0].UnallocatedQty = 50;
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(50), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Added.Should().Be(1);
    }

    [Fact]
    public async Task UTCID483_SaveAsync_OverUnallocated_Fails()
    {
        // Arrange
        _progress[0].UnallocatedQty = 50;
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(51), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AllocationExceedsRequired);
    }

    [Fact]
    public async Task UTCID484_SaveAsync_NoDelta_NoDbWrite()
    {
        // Arrange
        ConfirmedHandover(30, Today);

        // Act
        var result = await Sut().SaveAsync(Save(30), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Added.Should().Be(0);
        result.Data.Adjusted.Should().Be(0);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID485_SaveAsync_Yesterday_Fails()
    {
        // Act
        var result = await Sut().SaveAsync(Save(10, day: Today.AddDays(-1)), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.DailyDateMustBeToday);
    }

    [Fact]
    public async Task UTCID486_SaveAsync_Tomorrow_Fails()
    {
        // Act
        var result = await Sut().SaveAsync(Save(10, day: Today.AddDays(1)), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID487_SaveAsync_EmptyItems_NoLock()
    {
        // Act
        var result = await Sut().SaveAsync(new SaveDailyAllocationDto(Today, [], null), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _locks.DidNotReceive().AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID488_SaveAsync_ZeroQty_CancelsPendingOnly()
    {
        // Arrange
        PendingHandover(10, Today);
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(0, "x"), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _allocations.Should().NotContain(a => a.MovementType == PieceMovementTypes.Retrieval);
    }

    [Fact]
    public async Task UTCID489_SaveAsync_NegativeQty_FailsValidation()
    {
        // Act
        var result = await Sut().SaveAsync(Save(-1), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task UTCID490_SaveAsync_DuplicateKeys_Fails()
    {
        // Arrange
        var dto = new SaveDailyAllocationDto(Today, [new SaveDailyAllocationItem(WorkerW1, Line100, Op10, 10), new(WorkerW1, Line100, Op10, 20)], null);

        // Act
        var result = await Sut().SaveAsync(dto, TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID491_SaveAsync_LowerWithoutReason_Fails()
    {
        // Arrange
        ConfirmedHandover(30, Today);

        // Act
        var result = await Sut().SaveAsync(Save(20), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.DailyReasonRequired);
    }

    [Fact]
    public async Task UTCID492_SaveAsync_LowerConfirmedBeyondRemaining_Fails()
    {
        // Arrange
        ConfirmedHandover(30, Today);
        Output(25, Today);

        // Act
        var result = await Sut().SaveAsync(Save(20, "x"), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
    }

    [Fact]
    public async Task UTCID493_SaveAsync_WorkerNotInTeam_Fails()
    {
        // Arrange
        _employees.Add(new Employee { EmployeeId = 9, TeamId = 5, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker } });
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));

        // Act
        var result = await Sut().SaveAsync(Save(10, worker: 9), TeamId, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task UTCID494_SaveAsync_LineNotAllotted_Fails()
    {
        // Act
        var result = await Sut().SaveAsync(Save(10, line: Line102), TeamId, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
    }

    [Fact]
    public async Task UTCID495_SaveAsync_OrderClosed_Fails()
    {
        // Arrange
        _orderLines.First(l => l.OrderLineId == Line100).Order.Status = OrderStatuses.Stopped;

        // Act
        var result = await Sut().SaveAsync(Save(10), TeamId, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
    }

    [Fact]
    public async Task UTCID496_SaveAsync_CuttingTeam_Fails()
    {
        // Act
        var result = await Sut().SaveAsync(Save(10), CuttingTeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID497_SaveAsync_UnknownTeam_Fails()
    {
        // Act
        var result = await Sut().SaveAsync(Save(10), 999, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID498_SaveAsync_NoApprovedRate_Fails()
    {
        // Arrange
        _ratesData.Clear();
        _rates.FindEffectiveManyAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, EffectivePieceRate>());
        _rates.FindEffectiveAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((EffectivePieceRate?)null);

        // Act
        var result = await Sut().SaveAsync(Save(10), TeamId, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
    }

    [Fact]
    public async Task UTCID499_SaveAsync_ZeroQtyWithoutRate_Success()
    {
        // Arrange
        ConfirmedHandover(10, Today);
        _ratesData.Clear();

        // Act
        var result = await Sut().SaveAsync(Save(0, "x"), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID500_SaveAsync_LockTimeout_Fails()
    {
        // Arrange
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("busy"));

        // Act
        var result = await Sut().SaveAsync(Save(10), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID501_SaveAsync_NotifierThrows_StillCommits()
    {
        // Arrange
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(a => _allocations.Add(a)));
        _notifier.When(n => n.NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>()))
            .Do(_ => throw new InvalidOperationException("notify"));

        // Act
        var result = await Sut().SaveAsync(Save(10), TeamId, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _uow.Received().SaveChangesAsync();
    }
}