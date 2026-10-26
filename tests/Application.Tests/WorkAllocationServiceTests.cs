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

/// <summary>TH-19 / UC53, UC54, UC60 (database 25/10): giao / thu hồi việc theo công đoạn kèm BTP, tự xác nhận và xác nhận hộ.</summary>
public class WorkAllocationServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0);
    private const int TeamId = 5;
    private const int WorkerId = 40;
    private const int SewOpId = 9;      // công đoạn dùng BTP
    private const int IronOpId = 10;    // công đoạn không dùng BTP

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInventoryService _inventory = Substitute.For<IInventoryService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Team> _teams = new();
    private readonly List<Employee> _employees = new();
    private readonly List<OrderLine> _orderLines = new();
    private readonly List<Operation> _operations = new();
    private readonly List<WorkAllocation> _allocations = new();
    private readonly List<VwOperationAllocationProgress> _progress = new();
    private readonly List<OutputRecord> _outputs = new();
    private readonly Team _team = new() { TeamId = TeamId, TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing };
    private readonly Employee _worker;
    private readonly CustomerOrder _order = new() { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction };
    private readonly OrderLine _line;
    private int _held = 100;

    public WorkAllocationServiceTests()
    {
        _clock.Now.Returns(Now);
        _worker = new Employee { EmployeeId = WorkerId, EmployeeCode = "CNMAY1", FullName = "Công nhân 1", TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker } };
        _line = new OrderLine { OrderLineId = 101, OrderId = 7, Order = _order, ProductId = 3, ProductVariant = TestVariant.Make(new Product { ProductId = 3, ProductCode = "AO-01" }, "Đen", "M") };
        _teams.Add(_team); _employees.Add(_worker); _orderLines.Add(_line);
        _employees.Add(new Employee { EmployeeId = 41, TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.TeamLeader } });
        _operations.Add(new Operation { OperationId = SewOpId, ProductId = 3, OperationCode = "OP09", OperationName = "Tra tay", UsesSemiFinished = true, StationSequence = 9 });
        _operations.Add(new Operation { OperationId = IronOpId, ProductId = 3, OperationCode = "OP10", OperationName = "Là", UsesSemiFinished = false, StationSequence = 10 });
        _progress.Add(Progress(SewOpId, required: 100, allocated: 0, pending: 0));
        _progress.Add(Progress(IronOpId, required: 100, allocated: 0, pending: 0));
        _inventory.GetTeamHeldQtyAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _held);
        Setup(_teams); Setup(_employees); Setup(_orderLines); Setup(_operations); Setup(_allocations); Setup(_progress); Setup(_outputs);
    }

    private static VwOperationAllocationProgress Progress(int operationId, int required, int allocated, int pending) => new()
    {
        SewingTeamId = TeamId, OrderLineId = 101, OperationId = operationId, RequiredQty = required, AllocatedQty = allocated,
        PendingQty = pending, UnallocatedQty = required - allocated - pending, UsesSemiFinished = operationId == SewOpId,
    };

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private WorkAllocationService Service() => new(_uow, _inventory, _notifier, _audit, _locks, _clock);

    private WorkAllocation Allocation(string type, string status, int qty, int? confirmed = null, int worker = WorkerId,
        int operationId = SewOpId, int pieces = 0, int? confirmedPieces = null, string? retrievalReason = null)
    {
        var m = new WorkAllocation
        {
            WorkAllocationId = _allocations.Count + 1, MovementType = type, SewingTeamId = TeamId, SewingTeam = _team,
            WorkerId = worker, Worker = _worker, OrderLineId = 101, OrderLine = _line, OperationId = operationId,
            Operation = _operations.First(o => o.OperationId == operationId), Quantity = qty, PieceQty = pieces,
            ConfirmedQty = confirmed, ConfirmedPieceQty = confirmedPieces, Status = status, ConfirmMode = ConfirmModes.Worker,
            CreatedAt = Now, RetrievalReason = type == PieceMovementTypes.Retrieval ? retrievalReason ?? RetrievalReasons.WorkerAbsent : null,
        };
        _allocations.Add(m);
        return m;
    }

    private List<WorkAllocation> CaptureAdds()
    {
        var added = new List<WorkAllocation>();
        _uow.Repository<WorkAllocation>().AddAsync(Arg.Do<WorkAllocation>(m =>
        {
            m.SewingTeam = _team; m.Worker = _worker; m.OrderLine = _line;
            m.Operation = _operations.First(o => o.OperationId == m.OperationId);
            added.Add(m); _allocations.Add(m);
        }));
        return added;
    }

    private static CreateWorkAllocationDto Handover(int qty, int operationId = SewOpId, int? pieces = null, int worker = WorkerId) =>
        new(PieceMovementTypes.Handover, worker, 101, operationId, qty, pieces, null);

    private static CreateWorkAllocationDto Retrieval(int qty, string? reason = RetrievalReasons.OperationChanged, int operationId = SewOpId) =>
        new(PieceMovementTypes.Retrieval, WorkerId, 101, operationId, qty, null, reason);

    // ---------- Giao việc ----------

    [Fact]
    public async Task Handover_is_recorded_pending_with_pieces_defaulting_to_the_qty_and_notifies_the_worker()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Handover(60), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        var m = added.Should().ContainSingle().Subject;
        m.Status.Should().Be(HandoverStatuses.Pending);
        m.ConfirmMode.Should().Be(ConfirmModes.Worker);
        m.OperationId.Should().Be(SewOpId);
        m.Quantity.Should().Be(60);
        m.PieceQty.Should().Be(60);
        m.ConfirmedQty.Should().BeNull();
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Single() == WorkerId), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task An_operation_that_does_not_use_pieces_is_handed_over_without_pieces_and_refuses_pieces()
    {
        var added = CaptureAdds();

        (await Service().CreateAsync(Handover(40, IronOpId), TeamId, default)).IsSuccess.Should().BeTrue();
        added.Single().PieceQty.Should().Be(0);

        var withPieces = await Service().CreateAsync(Handover(10, IronOpId, pieces: 5), TeamId, default);
        withPieces.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task The_pieces_can_be_set_lower_than_the_qty_but_not_higher()
    {
        var added = CaptureAdds();
        (await Service().CreateAsync(Handover(30, pieces: 20), TeamId, default)).IsSuccess.Should().BeTrue();
        added.Single().PieceQty.Should().Be(20);

        (await Service().CreateAsync(Handover(30, pieces: 31), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Handover_cannot_exceed_what_is_left_to_allocate_for_the_operation()
    {
        _progress.Clear();
        _progress.Add(Progress(SewOpId, required: 100, allocated: 70, pending: 10));   // còn 20

        var result = await Service().CreateAsync(Handover(21), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.AllocationExceedsRequired);
        result.Message.Should().Contain("còn 20");
    }

    [Fact]
    public async Task Nothing_can_be_allocated_before_the_team_has_received_pieces()
    {
        _progress.Clear();   // tổ chưa nhận BTP của dòng đơn này → không có dòng tiến độ

        var result = await Service().CreateAsync(Handover(1), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.AllocationExceedsRequired);
    }

    [Fact]
    public async Task Pieces_cannot_exceed_what_the_team_holds_and_pending_pieces_are_reserved()
    {
        (await Service().CreateAsync(Handover(101, pieces: 101), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.AllocationExceedsRequired);

        _held = 80;
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60, worker: 41);
        var result = await Service().CreateAsync(Handover(30, pieces: 30), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceHandoverExceedsHolding);
        result.Message.Should().Contain("còn 20");
    }

    [Fact]
    public async Task Handover_is_refused_for_a_worker_of_another_team_or_a_resigned_worker()
    {
        _worker.TeamId = 99;
        (await Service().CreateAsync(Handover(10), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);

        _worker.TeamId = TeamId;
        _worker.Status = RecordStatus.Resigned;
        (await Service().CreateAsync(Handover(10), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task Only_a_sewing_team_can_allocate_work()
    {
        _team.TeamType = TeamTypes.Cutting;

        var result = await Service().CreateAsync(Handover(10), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task Handover_is_blocked_for_a_closed_order_and_for_an_operation_of_another_product()
    {
        _operations.Add(new Operation { OperationId = 99, ProductId = 4, OperationName = "May cổ" });
        (await Service().CreateAsync(Handover(10, operationId: 99), TeamId, default)).ErrorCode
            .Should().Be(ErrorCodes.WarehouseInvalidReference);

        _order.Status = OrderStatuses.Delivered;
        (await Service().CreateAsync(Handover(10), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
    }

    // ---------- Thu hồi việc ----------

    [Fact]
    public async Task Retrieval_is_limited_to_what_the_worker_holds_for_that_operation_minus_pending_and_reported_output()
    {
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 80, confirmed: 80, pieces: 80, confirmedPieces: 80);
        Allocation(PieceMovementTypes.Retrieval, HandoverStatuses.Confirmed, 20, confirmed: 20);       // còn giữ 60
        Allocation(PieceMovementTypes.Retrieval, HandoverStatuses.Pending, 10);                        // chờ thu hồi 10 → 50
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 99, pieces: 99);             // chưa xác nhận: không tính
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 30, confirmed: 30, operationId: IronOpId); // công đoạn khác
        _outputs.Add(new OutputRecord { WorkerId = WorkerId, OrderLineId = 101, OperationId = SewOpId, Quantity = 15 });   // đã báo 15 → thu hồi được 35

        var over = await Service().CreateAsync(Retrieval(36), TeamId, default);
        over.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
        over.Message.Should().Contain("còn 35");

        CaptureAdds();
        (await Service().CreateAsync(Retrieval(35), TeamId, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Retrieval_stores_the_reason_and_handover_does_not()
    {
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 20, confirmed: 20);
        var added = CaptureAdds();

        await Service().CreateAsync(Retrieval(5, RetrievalReasons.StyleFinished), TeamId, default);
        await Service().CreateAsync(new CreateWorkAllocationDto(PieceMovementTypes.Handover, WorkerId, 101, SewOpId, 5, null, "ignored"), TeamId, default);

        added[0].RetrievalReason.Should().Be(RetrievalReasons.StyleFinished);
        added[1].RetrievalReason.Should().BeNull();
    }

    // ---------- Công nhân tự xác nhận (UC60) ----------

    [Fact]
    public async Task Worker_confirms_the_full_qty_and_pieces_without_a_reason()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60);

        var result = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, null, null), WorkerId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Confirmed);
        m.ConfirmedQty.Should().Be(60);
        m.ConfirmedPieceQty.Should().Be(60);
        m.ConfirmMode.Should().Be(ConfirmModes.Worker);
        m.ConfirmedAt.Should().Be(Now);
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Contains(41)), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task Worker_confirming_a_different_qty_or_pieces_must_give_a_reason()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60);

        var noReason = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(55, null, null), WorkerId, default);
        var piecesOnly = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, 58, null), WorkerId, default);
        var withReason = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(55, null, " thiếu 5 "), WorkerId, default);

        noReason.IsSuccess.Should().BeFalse();
        piecesOnly.IsSuccess.Should().BeFalse();
        withReason.IsSuccess.Should().BeTrue();
        m.ConfirmedQty.Should().Be(55);
        m.ConfirmedPieceQty.Should().Be(55);   // tỉ lệ: BTP không nhiều hơn số lần xác nhận
        m.DiscrepancyReason.Should().Be("thiếu 5");
    }

    [Fact]
    public async Task Worker_cannot_confirm_more_than_the_recorded_qty_or_more_pieces_than_qty()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 40);

        (await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(61, null, "x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        (await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, 41, "x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task A_worker_cannot_touch_another_workers_allocation()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, worker: 77);

        (await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, null, null), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().RejectByWorkerAsync(m.WorkAllocationId, new ReasonDto("x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task A_confirmed_allocation_cannot_be_decided_again()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 60, confirmed: 60);

        (await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, null, null), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().RejectByWorkerAsync(m.WorkAllocationId, new ReasonDto("x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Worker_reject_stores_the_reason_and_leaves_holdings_unchanged()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60);

        var result = await Service().RejectByWorkerAsync(m.WorkAllocationId, new ReasonDto(" không phải của tôi "), WorkerId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Rejected);
        m.RejectReason.Should().Be("không phải của tôi");
        m.ConfirmedQty.Should().BeNull();
    }

    [Fact]
    public async Task Confirming_a_handover_rechecks_that_the_team_still_holds_the_pieces()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60);
        _held = 50;   // trong lúc chờ, tổ đã giao bớt BTP cho công nhân khác

        var result = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(60, null, null), WorkerId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceHandoverExceedsHolding);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task Confirming_a_retrieval_is_refused_when_the_worker_has_reported_output_since()
    {
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 50, confirmed: 50);
        var m = Allocation(PieceMovementTypes.Retrieval, HandoverStatuses.Pending, 30);
        _outputs.Add(new OutputRecord { WorkerId = WorkerId, OrderLineId = 101, OperationId = SewOpId, Quantity = 30 });   // chỉ còn thu hồi được 20

        var result = await Service().ConfirmByWorkerAsync(m.WorkAllocationId, new ConfirmWorkAllocationDto(30, null, null), WorkerId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
    }

    // ---------- Tổ trưởng xác nhận hộ (UC54) ----------

    [Fact]
    public async Task Leader_confirms_on_behalf_with_a_reason_and_the_mode_is_recorded()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, pieces: 60);

        var result = await Service().ConfirmOnBehalfAsync(m.WorkAllocationId,
            new ConfirmOnBehalfDto(60, null, null, " công nhân nghỉ ốm "), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Confirmed);
        m.ConfirmMode.Should().Be(ConfirmModes.OnBehalf);
        m.OnBehalfReason.Should().Be("công nhân nghỉ ốm");
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(WorkAllocation)));
    }

    [Fact]
    public async Task A_leader_of_another_team_cannot_confirm_on_behalf()
    {
        var m = Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().ConfirmOnBehalfAsync(m.WorkAllocationId, new ConfirmOnBehalfDto(60, null, null, "x"), 99, default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    // ---------- Bảng chia việc ----------

    [Fact]
    public async Task Board_shows_team_pieces_operation_progress_and_what_each_worker_can_give_back()
    {
        _inventory.GetTeamHoldingsAsync(TeamId, null, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<TeamPieceHoldingDto>>.Success(new[]
            {
                new TeamPieceHoldingDto(TeamId, "Tổ may 1", 101, "DH-1", "AO-01", "Đen", "M", 200, 80, 0, 120),
            }));
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 80, confirmed: 80, pieces: 80, confirmedPieces: 80);
        Allocation(PieceMovementTypes.Retrieval, HandoverStatuses.Pending, 15);
        Allocation(PieceMovementTypes.Handover, HandoverStatuses.Pending, 20, pieces: 20, worker: 41);
        _outputs.Add(new OutputRecord { SewingTeamId = TeamId, WorkerId = WorkerId, OrderLineId = 101, OperationId = SewOpId, Quantity = 10, OrderLine = _line });
        _progress.Clear();
        _progress.Add(Progress(SewOpId, required: 200, allocated: 80, pending: 20));

        var board = (await Service().GetBoardAsync(TeamId, null, default)).Data!;

        var team = board.Team.Should().ContainSingle().Subject;
        team.HeldByTeam.Should().Be(120);
        team.PendingPieceQty.Should().Be(20);
        team.AvailablePieceQty.Should().Be(100);
        var operation = board.Operations.Should().ContainSingle().Subject;
        operation.OperationId.Should().Be(SewOpId);
        operation.UnallocatedQty.Should().Be(100);
        operation.UsesSemiFinished.Should().BeTrue();
        var worker = board.Workers.Should().ContainSingle().Subject;   // chỉ người đã được giao và đang giữ
        worker.WorkerId.Should().Be(WorkerId);
        worker.HeldQty.Should().Be(80);
        worker.PendingRetrievalQty.Should().Be(15);
        worker.OutputQty.Should().Be(10);
        worker.RetrievableQty.Should().Be(55);
    }

    // ---------- Validator ----------

    [Fact]
    public void Validators_require_a_type_an_operation_a_positive_qty_and_a_reason_for_retrieval_and_on_behalf()
    {
        var create = new CreateWorkAllocationDtoValidator();
        create.Validate(Handover(5)).IsValid.Should().BeTrue();
        create.Validate(Retrieval(5)).IsValid.Should().BeTrue();
        create.Validate(Retrieval(5, reason: null)).IsValid.Should().BeFalse();
        create.Validate(Retrieval(5, reason: "OTHER")).IsValid.Should().BeFalse();
        create.Validate(Handover(0)).IsValid.Should().BeFalse();
        create.Validate(Handover(5, pieces: 6)).IsValid.Should().BeFalse();
        create.Validate(Handover(5, operationId: 0)).IsValid.Should().BeFalse();
        create.Validate(Handover(5) with { MovementType = "SWAP" }).IsValid.Should().BeFalse();
        create.Validate(Handover(5, worker: 0)).IsValid.Should().BeFalse();

        var behalf = new ConfirmOnBehalfDtoValidator();
        behalf.Validate(new ConfirmOnBehalfDto(5, null, null, "nghỉ ốm")).IsValid.Should().BeTrue();
        behalf.Validate(new ConfirmOnBehalfDto(5, null, null, " ")).IsValid.Should().BeFalse();
        behalf.Validate(new ConfirmOnBehalfDto(-1, null, null, "x")).IsValid.Should().BeFalse();
    }
}
