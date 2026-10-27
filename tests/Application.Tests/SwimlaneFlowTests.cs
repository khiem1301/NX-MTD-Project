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

/// <summary>Các bước theo swimlane: tổ may nhận BTP thì đơn sang sản xuất, tổ cắt rút phiếu bàn giao, Quản đốc đóng lệnh cắt.</summary>
public class SwimlaneFlowTests : ProductionTestBase
{
    private const int SewingTeam = 5;
    private const int CuttingTeam = 2;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly List<MaterialIssue> _issues = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<CutHandover> _handovers = [];
    private readonly List<CutHandoverLine> _handoverLines = [];
    private readonly List<CuttingLay> _lays = [];

    public SwimlaneFlowTests()
    {
        SetupQueryable(_issues); SetupQueryable(_orders); SetupQueryable(_workOrders);
        SetupQueryable(_handovers); SetupQueryable(_handoverLines); SetupQueryable(_lays);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    // ---------- Tổ may nhận BTP → IN_PRODUCTION ----------

    private MaterialIssueService Issues() => new(Uow, _stock, new FabricPlanner(Uow), Substitute.For<IDocumentCodeGenerator>(), Clock,
        NullLogger<MaterialIssueService>.Instance, _requests, Audit);

    private (MaterialIssue Issue, ConfirmIssueDto Dto) PendingBtpIssue(CustomerOrder order, string warehouse = WarehouseTypes.SemiFinished)
    {
        _orders.Add(order);
        var issue = new MaterialIssue
        {
            MaterialIssueId = 1, OrderId = order.OrderId, WarehouseType = warehouse, ReceivingTeamId = SewingTeam,
            Status = IssueStatuses.PendingReceipt,
        };
        var line = new MaterialIssueLine { MaterialIssueLineId = 10, MaterialIssueId = 1, OrderLineId = 11, MaterialId = warehouse == WarehouseTypes.Material ? 1 : null, IssuedQty = 50 };
        issue.MaterialIssueLines.Add(line);
        _issues.Add(issue);
        return (issue, new ConfirmIssueDto([new ConfirmIssueLineDto(10, 50, null)]));
    }

    [Fact]
    public async Task Sewing_team_receiving_btp_moves_a_new_order_to_production()
    {
        var order = new CustomerOrder { OrderId = 1, Status = OrderStatuses.New, MaterialStatus = OrderMaterialStatuses.Complete };
        var (issue, dto) = PendingBtpIssue(order);

        var result = await Issues().ConfirmAsync(issue.MaterialIssueId, dto, SewingTeam, 900, default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatuses.InProduction);
        order.UpdatedAt.Should().Be(Now);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(CustomerOrder) && e.Action == AuditActions.Update));
    }

    [Theory]
    [InlineData(OrderStatuses.InProduction)]
    [InlineData(OrderStatuses.InDelivery)]
    [InlineData(OrderStatuses.Stopped)]
    public async Task Receiving_btp_does_not_touch_an_order_that_is_not_new(string status)
    {
        var order = new CustomerOrder { OrderId = 1, Status = status, MaterialStatus = OrderMaterialStatuses.Complete };
        var (issue, dto) = PendingBtpIssue(order);

        await Issues().ConfirmAsync(issue.MaterialIssueId, dto, SewingTeam, 900, default);

        order.Status.Should().Be(status);
        Audit.DidNotReceive().Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(CustomerOrder)));
    }

    [Fact]
    public async Task Receiving_btp_while_materials_are_still_awaited_keeps_the_order_new()
    {
        var order = new CustomerOrder { OrderId = 1, Status = OrderStatuses.New, MaterialStatus = OrderMaterialStatuses.Awaiting };
        var (issue, dto) = PendingBtpIssue(order);

        await Issues().ConfirmAsync(issue.MaterialIssueId, dto, SewingTeam, 900, default);

        order.Status.Should().Be(OrderStatuses.New);
    }

    [Fact]
    public async Task Cutting_team_receiving_fabric_does_not_start_production()
    {
        var order = new CustomerOrder { OrderId = 1, Status = OrderStatuses.New, MaterialStatus = OrderMaterialStatuses.Complete };
        var (issue, dto) = PendingBtpIssue(order, WarehouseTypes.Material);

        await Issues().ConfirmAsync(issue.MaterialIssueId, dto, SewingTeam, 900, default);

        order.Status.Should().Be(OrderStatuses.New);
    }

    // ---------- Tổ cắt rút phiếu bàn giao BTP ----------

    private CutHandoverService Handovers() => new(Uow, _stock, Substitute.For<IDocumentCodeGenerator>(), Substitute.For<IAppLock>(), Clock,
        _requests, NullLogger<CutHandoverService>.Instance);

    private WorkOrder CuttingWorkOrder(int plannedQty = 100, string status = WorkOrderStatuses.Cutting)
    {
        var wo = new WorkOrder { WorkOrderId = 7, WorkOrderCode = "LC-7", CuttingTeamId = CuttingTeam, Status = status };
        wo.WorkOrderLines.Add(new WorkOrderLine { WorkOrderLineId = 1, WorkOrderId = 7, OrderLineId = 11, PlannedQty = plannedQty });
        _workOrders.Add(wo);
        return wo;
    }

    private CutHandover Handover(WorkOrder wo, string status, int handed = 100, int? confirmed = null, int id = 1)
    {
        var h = new CutHandover { CutHandoverId = id, WorkOrderId = wo.WorkOrderId, WorkOrder = wo, Status = status };
        var line = new CutHandoverLine { CutHandoverLineId = id, CutHandoverId = id, CutHandover = h, OrderLineId = 11, HandedQty = handed, ConfirmedQty = confirmed };
        h.CutHandoverLines.Add(line);
        _handovers.Add(h); _handoverLines.Add(line);
        return h;
    }

    [Fact]
    public async Task Cutting_team_can_cancel_a_pending_handover_and_the_locked_lays_open_again()
    {
        var wo = CuttingWorkOrder();
        var h = Handover(wo, HandoverStatuses.Pending);
        var lay = new CuttingLay { CuttingLayId = 1, WorkOrderId = 7, Status = CuttingLayStatuses.Locked };
        _lays.Add(lay);

        var result = await Handovers().CancelAsync(h.CutHandoverId, CuttingTeam, default);

        result.IsSuccess.Should().BeTrue();
        h.Status.Should().Be(HandoverStatuses.Cancelled);
        lay.Status.Should().Be(CuttingLayStatuses.Recorded);
    }

    [Fact]
    public async Task Cancelling_one_handover_keeps_lays_locked_while_another_handover_still_stands()
    {
        var wo = CuttingWorkOrder();
        Handover(wo, HandoverStatuses.Confirmed, confirmed: 60, id: 1);
        var pending = Handover(wo, HandoverStatuses.Pending, handed: 40, id: 2);
        var lay = new CuttingLay { CuttingLayId = 1, WorkOrderId = 7, Status = CuttingLayStatuses.Locked };
        _lays.Add(lay);

        await Handovers().CancelAsync(pending.CutHandoverId, CuttingTeam, default);

        pending.Status.Should().Be(HandoverStatuses.Cancelled);
        lay.Status.Should().Be(CuttingLayStatuses.Locked);
    }

    [Fact]
    public async Task Only_pending_handovers_of_the_own_cutting_team_can_be_cancelled()
    {
        var wo = CuttingWorkOrder();
        var confirmed = Handover(wo, HandoverStatuses.Confirmed, confirmed: 100, id: 1);
        var pending = Handover(wo, HandoverStatuses.Pending, id: 2);

        (await Handovers().CancelAsync(confirmed.CutHandoverId, CuttingTeam, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Handovers().CancelAsync(pending.CutHandoverId, 99, default)).Kind.Should().Be(ResultKind.NotFound);
        pending.Status.Should().Be(HandoverStatuses.Pending);
    }

    // ---------- Quản đốc đóng lệnh cắt ----------

    private WorkOrderService WorkOrdersService() => new(Uow, _stock, new FabricPlanner(Uow), Substitute.For<IDocumentCodeGenerator>(),
        NullLogger<WorkOrderService>.Instance);

}
