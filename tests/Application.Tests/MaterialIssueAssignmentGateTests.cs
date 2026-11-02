using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-48: tổ may phải phân công đủ công đoạn của mã hàng trên phiếu trước khi nhận BTP.</summary>
public class MaterialIssueAssignmentGateTests : ProductionTestBase
{
    private const int TeamId = 5, OrderId = 1, LineId = 11, ProductId = 7, IssueLineId = 100;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<MaterialIssue> _issues = [];
    private readonly List<Operation> _operations = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly MaterialIssueService _sut;

    public MaterialIssueAssignmentGateTests()
    {
        SetupQueryable(_issues); SetupQueryable(_operations); SetupQueryable(_assignments);
        SetupQueryable(new List<OrderLine> { new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = ProductId }.WithVariant(TestVariant.Make(ProductId, "MH7")) });
        SetupQueryable(new List<CustomerOrder> { new() { OrderId = OrderId, Status = OrderStatuses.InProduction } });
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sut = new MaterialIssueService(Uow, _stock, new FabricPlanner(Uow), Substitute.For<IDocumentCodeGenerator>(), Clock,
            NullLogger<MaterialIssueService>.Instance, Substitute.For<IRequestService>(), Audit, Notifier);
    }

    private MaterialIssue Issue(string warehouse = WarehouseTypes.SemiFinished)
    {
        var issue = new MaterialIssue { MaterialIssueId = 1, OrderId = OrderId, ReceivingTeamId = TeamId, WarehouseType = warehouse, Status = IssueStatuses.PendingReceipt };
        issue.MaterialIssueLines.Add(new MaterialIssueLine { MaterialIssueLineId = IssueLineId, OrderLineId = LineId, IssuedQty = 5 });
        _issues.Add(issue);
        return issue;
    }

    private void Op(int id, string code, int productId = ProductId, string status = OperationStatuses.Active, string team = TeamTypes.Sewing) =>
        _operations.Add(new Operation { OperationId = id, ProductId = productId, OperationCode = code, StationSequence = (short)id, Status = status, TeamType = team });

    private void Assign(int operationId, int orderId = OrderId, int team = TeamId, DateOnly? validTo = null, string workerStatus = RecordStatus.Active, int workerTeam = TeamId) =>
        _assignments.Add(new OperationAssignment
        {
            OperationId = operationId, OrderId = orderId, SewingTeamId = team, ValidTo = validTo,
            Worker = new Employee { Status = workerStatus, TeamId = workerTeam },
        });

    private Task<Result> Receive() =>
        _sut.ConfirmAsync(1, new ConfirmIssueDto([new ConfirmIssueLineDto(IssueLineId, 5, null)]), TeamId, 900, default);

    [Fact]
    public async Task ConfirmAsync_AllOperationsAssigned_Receives()
    {
        var issue = Issue();
        Op(1, "OP1"); Op(2, "OP2");
        Assign(1); Assign(2);

        (await Receive()).IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Received);
    }

    [Fact]
    public async Task ConfirmAsync_MissingOperation_IsBlockedAndNamesIt()
    {
        var issue = Issue();
        Op(1, "OP1"); Op(2, "OP2");
        Assign(1);

        var result = await Receive();

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentIncomplete);
        result.Message.Should().Contain("1 công đoạn").And.Contain("OP2").And.NotContain("OP1");
        issue.Status.Should().Be(IssueStatuses.PendingReceipt);
        await _stock.DidNotReceiveWithAnyArgs().PostAsync(default!, default);
    }

    [Fact]
    public async Task ConfirmAsync_LongList_IsShortenedButCountsAll()
    {
        Issue();
        for (var i = 1; i <= 7; i++) Op(i, $"OP{i}");

        (await Receive()).Message.Should().Contain("7 công đoạn").And.Contain("OP5...").And.NotContain("OP6");
    }

    [Fact]
    public async Task ConfirmAsync_AssignmentsThatDoNotCount_StillBlock()
    {
        Issue();
        Op(1, "OP1");
        Assign(1, orderId: 99);                                     // đơn khác
        Assign(1, team: 99);                                        // tổ khác
        Assign(1, validTo: new DateOnly(2026, 1, 1));               // đã bỏ phân công
        Assign(1, workerStatus: RecordStatus.Discontinued);         // công nhân đã nghỉ
        Assign(1, workerTeam: 99);                                  // công nhân đã chuyển tổ

        (await Receive()).ErrorCode.Should().Be(ErrorCodes.AssignmentIncomplete);
    }

    [Fact]
    public async Task ConfirmAsync_OnlyChecksActiveSewingOperationsOfTheProductsOnTheIssue()
    {
        Issue();
        Op(1, "OP1");
        Assign(1);
        Op(2, "KHAC", productId: 8);
        Op(3, "CU", status: OperationStatuses.Discontinued);
        Op(4, "CAT", team: TeamTypes.Cutting);

        (await Receive()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_ProductWithoutOperations_IsBlockedAndNamesTheProduct()
    {
        var issue = Issue();

        var result = await Receive();

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentIncomplete);
        result.Message.Should().Be(string.Format(Messages.AssignmentNoOperations, "MH7"));
        issue.Status.Should().Be(IssueStatuses.PendingReceipt);
    }

    [Fact]
    public async Task ConfirmAsync_ProductWithOnlyInactiveOrCuttingOperations_IsBlocked()
    {
        Issue();
        Op(1, "CU", status: OperationStatuses.Discontinued);
        Op(2, "CAT", team: TeamTypes.Cutting);

        (await Receive()).Message.Should().Be(string.Format(Messages.AssignmentNoOperations, "MH7"));
    }

    [Fact]
    public async Task ConfirmAsync_FabricIssueOfTheCuttingTeam_IsNotChecked()
    {
        var issue = Issue(WarehouseTypes.Material);
        issue.MaterialIssueLines.Single().MaterialId = 3;
        issue.MaterialIssueLines.Single().OrderLineId = null;
        Op(1, "OP1");

        (await Receive()).IsSuccess.Should().BeTrue();
    }
}
