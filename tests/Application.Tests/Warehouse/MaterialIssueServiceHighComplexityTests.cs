using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID252–UTCID291: MaterialIssueService độ phức tạp cao (UNIT_TEST_CASES_HIGH_COMPLEXITY_SERVICES.md).</summary>
public sealed class MaterialIssueServiceHighComplexityTests : ProductionTestBase
{
    private const int Team3 = 3;
    private const int Team5 = 5;
    private const int OrderId = 1;
    private const int LineId = 100;
    private const int ProductId = 5;
    private const int MaterialM1 = 1;
    private const int Request30 = 30;
    private const int Request31 = 31;
    private const int Issue50 = 50;
    private const int IssuedBy = 7;
    private const int Receiver = 12;

    private static readonly DateTime SpecNow = new(2026, 11, 22, 10, 0, 0);
    private static readonly DateOnly SpecToday = new(2026, 11, 22);

    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderLineTeam> _allotted = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Request> _requestRows = [];
    private readonly List<Material> _materials = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<MaterialIssue> _issues = [];
    private readonly List<Operation> _operations = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<Employee> _employees = [];
    private readonly MaterialIssueService _sut;
    private int _nextLineId = 500;
    private int _nextIssueId = 600;

    public MaterialIssueServiceHighComplexityTests()
    {
        Clock.Now.Returns(SpecNow);
        Clock.Today.Returns(SpecToday);

        SetupQueryable(_teams);
        SetupQueryable(_lines);
        SetupQueryable(_allotted);
        SetupQueryable(_orders);
        SetupQueryable(_requestRows);
        SetupQueryable(_materials);
        SetupQueryable(_orderMaterials);
        SetupQueryable(_issues);
        SetupQueryable(_operations);
        SetupQueryable(_assignments);
        SetupQueryable(_employees);

        Uow.BeginTransactionAsync().Returns(Task.CompletedTask);
        Uow.CommitTransactionAsync().Returns(Task.CompletedTask);
        Uow.RollbackTransactionAsync().Returns(Task.CompletedTask);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));

        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<MaterialIssue>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialIssue, string>>>(),
            Arg.Any<CancellationToken>()).Returns("PX2611-001");
        _requests.MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        Notifier.NotifyTeamLeadersAsync(Arg.Any<int>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        SeedTeamsAndOrder();
        CaptureIssueAdds();
        _sut = new MaterialIssueService(Uow, _stock, new FabricPlanner(Uow), _codes, Clock,
            NullLogger<MaterialIssueService>.Instance, _requests, Audit, Notifier);
    }

    /// <summary>Giả lập EF: AddAsync gán Id + navigation rồi đưa vào list để Create → GetById thấy phiếu.</summary>
    private void CaptureIssueAdds()
    {
        Uow.Repository<MaterialIssue>().AddAsync(Arg.Do<MaterialIssue>(issue =>
        {
            if (issue.MaterialIssueId == 0)
                issue.MaterialIssueId = ++_nextIssueId;

            issue.IssuedByNavigation ??= _employees.FirstOrDefault(e => e.EmployeeId == issue.IssuedBy) ?? _employees[0];
            issue.ReceivingTeam ??= _teams.SingleOrDefault(t => t.TeamId == issue.ReceivingTeamId);
            issue.Order ??= _orders.SingleOrDefault(o => o.OrderId == issue.OrderId) ?? _orders[0];

            var lineSeq = 0;
            foreach (var line in issue.MaterialIssueLines)
            {
                if (line.MaterialIssueLineId == 0)
                    line.MaterialIssueLineId = issue.MaterialIssueId * 10 + ++lineSeq;
                line.MaterialIssue = issue;
                line.MaterialIssueId = issue.MaterialIssueId;
                if (line.MaterialId is { } mid)
                    line.Material ??= _materials.SingleOrDefault(m => m.MaterialId == mid);
                if (line.OrderLineId is { } olId)
                    line.OrderLine ??= _lines.SingleOrDefault(l => l.OrderLineId == olId);
            }

            if (_issues.All(i => i.MaterialIssueId != issue.MaterialIssueId))
                _issues.Add(issue);
        })).Returns(Task.CompletedTask);
    }

    private void SeedTeamsAndOrder()
    {
        _teams.AddRange(
            new Team { TeamId = Team3, TeamCode = "T03", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
            new Team { TeamId = Team5, TeamCode = "T05", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active });
        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH2611", Status = OrderStatuses.InProduction, ReceivedDate = SpecToday });
        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = ProductId }.WithVariant(TestVariant.Make(ProductId, "MH5")));
        _allotted.Add(new OrderLineTeam { OrderLineId = LineId, SewingTeamId = Team3 });
        _materials.Add(new Material { MaterialId = MaterialM1, MaterialCode = "M1", MaterialName = "Vải M1", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active, Unit = "m" });
        _orderMaterials.Add(new OrderMaterial { OrderId = OrderId, MaterialId = MaterialM1 });
        _employees.Add(new Employee { EmployeeId = IssuedBy, FullName = "Thủ kho A", Status = RecordStatus.Active });
    }

    private MaterialIssue AddIssue(int id, int teamId, string status = IssueStatuses.PendingReceipt, string warehouse = WarehouseTypes.Material,
        int? requestId = null, int materialId = MaterialM1, decimal issued = 100, decimal? confirmed = null)
    {
        var issue = new MaterialIssue
        {
            MaterialIssueId = id, IssueCode = $"PX-{id}", WarehouseType = warehouse, OrderId = OrderId,
            ReceivingTeamId = teamId, RequestId = requestId, Status = status, IssueDate = SpecToday,
            IssuedBy = IssuedBy, IssuedByNavigation = _employees[0],
            ReceivingTeam = _teams.Single(t => t.TeamId == teamId),
            Order = _orders[0],
            MaterialIssueLines =
            [
                new MaterialIssueLine
                {
                    MaterialIssueLineId = id * 10, MaterialId = materialId, IssuedQty = issued, ConfirmedQty = confirmed,
                    Material = _materials.Single(m => m.MaterialId == materialId),
                },
            ],
        };
        _issues.Add(issue);
        return issue;
    }

    private void AddSemiIssue(int id, int teamId, string status = IssueStatuses.PendingReceipt)
    {
        var issue = new MaterialIssue
        {
            MaterialIssueId = id, IssueCode = $"BTP-{id}", WarehouseType = WarehouseTypes.SemiFinished, OrderId = OrderId,
            ReceivingTeamId = teamId, Status = status, IssueDate = SpecToday, IssuedBy = IssuedBy,
            IssuedByNavigation = _employees[0], ReceivingTeam = _teams.Single(t => t.TeamId == teamId), Order = _orders[0],
            MaterialIssueLines = [new MaterialIssueLine { MaterialIssueLineId = id * 10, OrderLineId = LineId, IssuedQty = 50 }],
        };
        _issues.Add(issue);
    }

    private void GivenRequest(int id, string status, decimal qty = 100, string type = RequestTypes.MaterialRequisition, int teamId = Team3)
    {
        _requestRows.Add(new Request
        {
            RequestId = id, RequestTypeCode = type, Status = status, CreatedByTeamId = teamId, OrderId = OrderId,
            RequestLines = type == RequestTypes.MaterialRequisition
                ? [new RequestLine { MaterialId = MaterialM1, Quantity = qty, OrderLineId = null }]
                : [new RequestLine { OrderLineId = LineId, Quantity = qty }],
        });
    }

    private CreateMaterialIssueDto MaterialDto(int? requestId, decimal qty, int teamId = Team3) =>
        new(WarehouseTypes.Material, null, OrderId, teamId, requestId, SpecToday, [new CreateIssueLineDto(MaterialM1, null, null, qty)]);

    private CreateMaterialIssueDto SemiDto(int? requestId = null, decimal qty = 50, int teamId = Team3) =>
        new(WarehouseTypes.SemiFinished, null, OrderId, teamId, requestId, SpecToday, [new CreateIssueLineDto(null, LineId, null, qty)]);

    private void AssignAllOperations(int teamId = Team3)
    {
        for (var i = 1; i <= 2; i++)
        {
            var opId = 200 + i;
            if (_operations.All(o => o.OperationId != opId))
                _operations.Add(new Operation { OperationId = opId, ProductId = ProductId, OperationCode = $"OP{i}", StationSequence = (short)i, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing });
            _assignments.Add(new OperationAssignment
            {
                OperationId = opId, OrderId = OrderId, SewingTeamId = teamId, ValidTo = null,
                Worker = new Employee { Status = RecordStatus.Active, TeamId = teamId },
            });
        }
    }

    private ConfirmIssueDto ConfirmAll(MaterialIssue issue, decimal confirmed, string? reason = null) =>
        new(issue.MaterialIssueLines.Select(l => new ConfirmIssueLineDto(l.MaterialIssueLineId, confirmed, reason)).ToList());


    [Fact]
    public async Task UTCID252_GetPagedAsync_WarehouseKeeper_ReturnsAllIssues()
    {

        // Arrange
        AddIssue(1, Team3, status: IssueStatuses.PendingReceipt);
        AddIssue(2, Team3, status: IssueStatuses.Received);
        AddIssue(3, Team5, status: IssueStatuses.PendingReceipt);
        AddIssue(4, Team5, status: IssueStatuses.Rejected);

        // Act
        var page = (await _sut.GetPagedAsync(new MaterialIssueQuery { Page = 1, PageSize = 10 }, null, default)).Data!;

        // Assert
        page.Items.Should().HaveCount(4);
        page.TotalItems.Should().Be(4);

    }

    [Fact]
    public async Task UTCID253_GetPagedAsync_FilterByTeamAndStatus()
    {

        // Arrange
        AddIssue(1, Team3, status: IssueStatuses.PendingReceipt);
        AddIssue(2, Team3, status: IssueStatuses.Received);
        AddIssue(3, Team5, status: IssueStatuses.PendingReceipt);
        AddIssue(4, Team5, status: IssueStatuses.Rejected);

        // Act
        var page = (await _sut.GetPagedAsync(new MaterialIssueQuery { Page = 1, PageSize = 10, Status = IssueStatuses.PendingReceipt }, Team3, default)).Data!;

        // Assert
        page.Items.Should().HaveCount(1);
        page.Items.Single().ReceivingTeamId.Should().Be(Team3);
        page.Items.Single().Status.Should().Be(IssueStatuses.PendingReceipt);

    }

    [Fact]
    public async Task UTCID254_GetPagedAsync_EmptyDb_ReturnsEmpty()
    {

        // Act
        var page = (await _sut.GetPagedAsync(new MaterialIssueQuery { Page = 1, PageSize = 10 }, null, default)).Data!;

        // Assert
        page.Items.Should().BeEmpty();
        page.TotalItems.Should().Be(0);

    }

    [Fact]
    public async Task UTCID255_GetPagedAsync_PageBeyondRange_ReturnsEmptyItems()
    {

        // Arrange
        for (var i = 1; i <= 4; i++) AddIssue(i, Team3);

        // Act
        var page = (await _sut.GetPagedAsync(new MaterialIssueQuery { Page = 3, PageSize = 2 }, null, default)).Data!;

        // Assert
        page.Items.Should().BeEmpty();
        page.TotalItems.Should().Be(4);

    }

    [Fact]
    public async Task UTCID256_GetPagedAsync_NullNavigation_ReturnsNullFieldsSafely()
    {

        // Arrange
        _issues.Add(new MaterialIssue
        {
            MaterialIssueId = 60, IssueCode = "PX-ORPHAN", WarehouseType = WarehouseTypes.Material,
            OrderId = OrderId, ReceivingTeamId = Team3, Status = IssueStatuses.PendingReceipt, IssueDate = SpecToday,
            IssuedBy = IssuedBy, IssuedByNavigation = null, Order = null,
            ReceivingTeam = _teams[0],
            MaterialIssueLines = [],
        });

        // Act
        var act = async () => await _sut.GetPagedAsync(new MaterialIssueQuery { Page = 1, PageSize = 10 }, null, default);

        // Assert
        await act.Should().NotThrowAsync<NullReferenceException>();

    }

    [Fact]
    public async Task UTCID257_GetByIdAsync_MatchingTeam_ReturnsDto()
    {

        // Arrange
        AddIssue(Issue50, Team3);
        AddIssue(Issue50 + 1, Team5);

        // Act
        var result = await _sut.GetByIdAsync(Issue50, Team3, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Lines.Should().NotBeEmpty();

    }

    [Fact]
    public async Task UTCID258_GetByIdAsync_NullTeam_Succeeds()
    {

        // Arrange
        AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.GetByIdAsync(Issue50, null, default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID259_GetByIdAsync_WrongTeam_ReturnsTeamMismatch()
    {

        // Arrange
        AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.GetByIdAsync(Issue50, Team5, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);

    }

    [Fact]
    public async Task UTCID260_GetByIdAsync_NotFound_ReturnsFailure()
    {

        // Act
        var result = await _sut.GetByIdAsync(999, Team3, default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);

    }

    [Fact]
    public async Task UTCID261_CreateAsync_ApprovedRequest_CreatesPendingReceipt()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved);

        // Act
        var result = await _sut.CreateAsync(MaterialDto(Request30, 100), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(IssueStatuses.PendingReceipt);
        await _stock.Received(1).EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>());
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
        await _requests.DidNotReceive().MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task UTCID262_CreateAsync_SemiFinishedWithAssignments_Succeeds()
    {

        // Arrange
        AssignAllOperations(Team3);

        // Act
        var result = await _sut.CreateAsync(SemiDto(), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID263_CreateAsync_ExactStock_Succeeds()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved);
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var req = call.Arg<IReadOnlyList<StockRequirement>>().Single();
                return req.Qty <= 100 ? Result.Success() : Result.Failure(ErrorCodes.WarehouseInsufficientStock, WarehouseMessages.InsufficientStock);
            });

        // Act
        var result = await _sut.CreateAsync(MaterialDto(Request30, 100), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID264_CreateAsync_InsufficientStock_ReturnsFailure()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved);
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, WarehouseMessages.InsufficientStock));

        // Act
        var result = await _sut.CreateAsync(MaterialDto(Request30, 101), IssuedBy, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        await Uow.Repository<MaterialIssue>().DidNotReceiveWithAnyArgs().AddAsync(default!);

    }

    [Fact]
    public async Task UTCID265_CreateAsync_EmptyLines_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.CreateAsync(new CreateMaterialIssueDto(WarehouseTypes.Material, null, OrderId, Team3, null, SpecToday, []), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID266_CreateAsync_OverRequestQty_WarnsAtUiOnly_Succeeds()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved, qty: 100);

        // Act
        var result = await _sut.CreateAsync(MaterialDto(Request30, 120), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeTrue();

    }

    [Fact]
    public async Task UTCID267_CreateAsync_ZeroQty_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.CreateAsync(MaterialDto(null, 0), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID268_CreateAsync_NegativeQty_ReturnsValidationFailure()
    {

        // Act
        var result = await _sut.CreateAsync(MaterialDto(null, -5), IssuedBy, default);

        // Assert
        result.IsSuccess.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID269_CreateAsync_RequestNotApproved_ReturnsInvalidStatus()
    {

        // Arrange
        GivenRequest(Request31, RequestStatuses.PendingApproval);

        // Act
        var result = await _sut.CreateAsync(MaterialDto(Request31, 10), IssuedBy, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);

    }

    [Fact]
    public async Task UTCID270_CreateAsync_RequestNotFound_ReturnsFailure()
    {

        // Act
        var result = await _sut.CreateAsync(MaterialDto(999, 10), IssuedBy, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);

    }

    [Fact]
    public async Task UTCID271_CreateAsync_BtpWithoutOperationAssignment_ReturnsFailure()
    {

        // Act
        var result = await _sut.CreateAsync(SemiDto(), IssuedBy, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.AssignmentIncomplete);

    }

    [Fact]
    public async Task UTCID272_CreateAsync_BtpLinesWithoutOrderLineId_BypassesAssignmentCheck()
    {

        // Arrange
        var dto = new CreateMaterialIssueDto(WarehouseTypes.SemiFinished, null, OrderId, Team3, null, SpecToday,
            [new CreateIssueLineDto(null, null, null, 5)]);

        // Act
        var result = await _sut.CreateAsync(dto, IssuedBy, default);

        // Assert
        result.ErrorCode.Should().NotBe(ErrorCodes.AssignmentIncomplete);

    }

    [Fact]
    public async Task UTCID273_ConfirmAsync_PendingMaterialIssue_ReceivesAndCompletesRequest()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved);
        var issue = AddIssue(Issue50, Team3, requestId: Request30);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 100), Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Received);
        await _stock.Received(1).PostAsync(Arg.Is<IReadOnlyList<StockMovement>>(m => m.Single().QtyChange == -100), Arg.Any<CancellationToken>());
        await _requests.Received(1).MarkCompletedAsync(Request30, issue.IssueCode, Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task UTCID274_ConfirmAsync_SemiFinished_StartsProduction()
    {

        // Arrange
        AssignAllOperations(Team3);
        _orders[0].Status = OrderStatuses.New;
        _orders[0].MaterialStatus = OrderMaterialStatuses.Complete;
        var issue = new MaterialIssue
        {
            MaterialIssueId = Issue50, IssueCode = "BTP-50", WarehouseType = WarehouseTypes.SemiFinished,
            OrderId = OrderId, ReceivingTeamId = Team3, Status = IssueStatuses.PendingReceipt, IssueDate = SpecToday,
            MaterialIssueLines = [new MaterialIssueLine { MaterialIssueLineId = 501, OrderLineId = LineId, IssuedQty = 50 }],
        };
        _issues.Add(issue);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 50), Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _orders[0].Status.Should().Be(OrderStatuses.InProduction);

    }

    [Fact]
    public async Task UTCID275_ConfirmAsync_DiscrepancyWithReason_Succeeds()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 99, "Thiếu 1 cuộn"), Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.MaterialIssueLines.Single().ConfirmedQty.Should().Be(99);
        await _stock.Received(1).PostAsync(Arg.Is<IReadOnlyList<StockMovement>>(m => m.Single().QtyChange == -99), Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task UTCID276_ConfirmAsync_DiscrepancyWithoutReason_ReturnsFailure()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 99), Team3, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);

    }

    [Fact]
    public async Task UTCID277_ConfirmAsync_ConfirmedExceedsIssued_ReturnsFailure()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 101), Team3, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);

    }

    [Fact]
    public async Task UTCID278_ConfirmAsync_AllZeroConfirmed_ShouldNotReceive()
    {

        // Arrange
        AssignAllOperations(Team3);
        _orders[0].Status = OrderStatuses.New;
        var issue = new MaterialIssue
        {
            MaterialIssueId = Issue50, IssueCode = "BTP-50", WarehouseType = WarehouseTypes.SemiFinished,
            OrderId = OrderId, ReceivingTeamId = Team3, Status = IssueStatuses.PendingReceipt,
            MaterialIssueLines = [new MaterialIssueLine { MaterialIssueLineId = 501, OrderLineId = LineId, IssuedQty = 50 }],
        };
        _issues.Add(issue);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 0, "Không nhận"), Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        issue.Status.Should().Be(IssueStatuses.PendingReceipt);
        _orders[0].Status.Should().Be(OrderStatuses.New);

    }

    [Fact]
    public async Task UTCID279_ConfirmAsync_AwaitingMaterial_DoesNotStartProduction()
    {

        // Arrange
        AssignAllOperations(Team3);
        _orders[0].Status = OrderStatuses.New;
        _orders[0].MaterialStatus = OrderMaterialStatuses.Awaiting;
        var issue = new MaterialIssue
        {
            MaterialIssueId = Issue50, IssueCode = "BTP-50", WarehouseType = WarehouseTypes.SemiFinished,
            OrderId = OrderId, ReceivingTeamId = Team3, Status = IssueStatuses.PendingReceipt,
            MaterialIssueLines = [new MaterialIssueLine { MaterialIssueLineId = 501, OrderLineId = LineId, IssuedQty = 50 }],
        };
        _issues.Add(issue);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 50), Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _orders[0].Status.Should().Be(OrderStatuses.New);

    }

    [Fact]
    public async Task UTCID280_ConfirmAsync_NegativeConfirmed_ReturnsValidationFailure()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);
        var dto = new ConfirmIssueDto([new ConfirmIssueLineDto(issue.MaterialIssueLines.Single().MaterialIssueLineId, -1, null)]);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, dto, Team3, Receiver, default);

        // Assert
        result.IsSuccess.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID281_ConfirmAsync_AlreadyReceived_ReturnsInvalidStatus()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3, status: IssueStatuses.Received);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 100), Team3, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);

    }

    [Fact]
    public async Task UTCID282_ConfirmAsync_WrongTeam_ReturnsTeamMismatch()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 100), Team5, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);

    }

    [Fact]
    public async Task UTCID283_ConfirmAsync_MissingLine_ReturnsFailure()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);
        issue.MaterialIssueLines.Add(new MaterialIssueLine { MaterialIssueLineId = _nextLineId++, MaterialId = MaterialM1, IssuedQty = 10, Material = _materials[0] });
        var firstLineId = issue.MaterialIssueLines.First().MaterialIssueLineId;
        var partial = new ConfirmIssueDto([new ConfirmIssueLineDto(firstLineId, 100, null)]);

        // Act
        var result = await _sut.ConfirmAsync(Issue50, partial, Team3, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);

    }

    [Fact]
    public async Task UTCID284_ConfirmAsync_StockPostFails_RollsBackPending()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, WarehouseMessages.InsufficientStock));

        // Act
        var result = await _sut.ConfirmAsync(Issue50, ConfirmAll(issue, 100), Team3, Receiver, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        issue.Status.Should().Be(IssueStatuses.PendingReceipt);
        await Uow.Received().RollbackTransactionAsync();

    }

    [Fact]
    public async Task UTCID285_ConfirmAsync_NotFound_ReturnsFailure()
    {

        // Act
        var result = await _sut.ConfirmAsync(999, new ConfirmIssueDto([new ConfirmIssueLineDto(1, 1, null)]), Team3, Receiver, default);

        // Assert
        result.Kind.Should().Be(ResultKind.NotFound);

    }

    [Fact]
    public async Task UTCID286_RejectAsync_PendingIssue_RejectsAndNotifies()
    {

        // Arrange
        GivenRequest(Request30, RequestStatuses.Approved);
        var issue = AddIssue(Issue50, Team3, requestId: Request30);
        _requestRows[0].Status = RequestStatuses.Approved;

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto("Sai mã NVL"), Team3, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Rejected);
        _requestRows[0].Status.Should().Be(RequestStatuses.Approved);
        Notifier.Received(1).NotifyEmployees(Arg.Is<IEnumerable<int>>(ids => ids.Contains(IssuedBy)), Arg.Any<NotificationMessage>());
        await Uow.Received().SaveChangesAsync();

    }

    [Fact]
    public async Task UTCID287_RejectAsync_EmptyReason_ReturnsValidationFailure()
    {

        // Arrange
        AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto(""), Team3, default);

        // Assert
        result.IsSuccess.Should().BeFalse();

    }

    [Fact]
    public async Task UTCID288_RejectAsync_NullReason_ThrowsOrValidationFailure()
    {

        // Arrange
        AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto(null!), Team3, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);

    }

    [Fact]
    public async Task UTCID289_RejectAsync_WrongTeam_ReturnsTeamMismatch()
    {

        // Arrange
        AddIssue(Issue50, Team3);

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto("x"), Team5, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);

    }

    [Fact]
    public async Task UTCID290_RejectAsync_AlreadyReceived_ReturnsInvalidStatus()
    {

        // Arrange
        AddIssue(Issue50, Team3, status: IssueStatuses.Received);

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto("x"), Team3, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);

    }

    [Fact]
    public async Task UTCID291_RejectAsync_NotifierThrows_IsHandled()
    {

        // Arrange
        var issue = AddIssue(Issue50, Team3);
        Notifier.When(n => n.NotifyEmployees(Arg.Any<IEnumerable<int>>(), Arg.Any<NotificationMessage>()))
            .Do(_ => throw new InvalidOperationException("notify failed"));

        // Act
        var result = await _sut.RejectAsync(Issue50, new ReasonDto("x"), Team3, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Rejected);

    }

}
