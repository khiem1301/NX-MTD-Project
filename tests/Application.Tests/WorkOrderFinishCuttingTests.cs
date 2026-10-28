using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Tổ cắt báo cắt xong khi cắt ít hơn kế hoạch: BTP đã cắt phải được kho xác nhận hết; thiếu kế hoạch thì cần lý do.</summary>
public class WorkOrderFinishCuttingTests : ProductionTestBase
{
    private const int TeamId = 2, LineId = 11;

    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<CutHandoverLine> _handoverLines = [];
    private readonly List<CuttingLayLine> _layLines = [];
    private readonly WorkOrder _wo;

    public WorkOrderFinishCuttingTests()
    {
        SetupQueryable(_workOrders); SetupQueryable(_handoverLines); SetupQueryable(_layLines);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _wo = new WorkOrder
        {
            WorkOrderId = 7, WorkOrderCode = "LC-7", CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting,
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001" },
        };
        _wo.WorkOrderLines.Add(new WorkOrderLine { WorkOrderId = 7, OrderLineId = LineId, PlannedQty = 100 });
        _workOrders.Add(_wo);
    }

    private WorkOrderService Sut() => new(Uow, Substitute.For<IStockService>(), new FabricPlanner(Uow),
        Substitute.For<IDocumentCodeGenerator>(), Notifier, NullLogger<WorkOrderService>.Instance);

    private void Cut(int qty) => _layLines.Add(new CuttingLayLine
    {
        OrderLineId = LineId, ActualQty = qty, CuttingLay = new CuttingLay { WorkOrderId = 7 },
    });

    private void Handover(string status, int handed, int? confirmed) => _handoverLines.Add(new CutHandoverLine
    {
        OrderLineId = LineId, HandedQty = handed, ConfirmedQty = confirmed, CutHandover = new CutHandover { WorkOrderId = 7, Status = status },
    });

    [Fact]
    public async Task ShortCut_EverythingHandedAndConfirmed_CompletesWithReasonAndTellsTheManager()
    {
        Cut(80); Handover(HandoverStatuses.Confirmed, 80, 80);

        var result = await Sut().FinishCuttingAsync(7, TeamId, "Hết vải", default);

        result.IsSuccess.Should().BeTrue();
        _wo.Status.Should().Be(WorkOrderStatuses.Completed);
        _wo.Note.Should().Contain("Hết vải");
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => m.Message.Contains("80/100") && m.Message.Contains("Hết vải")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortCut_WithoutAReason_IsRefused()
    {
        Cut(80); Handover(HandoverStatuses.Confirmed, 80, 80);

        var result = await Sut().FinishCuttingAsync(7, TeamId, "  ", default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        result.Message.Should().Contain("80/100");
        _wo.Status.Should().Be(WorkOrderStatuses.Cutting);
    }

    [Fact]
    public async Task APendingHandover_OrCutNotYetHandedOver_BlocksFinishing()
    {
        Cut(80); Handover(HandoverStatuses.Pending, 80, null);
        (await Sut().FinishCuttingAsync(7, TeamId, "x", default)).Message.Should().Be(WarehouseMessages.WorkOrderHasPendingHandoverToFinish);

        _handoverLines.Clear();
        Cut(10); Handover(HandoverStatuses.Confirmed, 80, 80);   // cắt 90, kho mới nhận 80
        var result = await Sut().FinishCuttingAsync(7, TeamId, "x", default);
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        result.Message.Should().Contain("10");
        _wo.Status.Should().Be(WorkOrderStatuses.Cutting);
    }

    [Fact]
    public async Task OnlyTheOwnTeam_AndOnlyAWorkOrderThatIsCutting_CanFinish()
    {
        Cut(100); Handover(HandoverStatuses.Confirmed, 100, 100);

        (await Sut().FinishCuttingAsync(7, 99, null, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseTeamMismatch);
        _wo.Status = WorkOrderStatuses.Open;
        (await Sut().FinishCuttingAsync(7, TeamId, null, default)).Message.Should().Be(WarehouseMessages.WorkOrderNotFinishable);
        (await Sut().FinishCuttingAsync(99, TeamId, null, default)).Kind.Should().Be(ResultKind.NotFound);
    }
}
