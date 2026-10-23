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

/// <summary>TH-19 / UC53, UC54, UC60: giao / thu hồi BTP với công nhân, tự xác nhận và xác nhận hộ.</summary>
public class WorkerPieceMovementServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 9, 0, 0);
    private const int TeamId = 5;
    private const int WorkerId = 40;

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
    private readonly List<WorkerPieceMovement> _movements = new();
    private readonly Team _team = new() { TeamId = TeamId, TeamName = "Tổ may 1", TeamType = TeamTypes.Sewing };
    private readonly Employee _worker;
    private readonly CustomerOrder _order = new() { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction };
    private readonly OrderLine _line;
    private int _held = 100;

    public WorkerPieceMovementServiceTests()
    {
        _clock.Now.Returns(Now);
        _worker = new Employee { EmployeeId = WorkerId, EmployeeCode = "CNMAY1", FullName = "Công nhân 1", TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.Worker } };
        _line = new OrderLine { OrderLineId = 101, OrderId = 7, Order = _order, ProductId = 3, Product = new Product { ProductId = 3, ProductCode = "AO-01" }, Color = "Đen", SizeCode = "M" };
        _teams.Add(_team); _employees.Add(_worker); _orderLines.Add(_line);
        _employees.Add(new Employee { EmployeeId = 41, TeamId = TeamId, Status = RecordStatus.Active, Role = new Role { RoleCode = RoleCodes.TeamLeader } });
        _inventory.GetTeamHeldQtyAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _held);
        Setup(_teams); Setup(_employees); Setup(_orderLines); Setup(_operations); Setup(_movements);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private WorkerPieceMovementService Service() => new(_uow, _inventory, _notifier, _audit, _locks, _clock);

    private WorkerPieceMovement Movement(string type, string status, int qty, int? confirmed = null, int worker = WorkerId,
        string? retrievalReason = null)
    {
        var m = new WorkerPieceMovement
        {
            WorkerPieceMovementId = _movements.Count + 1, MovementType = type, SewingTeamId = TeamId, SewingTeam = _team,
            WorkerId = worker, Worker = _worker, OrderLineId = 101, OrderLine = _line, Quantity = qty, ConfirmedQty = confirmed,
            Status = status, ConfirmMode = ConfirmModes.Worker, CreatedAt = Now,
            RetrievalReason = type == PieceMovementTypes.Retrieval ? retrievalReason ?? RetrievalReasons.WorkerAbsent : null,
        };
        _movements.Add(m);
        return m;
    }

    private List<WorkerPieceMovement> CaptureAdds()
    {
        var added = new List<WorkerPieceMovement>();
        _uow.Repository<WorkerPieceMovement>().AddAsync(Arg.Do<WorkerPieceMovement>(m =>
        {
            m.SewingTeam = _team; m.Worker = _worker; m.OrderLine = _line;
            added.Add(m); _movements.Add(m);
        }));
        return added;
    }

    private static CreateWorkerMovementDto Handover(int qty, int worker = WorkerId, int? operationId = null) =>
        new(PieceMovementTypes.Handover, worker, 101, operationId, qty, null);

    private static CreateWorkerMovementDto Retrieval(int qty, string? reason = RetrievalReasons.OperationChanged) =>
        new(PieceMovementTypes.Retrieval, WorkerId, 101, null, qty, reason);

    // ---------- Giao BTP ----------

    [Fact]
    public async Task Handover_is_recorded_pending_and_notifies_the_worker()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Handover(60), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        var m = added.Should().ContainSingle().Subject;
        m.Status.Should().Be(HandoverStatuses.Pending);
        m.ConfirmMode.Should().Be(ConfirmModes.Worker);
        m.Quantity.Should().Be(60);
        m.ConfirmedQty.Should().BeNull();
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Single() == WorkerId), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task Handover_cannot_exceed_what_the_team_holds()
    {
        var result = await Service().CreateAsync(Handover(101), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceHandoverExceedsHolding);
        result.Message.Should().Contain("còn 100");
    }

    [Fact]
    public async Task Pending_handovers_are_reserved_against_the_team_holding()
    {
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 70);

        var result = await Service().CreateAsync(Handover(31), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceHandoverExceedsHolding);
        result.Message.Should().Contain("còn 30");
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
    public async Task Only_a_sewing_team_can_hand_over_pieces()
    {
        _team.TeamType = TeamTypes.Cutting;

        var result = await Service().CreateAsync(Handover(10), TeamId, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
    }

    [Fact]
    public async Task Handover_is_blocked_for_a_closed_order_and_for_an_operation_of_another_product()
    {
        _operations.Add(new Operation { OperationId = 9, ProductId = 4, OperationName = "May cổ" });
        (await Service().CreateAsync(Handover(10, operationId: 9), TeamId, default)).ErrorCode
            .Should().Be(ErrorCodes.WarehouseInvalidReference);

        _order.Status = OrderStatuses.Delivered;
        (await Service().CreateAsync(Handover(10), TeamId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
    }

    // ---------- Thu hồi BTP ----------

    [Fact]
    public async Task Retrieval_is_limited_to_what_the_worker_currently_holds()
    {
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 80, confirmed: 80);
        Movement(PieceMovementTypes.Retrieval, HandoverStatuses.Confirmed, 30, confirmed: 30);   // còn giữ 50
        Movement(PieceMovementTypes.Retrieval, HandoverStatuses.Pending, 10);                    // đang chờ thu hồi 10 → thu thêm được 40
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 99);                     // chưa xác nhận: không tính là đang giữ

        var over = await Service().CreateAsync(Retrieval(41), TeamId, default);
        over.ErrorCode.Should().Be(ErrorCodes.PieceRetrievalExceedsHolding);
        over.Message.Should().Contain("còn 40");

        CaptureAdds();
        (await Service().CreateAsync(Retrieval(40), TeamId, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Retrieval_stores_the_reason_and_handover_does_not()
    {
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 20, confirmed: 20);
        var added = CaptureAdds();

        await Service().CreateAsync(Retrieval(5, RetrievalReasons.StyleFinished), TeamId, default);
        await Service().CreateAsync(new CreateWorkerMovementDto(PieceMovementTypes.Handover, WorkerId, 101, null, 5, "ignored"), TeamId, default);

        added[0].RetrievalReason.Should().Be(RetrievalReasons.StyleFinished);
        added[1].RetrievalReason.Should().BeNull();
    }

    // ---------- Công nhân tự xác nhận (UC60) ----------

    [Fact]
    public async Task Worker_confirms_the_full_qty_without_a_reason()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(60, null), WorkerId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Confirmed);
        m.ConfirmedQty.Should().Be(60);
        m.ConfirmMode.Should().Be(ConfirmModes.Worker);
        m.ConfirmedAt.Should().Be(Now);
        _notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Contains(41)), Arg.Any<NotificationMessage>());
    }

    [Fact]
    public async Task Worker_confirming_a_different_qty_must_give_a_reason()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var noReason = await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(55, null), WorkerId, default);
        var withReason = await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(55, " thiếu 5 "), WorkerId, default);

        noReason.IsSuccess.Should().BeFalse();
        withReason.IsSuccess.Should().BeTrue();
        m.ConfirmedQty.Should().Be(55);
        m.DiscrepancyReason.Should().Be("thiếu 5");
    }

    [Fact]
    public async Task Worker_cannot_confirm_more_than_the_recorded_qty()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(61, "x"), WorkerId, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task A_worker_cannot_touch_another_workers_movement()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60, worker: 77);

        (await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(60, null), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().RejectByWorkerAsync(m.WorkerPieceMovementId, new ReasonDto("x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task A_confirmed_movement_cannot_be_decided_again()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 60, confirmed: 60);

        (await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(60, null), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().RejectByWorkerAsync(m.WorkerPieceMovementId, new ReasonDto("x"), WorkerId, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Worker_reject_stores_the_reason_and_leaves_holdings_unchanged()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().RejectByWorkerAsync(m.WorkerPieceMovementId, new ReasonDto(" không phải của tôi "), WorkerId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Rejected);
        m.RejectReason.Should().Be("không phải của tôi");
        m.ConfirmedQty.Should().BeNull();
    }

    [Fact]
    public async Task Confirming_a_handover_rechecks_that_the_team_still_holds_the_pieces()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);
        _held = 50;   // trong lúc chờ, tổ đã giao bớt cho công nhân khác

        var result = await Service().ConfirmByWorkerAsync(m.WorkerPieceMovementId, new ConfirmWorkerMovementDto(60, null), WorkerId, default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceHandoverExceedsHolding);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    // ---------- Tổ trưởng xác nhận hộ (UC54) ----------

    [Fact]
    public async Task Leader_confirms_on_behalf_with_a_reason_and_the_mode_is_recorded()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().ConfirmOnBehalfAsync(m.WorkerPieceMovementId,
            new ConfirmOnBehalfDto(60, null, " công nhân nghỉ ốm "), TeamId, default);

        result.IsSuccess.Should().BeTrue();
        m.Status.Should().Be(HandoverStatuses.Confirmed);
        m.ConfirmMode.Should().Be(ConfirmModes.OnBehalf);
        m.OnBehalfReason.Should().Be("công nhân nghỉ ốm");
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(WorkerPieceMovement)));
    }

    [Fact]
    public async Task A_leader_of_another_team_cannot_confirm_on_behalf()
    {
        var m = Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 60);

        var result = await Service().ConfirmOnBehalfAsync(m.WorkerPieceMovementId, new ConfirmOnBehalfDto(60, null, "x"), 99, default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        m.Status.Should().Be(HandoverStatuses.Pending);
    }

    // ---------- Tồn BTP ----------

    [Fact]
    public async Task Holdings_show_team_availability_and_what_each_worker_can_give_back()
    {
        _inventory.GetTeamHoldingsAsync(TeamId, null, Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<TeamPieceHoldingDto>>.Success(new[]
            {
                new TeamPieceHoldingDto(TeamId, "Tổ may 1", 101, "DH-1", "AO-01", "Đen", "M", 200, 80, 0, 120),
            }));
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Confirmed, 80, confirmed: 80);
        Movement(PieceMovementTypes.Retrieval, HandoverStatuses.Pending, 15);
        Movement(PieceMovementTypes.Handover, HandoverStatuses.Pending, 20, worker: 41);

        var holdings = (await Service().GetHoldingsAsync(TeamId, null, default)).Data!;

        var team = holdings.Team.Should().ContainSingle().Subject;
        team.HeldByTeam.Should().Be(120);
        team.PendingHandoverQty.Should().Be(20 + 0);
        team.AvailableQty.Should().Be(100);
        var worker = holdings.Workers.Should().ContainSingle().Subject;   // chỉ người đã được giao và đang giữ
        worker.WorkerId.Should().Be(WorkerId);
        worker.HeldQty.Should().Be(80);
        worker.PendingRetrievalQty.Should().Be(15);
        worker.RetrievableQty.Should().Be(65);
    }

    // ---------- Validator ----------

    [Fact]
    public void Validators_require_a_type_a_positive_qty_and_a_reason_for_retrieval_and_on_behalf()
    {
        var create = new CreateWorkerMovementDtoValidator();
        create.Validate(Handover(5)).IsValid.Should().BeTrue();
        create.Validate(Retrieval(5)).IsValid.Should().BeTrue();
        create.Validate(Retrieval(5, reason: null)).IsValid.Should().BeFalse();
        create.Validate(Retrieval(5, reason: "OTHER")).IsValid.Should().BeFalse();
        create.Validate(Handover(0)).IsValid.Should().BeFalse();
        create.Validate(Handover(5) with { MovementType = "SWAP" }).IsValid.Should().BeFalse();
        create.Validate(Handover(5, worker: 0)).IsValid.Should().BeFalse();

        var behalf = new ConfirmOnBehalfDtoValidator();
        behalf.Validate(new ConfirmOnBehalfDto(5, null, "nghỉ ốm")).IsValid.Should().BeTrue();
        behalf.Validate(new ConfirmOnBehalfDto(5, null, " ")).IsValid.Should().BeFalse();
        behalf.Validate(new ConfirmOnBehalfDto(-1, null, "x")).IsValid.Should().BeFalse();
    }
}
