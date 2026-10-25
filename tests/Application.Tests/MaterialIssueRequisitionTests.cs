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

/// <summary>TH-05 / TH-08 (UC40): cấp BTP cho tổ may gắn đơn Xin BTP; lập phiếu cấp xong đơn xin tự hoàn thành.</summary>
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
    private readonly List<OrderLine> _lines = [new OrderLine { OrderLineId = LineId, OrderId = OrderId }];
    private readonly List<CustomerOrder> _orders = [new CustomerOrder { OrderId = OrderId, Status = OrderStatuses.InProduction }];
    private readonly List<Request> _requestRows = [];
    private readonly MaterialIssueService _sut;

    public MaterialIssueRequisitionTests()
    {
        SetupQueryable(_teams);
        SetupQueryable(_lines);
        SetupQueryable(_orders);
        SetupQueryable(_requestRows);
        SetupQueryable(new List<MaterialIssue>());
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<MaterialIssue>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialIssue, string>>>(),
            Arg.Any<CancellationToken>()).Returns("CAP-20261021-001");
        _requests.MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sut = new MaterialIssueService(Uow, _stock, new FabricPlanner(Uow), _codes, Clock,
            NullLogger<MaterialIssueService>.Instance, _requests, Audit);
    }

    private static CreateMaterialIssueDto Dto(int? requestId) => new(WarehouseTypes.SemiFinished, null, OrderId, SewingTeamId,
        requestId, new DateOnly(2026, 10, 21), [new CreateIssueLineDto(null, null, LineId, null, 50)]);

    private void GivenRequest(string status, int createdByTeamId = SewingTeamId, string type = RequestTypes.SemiFinishedRequisition) =>
        _requestRows.Add(new Request { RequestId = RequestId, RequestTypeCode = type, Status = status, CreatedByTeamId = createdByTeamId });

    [Fact]
    public async Task SemiFinishedIssue_WithoutRequest_IsRejected()
    {
        var result = await _sut.CreateAsync(Dto(null), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
        await _requests.DidNotReceiveWithAnyArgs().MarkCompletedAsync(default, default, default);
    }

    [Fact]
    public async Task SemiFinishedIssue_WithPendingRequest_CompletesTheRequest()
    {
        GivenRequest(RequestStatuses.PendingProcessing);

        await _sut.CreateAsync(Dto(RequestId), 900, default);

        await _requests.Received(1).MarkCompletedAsync(RequestId, "CAP-20261021-001", Arg.Any<CancellationToken>());
    }

    [Theory]
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
        GivenRequest(RequestStatuses.PendingProcessing, createdByTeamId: 99);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
    }

    [Fact]
    public async Task SemiFinishedIssue_WithWrongTypeOfRequest_IsRejected()
    {
        GivenRequest(RequestStatuses.PendingProcessing, type: RequestTypes.MaterialRequisition);

        var result = await _sut.CreateAsync(Dto(RequestId), 900, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidRequest);
    }
}
