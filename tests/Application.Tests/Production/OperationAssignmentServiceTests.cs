using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-03 (UC52): tổ trưởng / tổ phó may phân công công nhân × công đoạn; đổi phân công giữ lịch sử.</summary>
public class OperationAssignmentServiceTests : ProductionTestBase
{
    private const int TeamId = 5;

    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<CustomerOrder> _orders =
    [
        new() { OrderId = 1, OrderCode = "DH001", Status = OrderStatuses.InProduction },
        new() { OrderId = 2, OrderCode = "DH002", Status = OrderStatuses.New },
        new() { OrderId = 3, OrderCode = "DH003", Status = OrderStatuses.Cancelled },
    ];
    private readonly List<OrderLine> _lines =
    [
        new() { OrderLineId = 11, OrderId = 1, ProductId = 1 },
        new() { OrderLineId = 21, OrderId = 2, ProductId = 1 },
        new() { OrderLineId = 31, OrderId = 3, ProductId = 1 },
    ];
    private readonly List<OrderLineTeam> _allotted =
    [
        new() { OrderLineId = 11, SewingTeamId = TeamId },
        new() { OrderLineId = 21, SewingTeamId = TeamId },
        new() { OrderLineId = 31, SewingTeamId = TeamId },
    ];
    private readonly List<Operation> _operations = [];
    private readonly List<PieceRate> _rates = [];
    private readonly List<Employee> _employees = [];
    private readonly OperationAssignmentService _sut;
    private int _nextId = 100;

    public OperationAssignmentServiceTests()
    {
        SetupQueryable(_assignments);
        SetupQueryable(_orders);
        SetupQueryable(_lines);
        SetupQueryable(_allotted);
        SetupQueryable(_operations);
        SetupQueryable(_rates);
        SetupQueryable(_employees);
        AsTeamLeader(TeamId);
        Repo<OperationAssignment>().When(r => r.AddAsync(Arg.Any<OperationAssignment>())).Do(c =>
        {
            var row = c.Arg<OperationAssignment>();
            row.OperationAssignmentId = _nextId++;
            _assignments.Add(row);
        });
        Repo<OperationAssignment>().When(r => r.AddRangeAsync(Arg.Any<IEnumerable<OperationAssignment>>())).Do(c =>
        {
            foreach (var row in c.Arg<IEnumerable<OperationAssignment>>())
            {
                row.OperationAssignmentId = _nextId++;
                _assignments.Add(row);
            }
        });
        _sut = new OperationAssignmentService(Uow, Guard(), Clock, Rates());
    }

    private IPieceRateService Rates() => new PieceRateService(Uow, Audit, Notifier, Clock);

    private Operation Op(int id, string code = "OP10", int productId = 1, string status = OperationStatuses.Active, bool hasRate = true)
    {
        var op = new Operation { OperationId = id, ProductId = productId, OperationCode = code, OperationName = "CĐ " + code,
            StationSequence = (short)id, Status = status, TeamType = TeamTypes.Sewing, Product = new Product { ProductId = productId, ProductCode = "MH00" + productId } };
        if (hasRate)
        {
            var rate = TestRates.Approved(id);
            op.PieceRates.Add(rate);
            _rates.Add(rate);
        }
        _operations.Add(op);
        return op;
    }

