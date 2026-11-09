using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-63 / DEV-ALL-64: giao sản lượng công đoạn theo ngày, hạ số giao, phần chưa làm hết chuyển sang hôm sau.</summary>
public class DailyWorkAllocationServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 5);
    private const int TeamId = 5;
    private const int WorkerId = 40;
    private const int OpId = 9;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Team> _teams = new();
    private readonly List<Employee> _employees = new();
    private readonly List<OrderLine> _orderLines = new();
    private readonly List<Operation> _operations = new();
    private readonly List<PieceRate> _rates = new();
    private readonly List<WorkAllocation> _allocations = new();
    private readonly List<VwOperationAllocationProgress> _progress = new();
    private readonly List<OutputRecord> _outputs = new();
    private readonly List<OperationAssignment> _assignments = new();
    private readonly List<OrderLineTeam> _allotted = new();

    public DailyWorkAllocationServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        var order = new CustomerOrder { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction, ReceivedDate = new DateOnly(2026, 10, 1) };
        _teams.Add(new Team { TeamId = TeamId, TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing });
        _employees.Add(new Employee
        {
            EmployeeId = WorkerId, EmployeeCode = "CNMAY1", FullName = "Công nhân 1", TeamId = TeamId,
            Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker },
        });
        _orderLines.Add(new OrderLine
        {
            OrderLineId = 101, OrderId = 7, Order = order, ProductId = 3,
            ProductVariant = TestVariant.Make(new Product { ProductId = 3, ProductCode = "AO-01" }, "Đen", "M"),
        });
        _operations.Add(new Operation
        {
            OperationId = OpId, ProductId = 3, OperationCode = "OP09", OperationName = "Tra tay", StationSequence = 9,
            Status = RecordStatus.Active, TeamType = TeamTypes.Sewing,
        });
        _rates.Add(TestRates.Approved(OpId));
        _progress.Add(new VwOperationAllocationProgress
        {
            SewingTeamId = TeamId, OrderLineId = 101, OperationId = OpId, RequiredQty = 100, UnallocatedQty = 100,
        });
        _allotted.Add(new OrderLineTeam { OrderLineId = 101, SewingTeamId = TeamId });
        Setup(_teams); Setup(_employees); Setup(_orderLines); Setup(_operations); Setup(_rates); Setup(_allocations);
        Setup(_progress); Setup(_outputs); Setup(_assignments); Setup(_allotted);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private DailyWorkAllocationService Service() => new(_uow, _notifier, _locks, _clock);

    private List<WorkAllocation> CaptureAdds()
    {
        var added = new List<WorkAllocation>();
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(m =>
        {
            added.Add(m); _allocations.Add(m);
        }));
        return added;
    }

    private void Held(string type, int qty, DateOnly day) => _allocations.Add(new WorkAllocation
    {
        WorkAllocationId = _allocations.Count + 1, WorkDate = day, MovementType = type, SewingTeamId = TeamId, WorkerId = WorkerId,
        OrderLineId = 101, OperationId = OpId, Quantity = qty, ConfirmedQty = qty, Status = HandoverStatuses.Confirmed,
        ConfirmMode = ConfirmModes.Leader, CreatedAt = Now,
        RetrievalReason = type == PieceMovementTypes.Retrieval ? RetrievalReasons.Adjusted : null,
    });

    private void Output(int qty, DateOnly day) => _outputs.Add(new OutputRecord
    {
        WorkDate = day, SewingTeamId = TeamId, WorkerId = WorkerId, OrderLineId = 101, OperationId = OpId, Quantity = qty,
    });

    private static SaveDailyAllocationDto Save(int qty, string? reason = null) =>
        new(Today, new[] { new SaveDailyAllocationItem(WorkerId, 101, OpId, qty) }, reason);

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    [InlineData(-1500)]
    [InlineData(1)]
    public async Task Save_only_accepts_today_and_past_days_are_view_only(int dayOffset)
    {
        CaptureAdds();
        var dto = new SaveDailyAllocationDto(Today.AddDays(dayOffset),
            new[] { new SaveDailyAllocationItem(WorkerId, 101, OpId, 60) }, null);

        var result = await Service().SaveAsync(dto, TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.DailyDateMustBeToday);
    }

    [Fact]
    public async Task Board_of_a_past_day_can_still_be_viewed()
    {
        var result = await Service().GetBoardAsync(TeamId, Today.AddDays(-3), 7, null, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_is_blocked_when_today_is_before_the_order_was_received()
    {
        CaptureAdds();
        _orderLines.Single().Order.ReceivedDate = Today.AddDays(3);

        var result = await Service().SaveAsync(Save(60), TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(Today.ToString("dd/MM/yyyy"));
    }


    [Fact]
    public async Task Save_records_a_confirmed_handover_for_the_day()
    {
        var added = CaptureAdds();

        var result = await Service().SaveAsync(Save(60), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Added.Should().Be(1);
        var m = added.Should().ContainSingle().Subject;
        (m.WorkDate, m.MovementType, m.Quantity, m.ConfirmedQty).Should().Be((Today, PieceMovementTypes.Handover, 60, 60));
        (m.Status, m.ConfirmMode).Should().Be((HandoverStatuses.Confirmed, ConfirmModes.Leader));
    }

    [Fact]
    public async Task Save_is_blocked_while_the_operation_has_no_approved_rate()
    {
        _rates.Clear();
        CaptureAdds();

        var result = await Service().SaveAsync(Save(60), TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        result.Message.Should().Contain("OP09");
    }

    [Fact]
    public async Task Save_only_adds_the_difference_to_what_is_already_given_that_day()
    {
        Held(PieceMovementTypes.Handover, 40, Today);
        var added = CaptureAdds();

        var result = await Service().SaveAsync(Save(70), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Quantity.Should().Be(30);
    }

    [Fact]
    public async Task Save_over_what_the_team_can_still_give_is_rejected()
    {
        CaptureAdds();

        var result = await Service().SaveAsync(Save(101), TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AllocationExceedsRequired);
    }

    [Fact]
    public async Task Save_for_a_future_day_is_rejected()
    {
        var result = await Service().SaveAsync(Save(10) with { WorkDate = Today.AddDays(1) }, TeamId, default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Lowering_the_day_records_a_retrieval_and_needs_a_reason()
    {
        Held(PieceMovementTypes.Handover, 50, Today);
        var added = CaptureAdds();

        var noReason = await Service().SaveAsync(Save(20), TeamId, default);
        var withReason = await Service().SaveAsync(Save(20, " đổi việc "), TeamId, default);

        noReason.IsSuccess.Should().BeFalse();
        withReason.IsSuccess.Should().BeTrue();
        var m = added.Should().ContainSingle().Subject;
        (m.MovementType, m.Quantity, m.RetrievalReason, m.RetrievalNote).Should()
            .Be((PieceMovementTypes.Retrieval, 30, RetrievalReasons.Adjusted, "đổi việc"));
    }

    [Fact]
    public async Task Lowering_the_day_below_the_reported_output_is_rejected()
    {
        Held(PieceMovementTypes.Handover, 50, Today);
        Output(35, Today);
        CaptureAdds();

        var result = await Service().SaveAsync(Save(30, "x"), TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
    }

    [Fact]
    public async Task GetBoardAsync_YesterdayNotFinished_CarriesRemainingToToday()
    {
        var yesterday = Today.AddDays(-1);
        Held(PieceMovementTypes.Handover, 50, yesterday);
        Output(35, yesterday);
        Held(PieceMovementTypes.Handover, 20, Today);
        Output(25, Today);

        var result = await Service().GetBoardAsync(TeamId, Today, 7, null, default);

        var row = result.Data!.Rows.Should().ContainSingle().Subject;
        (row.CarriedQty, row.AllocatedQty, row.OutputQty, row.RemainingQty, row.PreviousQty).Should().Be((15, 20, 25, 10, 50));
    }

    [Fact]
    public async Task SaveAsync_OutputCoveredByCarry_AllowsLoweringTodayBelowTodayOutput()
    {
        var yesterday = Today.AddDays(-1);
        Held(PieceMovementTypes.Handover, 50, yesterday);
        Output(30, yesterday);
        Held(PieceMovementTypes.Handover, 20, Today);
        Output(25, Today);
        var added = CaptureAdds();

        // Tồn 20 + giao 10 = 30 ≥ 25 đã báo trong ngày.
        var result = await Service().SaveAsync(Save(10, "đổi việc"), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task SaveAsync_CarryPlusDayBelowTodayOutput_ReturnsFailure()
    {
        var yesterday = Today.AddDays(-1);
        Held(PieceMovementTypes.Handover, 50, yesterday);
        Output(45, yesterday);
        Held(PieceMovementTypes.Handover, 20, Today);
        Output(15, Today);
        CaptureAdds();

        // Tồn 5 + giao 5 = 10 < 15 đã báo trong ngày.
        var result = await Service().SaveAsync(Save(5, "x"), TeamId, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
    }

    [Fact]
    public async Task Board_lists_assigned_operations_and_flags_the_ones_outside_the_assignment()
    {
        _assignments.Add(new OperationAssignment { SewingTeamId = TeamId, OrderId = 7, OperationId = OpId, WorkerId = WorkerId });
        Held(PieceMovementTypes.Handover, 25, Today);

        var result = await Service().GetBoardAsync(TeamId, Today, 7, null, default);

        result.IsSuccess.Should().BeTrue();
        var row = result.Data!.Rows.Should().ContainSingle().Subject;
        (row.IsAssigned, row.AllocatedQty, row.UnallocatedQty).Should().Be((true, 25, 100));
    }

    [Fact]
    public async Task GetBoardAsync_LineNotAllottedToTeam_IsHidden()
    {
        _assignments.Add(new OperationAssignment { SewingTeamId = TeamId, OrderId = 7, OperationId = OpId, WorkerId = WorkerId });
        _allotted.Clear();

        var result = await Service().GetBoardAsync(TeamId, Today, 7, null, default);

        result.Data!.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_LineNotAllottedToTeam_ReturnsFailure()
    {
        var added = CaptureAdds();
        _allotted.Clear();

        var result = await Service().SaveAsync(Save(10), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
        added.Should().BeEmpty();
    }
}
