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

/// <summary>TH-05 / TH-08 (UC40): cấp BTP cho tổ may gắn đơn Xin BTP; đơn xin chỉ hoàn thành khi tổ nhận phiếu cấp (DEV-ALL-109).</summary>
public class MaterialIssueRequisitionTests : ProductionTestBase
{
    private const int SewingTeamId = 5;
    private const int OrderId = 1;
    private const int LineId = 11;
    private const int RequestId = 70;

    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly List<Team> _teams = [new Team { TeamId = SewingTeamId, TeamType = TeamTypes.Sewing, Status = RecordStatus.Active }];
    private readonly List<OrderLine> _lines = [new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = 3 }.WithVariant(TestVariant.Make(3, "MH3"))];
    private readonly List<OrderLineTeam> _allotted = [new OrderLineTeam { OrderLineId = LineId, SewingTeamId = SewingTeamId }];
    private readonly List<CustomerOrder> _orders = [new CustomerOrder { OrderId = OrderId, Status = OrderStatuses.InProduction }];
    private readonly List<Request> _requestRows = [];
    private readonly List<Material> _materials = [];
    private readonly List<OrderMaterial> _orderMaterials = [new OrderMaterial { OrderId = OrderId, MaterialId = 7 }];
    private readonly List<MaterialIssue> _issues = [];
    private readonly MaterialIssueService _sut;

    public MaterialIssueRequisitionTests()
    {
        SetupQueryable(_materials);
        SetupQueryable(_orderMaterials);
        SetupQueryable(_teams);
        SetupQueryable(_lines);
        SetupQueryable(_allotted);
        SetupQueryable(_orders);
        SetupQueryable(_requestRows);
        SetupQueryable(_issues);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<MaterialIssue>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialIssue, string>>>(),
            Arg.Any<CancellationToken>()).Returns("CAP-20261021-001");
        _requests.MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sut = new MaterialIssueService(Uow, _stock, new FabricPlanner(Uow), _codes, Clock,
            NullLogger<MaterialIssueService>.Instance, _requests, Audit, Notifier);
    }

    private static CreateMaterialIssueDto Dto(int? requestId) => new(WarehouseTypes.SemiFinished, null, OrderId, SewingTeamId,
        requestId, new DateOnly(2026, 10, 21), [new CreateIssueLineDto(null, LineId, null, 50)]);

    private void GivenRequest(string status, int createdByTeamId = SewingTeamId, string type = RequestTypes.SemiFinishedRequisition,
        decimal requestedQty = 50, int? workOrderId = null) =>
        _requestRows.Add(new Request
        {
            RequestId = RequestId, RequestTypeCode = type, Status = status, CreatedByTeamId = createdByTeamId, WorkOrderId = workOrderId,
            RequestLines = [type == RequestTypes.MaterialRequisition
                ? new RequestLine { MaterialId = 7, Quantity = requestedQty }
                : new RequestLine { OrderLineId = LineId, Quantity = requestedQty }],
        });

    [Fact]
    public async Task SemiFinishedIssue_LineNotAllottedToTeam_IsRejectedAndNamesTheColorSize()
    {
        GivenRequest(RequestStatuses.Approved);
        _allotted.Clear();

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
        result.Message.Should().Be(string.Format(WarehouseMessages.ProductNotAllottedToTeam, "MH3 Đen/M"));
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task SemiFinishedIssue_OnlyOtherColorSizeAllotted_IsRejected()
    {
        GivenRequest(RequestStatuses.Approved);
        _allotted[0].OrderLineId = LineId + 1;

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentProductNotAllotted);
    }

    [Fact]
    public async Task SemiFinishedIssue_WithoutRequest_IsAllowed()
    {
        await _sut.CreateAsync(Dto(null), 900, default);

        await _stock.Received(1).EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>());
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task SemiFinishedIssue_LineNotInRequest_IsRejected()
    {
        GivenRequest(RequestStatuses.Approved);
        _lines.Add(new OrderLine { OrderLineId = LineId + 1, OrderId = OrderId, ProductId = 3 }.WithVariant(TestVariant.Make(3, "MH3")));
        _allotted.Add(new OrderLineTeam { OrderLineId = LineId + 1, SewingTeamId = SewingTeamId });
        var dto = Dto(RequestId) with { Lines = [new CreateIssueLineDto(null, LineId + 1, null, 5)] };

        var result = await _sut.CreateAsync(dto, 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        result.Message.Should().Be(WarehouseMessages.IssueLineNotInRequest);
    }

    [Fact]
    public async Task SemiFinishedIssue_QtyOverRequest_IsAllowed()
    {
        // DEV-ALL-108: cấp vượt số trong đơn xin chỉ cảnh báo ở màn cấp, BE không chặn.
        GivenRequest(RequestStatuses.Approved, requestedQty: 40);

        await _sut.CreateAsync(Dto(RequestId), 900, default);

        await _stock.Received(1).EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MaterialIssue_WorkOrderOfOtherRequest_IsRejected()
    {
        GivenRequest(RequestStatuses.Approved, type: RequestTypes.MaterialRequisition, workOrderId: 2);
        var dto = new CreateMaterialIssueDto(WarehouseTypes.Material, 1, null, null, RequestId, new DateOnly(2026, 10, 21),
            [new CreateIssueLineDto(7, null, null, 5)]);

        var result = await _sut.CreateAsync(dto, 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
        result.Message.Should().Be(WarehouseMessages.IssueRequestWorkOrderMismatch);
    }

    [Theory]
    [InlineData(MaterialTypes.Trim, 2.5, false)]
    [InlineData(MaterialTypes.Trim, 3, true)]
    [InlineData(MaterialTypes.Fabric, 2.555, false)]
    [InlineData(MaterialTypes.Fabric, 12.5, true)]
    public async Task MaterialIssue_ToSewingTeamWithoutWorkOrder_ChecksQtyByMaterialType(string type, decimal qty, bool accepted)
    {
        _materials.Add(new Material { MaterialId = 7, MaterialName = "Chỉ", MaterialType = type, Status = MaterialStatuses.Active });
        var dto = new CreateMaterialIssueDto(WarehouseTypes.Material, null, OrderId, SewingTeamId, null, new DateOnly(2026, 10, 21),
            [new CreateIssueLineDto(7, null, null, qty)]);

        var result = await _sut.CreateAsync(dto, 900, default);

        if (accepted)
            await _stock.Received(1).EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>());
        else
            result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task MaterialIssue_ByOrder_MaterialNotInOrder_IsRejected()
    {
        _materials.Add(new Material { MaterialId = 8, MaterialName = "Cúc", MaterialType = MaterialTypes.Trim, Status = MaterialStatuses.Active });
        var dto = new CreateMaterialIssueDto(WarehouseTypes.Material, null, OrderId, SewingTeamId, null, new DateOnly(2026, 10, 21),
            [new CreateIssueLineDto(8, null, null, 10)]);

        var result = await _sut.CreateAsync(dto, 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        result.Message.Should().Be(string.Format(WarehouseMessages.IssueMaterialNotInOrder, "Cúc"));
        await _stock.DidNotReceiveWithAnyArgs().EnsureAvailableAsync(default!, default);
    }

    [Fact]
    public async Task MaterialIssue_UnknownMaterial_IsRejected()
    {
        var dto = new CreateMaterialIssueDto(WarehouseTypes.Material, null, OrderId, SewingTeamId, null, new DateOnly(2026, 10, 21),
            [new CreateIssueLineDto(99, null, null, 1)]);

        var result = await _sut.CreateAsync(dto, 900, default);

        result.Message.Should().Be(WarehouseMessages.IssueMaterialInvalid);
    }

    [Theory]
    [InlineData(WarehouseTypes.Material, null, null, null, 5, false)]            // thiếu tổ nhận, đơn hàng, lệnh cắt, đơn xin
    [InlineData(WarehouseTypes.Material, 1, null, null, 5, true)]                // chỉ cần lệnh cắt
    [InlineData(WarehouseTypes.Material, null, 1, SewingTeamId, 5, true)]        // không lệnh cắt, không đơn xin
    [InlineData(WarehouseTypes.SemiFinished, null, 1, SewingTeamId, 0, false)] // số lượng 0
    [InlineData(WarehouseTypes.SemiFinished, null, 1, SewingTeamId, 1_000_001, false)] // vượt trần BTP
    [InlineData(WarehouseTypes.SemiFinished, null, 1, SewingTeamId, 1_000_000, true)]  // đúng trần BTP
    [InlineData(WarehouseTypes.SemiFinished, null, 1, SewingTeamId, 1.5, false)]       // BTP phải là số nguyên
    public void Validator_ChecksTargetAndQuantityBoundaries(string type, int? workOrderId, int? orderId, int? teamId, double qty,
        bool valid)
    {
        var line = type == WarehouseTypes.Material
            ? new CreateIssueLineDto(7, null, null, (decimal)qty)
            : new CreateIssueLineDto(null, LineId, null, (decimal)qty);
        var dto = new CreateMaterialIssueDto(type, workOrderId, orderId, teamId, null, new DateOnly(2026, 10, 21), [line]);

        new NX_MTD.Application.Validators.CreateMaterialIssueDtoValidator().Validate(dto).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_TooManyLines_IsRejected()
    {
        var lines = Enumerable.Range(1, RequisitionLimits.MaxLines + 1).Select(i => new CreateIssueLineDto(i, null, null, 1)).ToList();
        var dto = new CreateMaterialIssueDto(WarehouseTypes.Material, 1, null, null, null, new DateOnly(2026, 10, 21), lines);

        new NX_MTD.Application.Validators.CreateMaterialIssueDtoValidator().Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ApprovedRequest_RequestStaysApproved()
    {
        GivenRequest(RequestStatuses.Approved);

        await _sut.CreateAsync(Dto(RequestId), 900, default);

        await Uow.Repository<MaterialIssue>().ReceivedWithAnyArgs(1).AddAsync(default!);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task CreateAsync_RequestHasPendingIssue_ReturnsFailure()
    {
        GivenRequest(RequestStatuses.Approved);
        GivenIssue(IssueStatuses.PendingReceipt);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
        result.Message.Should().Be(string.Format(WarehouseMessages.IssueRequestHasPendingIssue, "CAP-OLD"));
    }

    [Fact]
    public async Task CreateAsync_PreviousIssueRejected_IsAllowed()
    {
        GivenRequest(RequestStatuses.Approved);
        GivenIssue(IssueStatuses.Rejected);

        await _sut.CreateAsync(Dto(RequestId), 900, default);

        await Uow.Repository<MaterialIssue>().ReceivedWithAnyArgs(1).AddAsync(default!);
    }

    [Fact]
    public async Task ConfirmAsync_IssueOfApprovedRequest_CompletesRequest()
    {
        GivenRequest(RequestStatuses.Approved);
        var issue = GivenIssue(IssueStatuses.PendingReceipt);

        var result = await Receive();

        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Received);
        await _requests.Received(1).MarkCompletedAsync(RequestId, "CAP-OLD", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RequestStatuses.Completed)]
    [InlineData(RequestStatuses.NotCompleted)]
    public async Task ConfirmAsync_RequestAlreadyClosedByHandler_ReceivesAndKeepsRequest(string status)
    {
        GivenRequest(status);
        var issue = GivenIssue(IssueStatuses.PendingReceipt);

        var result = await Receive();

        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Received);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task RejectAsync_IssueOfRequest_RequestStaysApproved()
    {
        GivenRequest(RequestStatuses.Approved);
        var issue = GivenIssue(IssueStatuses.PendingReceipt);

        var result = await _sut.RejectAsync(issue.MaterialIssueId, new ReasonDto("Sai số lượng"), SewingTeamId, default);

        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(IssueStatuses.Rejected);
        _requestRows[0].Status.Should().Be(RequestStatuses.Approved);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    private MaterialIssue GivenIssue(string status)
    {
        var issue = new MaterialIssue
        {
            MaterialIssueId = 300, IssueCode = "CAP-OLD", RequestId = RequestId, OrderId = OrderId, ReceivingTeamId = SewingTeamId,
            WarehouseType = WarehouseTypes.Material, Status = status, IssuedBy = 900,
        };
        issue.MaterialIssueLines.Add(new MaterialIssueLine { MaterialIssueLineId = 301, MaterialId = 7, IssuedQty = 5 });
        _issues.Add(issue);
        return issue;
    }

    private Task<Result> Receive() =>
        _sut.ConfirmAsync(300, new ConfirmIssueDto([new ConfirmIssueLineDto(301, 5, null)]), SewingTeamId, 900, default);

    [Fact]
    public async Task Issue_ByRequest_StockNotEnough_IsRejectedAndRequestStaysOpen()
    {
        GivenRequest(RequestStatuses.Approved);
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, WarehouseMessages.InsufficientStock));

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
        await Uow.Repository<MaterialIssue>().DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Theory]
    [InlineData(RequestStatuses.PendingApproval)]
    [InlineData(RequestStatuses.Completed)]
    [InlineData(RequestStatuses.Rejected)]
    [InlineData(RequestStatuses.Cancelled)]
    public async Task SemiFinishedIssue_WithAlreadyDecidedRequest_IsRejected(string status)
    {
        GivenRequest(status);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task SemiFinishedIssue_WithOtherTeamsRequest_IsRejected()
    {
        GivenRequest(RequestStatuses.Approved, createdByTeamId: 99);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
    }

    [Fact]
    public async Task SemiFinishedIssue_WithWrongTypeOfRequest_IsRejected()
    {
        GivenRequest(RequestStatuses.Approved, type: RequestTypes.MaterialRequisition);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
    }
}