    [Fact]
    public async Task ListOperations_returns_active_sewing_operations_of_order_products_only()
    {
        Op(1, "OP10").TeamType = TeamTypes.Sewing;
        Op(2, "OP20", status: OperationStatuses.Discontinued).TeamType = TeamTypes.Sewing;   // đã ngừng
        Op(3, "OP30", productId: 9).TeamType = TeamTypes.Sewing;                              // mã hàng không thuộc đơn
        Op(4, "OP40").TeamType = TeamTypes.Cutting;                                           // công đoạn tổ cắt

        var result = await _sut.ListOperationsAsync(orderId: 1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Select(o => o.OperationId).Should().Equal(1);
    }

    [Fact]
    public async Task ListOperations_returns_skill_level_of_operation()
    {
        var withLevel = Op(1, "OP10");
        withLevel.TeamType = TeamTypes.Sewing;
        withLevel.SkillLevel = 4;
        Op(2, "OP20").TeamType = TeamTypes.Sewing;   // chưa khai bậc

        var result = await _sut.ListOperationsAsync(orderId: 1, CancellationToken.None);

        result.Data!.Select(o => o.SkillLevel).Should().Equal((byte)4, null);
    }

    [Fact]
    public async Task ListOperations_ReturnsOnlyProductsAllottedToOwnTeam()
    {
        _lines.Add(new OrderLine { OrderLineId = 12, OrderId = 1, ProductId = 2 });
        Op(1, "OP10").TeamType = TeamTypes.Sewing;
        Op(2, "OP20", productId: 2).TeamType = TeamTypes.Sewing;

        var result = await _sut.ListOperationsAsync(orderId: 1, default);

        result.Data!.Select(o => o.OperationId).Should().Equal(1);
    }

    [Fact]
    public async Task ListOperations_OneColorSizeAllotted_IsEnoughForTheProduct()
    {
        _lines.Add(new OrderLine { OrderLineId = 12, OrderId = 1, ProductId = 1 });   // màu × size khác của cùng mã, không chia cho tổ
        Op(1, "OP10").TeamType = TeamTypes.Sewing;

        var result = await _sut.ListOperationsAsync(orderId: 1, default);

        result.Data!.Select(o => o.OperationId).Should().Equal(1);
    }

    [Fact]
    public async Task Assign_ProductNotAllottedToOwnTeam_IsRejected()
    {
        _lines.Add(new OrderLine { OrderLineId = 12, OrderId = 1, ProductId = 2 });
        Op(2, "OP20", productId: 2);
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 2, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
        _assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Copy_SkipsProductsNotAllottedToOwnTeam()
    {
        _allotted.RemoveAll(a => a.OrderLineId == 21);
        Current(50, 1, Op(1, "OP10"), Worker(7));

        (await _sut.CopyAsync(new CopyAssignmentsDto(1, 2), default)).Data.Should().Be(0);
    }

    private Employee Worker(int id, int teamId = TeamId, string status = RecordStatus.Active)
    {
        var e = new Employee { EmployeeId = id, EmployeeCode = "CN" + id, FullName = "Công nhân " + id, TeamId = teamId, Status = status,
            Role = new Role { RoleCode = RoleCodes.Worker } };
        _employees.Add(e);
        return e;
    }

    private OperationAssignment Current(int id, int orderId, Operation op, Employee worker, int teamId = TeamId,
        DateOnly? validTo = null)
    {
        var a = new OperationAssignment
        {
            OperationAssignmentId = id, SewingTeamId = teamId, OrderId = orderId, OperationId = op.OperationId,
            WorkerId = worker.EmployeeId, ValidFrom = Today.AddDays(-10), ValidTo = validTo, Operation = op, Worker = worker,
        };
        _assignments.Add(a);
        return a;
    }

    // ---------- Phân quyền ----------

    [Fact]
    public async Task Assign_ByCuttingTeamLeader_IsForbidden()
    {
        AsCuttingTeamLeader();
        Op(1);
        Worker(7);
        var service = new OperationAssignmentService(Uow, Guard(), Clock, Rates());

        var result = await service.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.Kind.Should().Be(ResultKind.Forbidden);
    }

    // ---------- Một công nhân, nhiều công đoạn ----------

    [Fact]
    public async Task AssignMany_OpensOneRowPerOperationForTheSameWorker()
    {
        Op(1, "OP10"); Op(2, "OP20"); Op(3, "OP30");
        Worker(7);

        var result = await _sut.AssignManyAsync(new AssignOperationsDto(1, 7, [1, 2, 3]), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(3);
        _assignments.Select(a => (a.OperationId, a.WorkerId)).Should().BeEquivalentTo(new[] { (1, 7), (2, 7), (3, 7) });
        _assignments.Should().OnlyContain(a => a.SewingTeamId == TeamId && a.ValidTo == null);
    }

    [Fact]
    public async Task AssignMany_SkipsOperationsAlreadyAssignedToThatWorker()
    {
        var op1 = Op(1, "OP10"); Op(2, "OP20");
        var worker = Worker(7);
        Current(50, 1, op1, worker);

        var result = await _sut.AssignManyAsync(new AssignOperationsDto(1, 7, [1, 2]), default);

        result.Data.Should().Be(1);
        _assignments.Should().HaveCount(2);
        _assignments.Count(a => a.OperationId == 1).Should().Be(1);
    }

    [Fact]
    public async Task AssignMany_SavesNothingWhenOneOperationIsInvalid()
    {
        Op(1, "OP10"); Op(2, "OP20", status: OperationStatuses.Discontinued);
        Worker(7);

        var result = await _sut.AssignManyAsync(new AssignOperationsDto(1, 7, [1, 2]), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.OperationNotActive);
        _assignments.Should().BeEmpty();
    }

    [Fact]
    public void AssignMany_Validator_RequiresOperationsWithoutDuplicates()
    {
        var validator = new NX_MTD.Application.Validators.AssignOperationsDtoValidator();

        validator.Validate(new AssignOperationsDto(1, 7, [1, 2])).IsValid.Should().BeTrue();
        validator.Validate(new AssignOperationsDto(1, 7, [])).IsValid.Should().BeFalse();
        validator.Validate(new AssignOperationsDto(1, 7, [1, 1])).IsValid.Should().BeFalse();
    }

    // ---------- Gán ----------

    [Fact]
    public async Task Assign_OperationWithoutApprovedRate_IsRejected()
    {
        Op(1, hasRate: false);
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        result.Message.Should().Contain("OP10");
        _assignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PieceRateStatuses.Draft)]
    [InlineData(PieceRateStatuses.PendingApproval)]
    [InlineData(PieceRateStatuses.Rejected)]
    public async Task Assign_OperationWhoseRateIsNotApproved_IsRejected(string status)
    {
        Op(1, hasRate: false);
        _rates.Add(TestRates.Approved(1, status));
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
    }

    [Fact]
    public async Task Assign_OperationWhoseRateStartsInTheFuture_IsRejected()
    {
        Op(1, hasRate: false);
        var future = TestRates.Approved(1);
        future.EffectiveDate = Today.AddDays(1);
        _rates.Add(future);
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
    }

    [Fact]
    public async Task AssignMany_OneOperationWithoutRate_SavesNothing()
    {
        Op(1);
        Op(2, "OP20", hasRate: false);
        Worker(7);

        var result = await _sut.AssignManyAsync(new AssignOperationsDto(1, 7, [1, 2]), default);

        result.ErrorCode.Should().Be(ErrorCodes.PieceRateNotApproved);
        result.Message.Should().Contain("OP20").And.NotContain("OP10");
        _assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Copy_SkipsOperationsWithoutApprovedRate()
    {
        var rated = Op(1);
        var unrated = Op(2, "OP20", hasRate: false);
        var worker = Worker(7);
        Current(50, 1, rated, worker);
        Current(51, 1, unrated, worker);

        var result = await _sut.CopyAsync(new CopyAssignmentsDto(1, 2), default);

        result.Data.Should().Be(1);
        _assignments.Where(a => a.OrderId == 2).Select(a => a.OperationId).Should().Equal(1);
    }

    [Fact]
    public async Task Assign_Valid_OpensRowFromTodayForOwnTeam()
    {
        Op(1);
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.IsSuccess.Should().BeTrue();
        var row = _assignments.Should().ContainSingle().Subject;
        row.SewingTeamId.Should().Be(TeamId);
        row.ValidFrom.Should().Be(Today);
        row.ValidTo.Should().BeNull();
        row.AssignedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Assign_SameWorkerSameOperationAgain_IsIdempotent()
    {
        var op = Op(1);
        var worker = Worker(7);
        var existing = Current(50, 1, op, worker);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.Data.Should().Be(existing.OperationAssignmentId);
        _assignments.Should().ContainSingle();   // không tạo dòng thứ hai (UX_OA_Current)
    }

    [Fact]
    public async Task Assign_WorkerFromAnotherTeam_IsRejected()
    {
        Op(1);
        Worker(7, teamId: 6);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
    }

    [Fact]
    public async Task Assign_ResignedWorker_IsRejected()
    {
        Op(1);
        Worker(7, status: RecordStatus.Resigned);

        (await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default)).ErrorCode
            .Should().Be(ErrorCodes.AssignmentWorkerInvalid);
    }

    [Fact]
    public async Task Assign_OperationOfProductNotInOrder_IsRejected()
    {
        Op(1, productId: 2);   // đơn 1 chỉ có mã hàng 1
        Worker(7);

        (await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default)).ErrorCode
            .Should().Be(ErrorCodes.OperationProductMismatch);
    }

    [Fact]
    public async Task Assign_DiscontinuedOperation_IsRejected()
    {
        Op(1, status: OperationStatuses.Discontinued);
        Worker(7);

        (await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default)).ErrorCode
            .Should().Be(ErrorCodes.OperationNotActive);
    }

    [Fact]
    public async Task Assign_ClosedOrUnknownOrder_IsRejected()
    {
        Op(1);
        Worker(7);

        (await _sut.AssignAsync(new AssignOperationDto(3, 1, 7), default)).ErrorCode
            .Should().Be(ErrorCodes.AssignmentOrderInvalid);   // đơn đã hủy
        (await _sut.AssignAsync(new AssignOperationDto(99, 1, 7), default)).ErrorCode
            .Should().Be(ErrorCodes.AssignmentOrderInvalid);
    }

    // ---------- Đổi phân công = đóng dòng cũ, giữ lịch sử ----------

    [Fact]
    public async Task Replace_ClosesOldRowAndOpensNewOneForNewWorker()
    {
        var op = Op(1);
        var old = Worker(7);
        Worker(8);
        var oldRow = Current(50, 1, op, old);

        var result = await _sut.ReplaceAsync(50, new ReplaceAssignmentDto(8), default);

        result.IsSuccess.Should().BeTrue();
        oldRow.ValidTo.Should().Be(Today);   // dòng cũ còn đó để xem lịch sử
        _assignments.Should().HaveCount(2);
        var fresh = _assignments.Single(a => a.WorkerId == 8);
        fresh.ValidTo.Should().BeNull();
        fresh.OperationId.Should().Be(1);
        fresh.OrderId.Should().Be(1);
    }

    [Fact]
    public async Task Replace_WithSameWorker_ChangesNothing()
    {
        var op = Op(1);
        var worker = Worker(7);
        var row = Current(50, 1, op, worker);

        await _sut.ReplaceAsync(50, new ReplaceAssignmentDto(7), default);

        row.ValidTo.Should().BeNull();
        _assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Replace_AlreadyClosedOrOtherTeamsRow_IsNotFound()
    {
        var op = Op(1);
        var worker = Worker(7);
        Worker(8);
        Current(50, 1, op, worker, validTo: Today.AddDays(-1));   // đã đóng
        Current(51, 1, op, worker, teamId: 6);                    // của tổ khác

        (await _sut.ReplaceAsync(50, new ReplaceAssignmentDto(8), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.ReplaceAsync(51, new ReplaceAssignmentDto(8), default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Replace_ToWorkerOutsideTheTeam_KeepsOldRowOpen()
    {
        var op = Op(1);
        var worker = Worker(7);
        Worker(8, teamId: 6);
        var row = Current(50, 1, op, worker);

        var result = await _sut.ReplaceAsync(50, new ReplaceAssignmentDto(8), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
        row.ValidTo.Should().BeNull();
    }

    // ---------- Kết thúc phân công ----------

    [Fact]
    public async Task Assign_CuttingOperation_IsRejected()
    {
        Op(1).TeamType = TeamTypes.Cutting;
        Worker(7);

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentOperationNotSewing);
        _assignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RoleCodes.TeamLeader)]
    [InlineData(RoleCodes.TeamDeputy)]
    public async Task Assign_LeaderOfOwnTeam_IsRejected(string roleCode)
    {
        Op(1);
        Worker(7).Role.RoleCode = roleCode;

        var result = await _sut.AssignAsync(new AssignOperationDto(1, 1, 7), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentWorkerInvalid);
        _assignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Lists_InvalidOrderId_IsAValidationError(int orderId)
    {
        (await _sut.ListCurrentAsync(orderId, default)).Kind.Should().Be(ResultKind.Validation);
        (await _sut.ListOperationsAsync(orderId, default)).Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task Close_RowOfClosedOrder_IsRejectedAndKeepsRowOpen()
    {
        var row = Current(50, 3, Op(1), Worker(7));   // đơn 3 đã hủy

        var result = await _sut.CloseAsync(50, default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);
        row.ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Copy_SkipsCuttingOperationsAndLeaders()
    {
        var cutting = Op(1);
        cutting.TeamType = TeamTypes.Cutting;
        Current(50, 1, cutting, Worker(7));
        var leader = Worker(8);
        leader.Role.RoleCode = RoleCodes.TeamLeader;
        Current(51, 1, Op(2, "OP20"), leader);

        (await _sut.CopyAsync(new CopyAssignmentsDto(1, 2), default)).Data.Should().Be(0);
    }

    [Fact]
    public async Task Close_SetsValidToToday()
    {
        var op = Op(1);
        var row = Current(50, 1, op, Worker(7));

        var result = await _sut.CloseAsync(50, default);

        result.IsSuccess.Should().BeTrue();
        row.ValidTo.Should().Be(Today);
    }

    // ---------- Danh sách hiện hành ----------

    [Fact]
    public async Task ListCurrent_ShowsOnlyOwnTeamsOpenRowsOfThatOrder()
    {
        var op = Op(1);
        Current(50, 1, op, Worker(7));                                   // hiện hành
        Current(51, 1, op, Worker(8), validTo: Today.AddDays(-2));       // đã đóng
        Current(52, 1, op, Worker(9), teamId: 6);                        // tổ khác
        Current(53, 2, op, Worker(10));                                  // đơn khác

        var result = await _sut.ListCurrentAsync(1, default);

        result.Data!.Should().ContainSingle().Which.WorkerId.Should().Be(7);
    }

    // ---------- Chép phân công ----------

    [Fact]
    public async Task Copy_CopiesOpenRowsToNewOrder_SkippingExistingAndLeavers()
    {
        var op = Op(1);
        var w7 = Worker(7);
        var w8 = Worker(8);
        var gone = Worker(9, status: RecordStatus.Resigned);
        var moved = Worker(10, teamId: 6);
        var src7 = Current(50, 1, op, w7);
        Current(51, 1, op, w8);
        Current(52, 1, op, gone);
        Current(53, 1, op, moved);
        Current(60, 2, op, w8);   // đơn đích đã có w8 → bỏ qua

        var result = await _sut.CopyAsync(new CopyAssignmentsDto(1, 2), default);

        result.Data.Should().Be(1);   // chỉ w7: w8 đã có, w9 đã nghỉ, w10 đã sang tổ khác
        var copy = _assignments.Single(a => a.OrderId == 2 && a.WorkerId == 7);
        copy.ValidFrom.Should().Be(Today);
    }

    [Fact]
    public async Task Copy_OnlyOperationsWhoseProductIsInTargetOrder()
    {
        var other = Op(2, "OP20", productId: 2);   // mã hàng 2 không có trong đơn đích
        Current(50, 1, other, Worker(7));

        var result = await _sut.CopyAsync(new CopyAssignmentsDto(1, 2), default);

        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task Copy_SameOrder_IsRejected()
    {
        (await _sut.CopyAsync(new CopyAssignmentsDto(1, 1), default)).Kind.Should().Be(ResultKind.Validation);
    }
}
