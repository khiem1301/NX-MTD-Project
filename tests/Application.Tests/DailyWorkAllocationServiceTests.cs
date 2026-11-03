using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-63: giao sản lượng công đoạn theo ngày, hạ số giao, thu hồi phần chưa làm hết cuối ngày (tổ trưởng và hệ thống).</summary>
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
    private readonly List<WorkAllocation> _allocations = new();
    private readonly List<VwOperationAllocationProgress> _progress = new();
    private readonly List<OutputRecord> _outputs = new();
    private readonly List<OperationAssignment> _assignments = new();

    public DailyWorkAllocationServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        var order = new CustomerOrder { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction };
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
        _progress.Add(new VwOperationAllocationProgress
        {
            SewingTeamId = TeamId, OrderLineId = 101, OperationId = OpId, RequiredQty = 100, UnallocatedQty = 100,
        });
        Setup(_teams); Setup(_employees); Setup(_orderLines); Setup(_operations); Setup(_allocations);
        Setup(_progress); Setup(_outputs); Setup(_assignments);
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
    public async Task Retrieve_remaining_takes_back_what_was_not_made_and_records_the_reason()
    {
        Held(PieceMovementTypes.Handover, 50, Today);
        Output(35, Today);
        var added = CaptureAdds();

        var result = await Service().RetrieveRemainingAsync(new RetrieveDailyAllocationDto(Today, null, " hết giờ "), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        var m = added.Should().ContainSingle().Subject;
        (m.Quantity, m.RetrievalReason, m.RetrievalNote, m.ConfirmMode).Should()
            .Be((15, RetrievalReasons.EndOfDay, "hết giờ", ConfirmModes.Leader));
    }

    [Fact]
    public async Task Retrieve_remaining_with_nothing_left_is_rejected()
    {
        Held(PieceMovementTypes.Handover, 50, Today);
        Output(50, Today);
        CaptureAdds();

        var result = await Service().RetrieveRemainingAsync(new RetrieveDailyAllocationDto(Today, null, "x"), TeamId, default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Auto_retrieve_takes_back_old_days_as_the_system_but_leaves_recent_days()
    {
        var oldDay = Today.AddDays(-3);
        Held(PieceMovementTypes.Handover, 50, oldDay);
        Output(20, oldDay);
        Held(PieceMovementTypes.Handover, 10, Today.AddDays(-1));
        var added = CaptureAdds();

        var count = await Service().AutoRetrieveAsync(default);

        count.Should().Be(1);
        var m = added.Should().ContainSingle().Subject;
        (m.WorkDate, m.Quantity, m.ConfirmMode, m.RetrievalReason).Should().Be((oldDay, 30, ConfirmModes.System, RetrievalReasons.EndOfDay));
        m.RetrievalNote.Should().NotBeNullOrWhiteSpace();
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
}
