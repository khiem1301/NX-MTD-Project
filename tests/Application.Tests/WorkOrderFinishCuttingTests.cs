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
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly List<MaterialReturn> _returns = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly WorkOrder _wo;

    public WorkOrderFinishCuttingTests()
    {
        SetupQueryable(_workOrders); SetupQueryable(_handoverLines); SetupQueryable(_layLines);
        SetupQueryable(_issueLines); SetupQueryable(_returnLines); SetupQueryable(_returns); SetupQueryable(_norms);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _wo = new WorkOrder
        {
            WorkOrderId = 7, WorkOrderCode = "LC-7", CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting,
            Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001" },
        };
        _wo.WorkOrderLines.Add(new WorkOrderLine
        {
            WorkOrderId = 7, OrderLineId = LineId, PlannedQty = 100,
            OrderLine = new OrderLine { OrderLineId = LineId, ProductVariantId = 5 },
        });
        _workOrders.Add(_wo);
    }

    private WorkOrderService Sut() => new(Uow, Substitute.For<IStockService>(), new FabricPlanner(Uow),
        Substitute.For<IDocumentCodeGenerator>(), Notifier, NullLogger<WorkOrderService>.Instance);

    private readonly Material _fabric = new()
    {
        MaterialId = 100, MaterialCode = "V01", MaterialName = "Vải gió", Unit = "m", Color = "Đen", MaterialType = MaterialTypes.Fabric,
    };

    /// <summary>Định mức 1 m vải cho 1 bộ của biến thể 5.</summary>
    private void Norm(decimal perGarment = 1m) => _norms.Add(new ProductComponentMaterial
    {
        ProductComponentMaterialId = 900, ProductVariantId = 5, ProductComponentId = 1, MaterialId = 100, QtyPerGarment = perGarment,
        Material = _fabric, ProductComponent = new ProductComponent { ProductComponentId = 1, ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1 },
    });

    private MaterialIssueLine IssueReceived(decimal qty) => AddIssueLine(qty, IssueStatuses.Received);

    private MaterialIssueLine AddIssueLine(decimal qty, string status)
    {
        var line = new MaterialIssueLine
        {
            MaterialIssueLineId = _issueLines.Count + 1, MaterialId = 100, IssuedQty = qty, ConfirmedQty = status == IssueStatuses.Received ? qty : null,
            MaterialIssue = new MaterialIssue { WorkOrderId = 7, Status = status },
        };
        _issueLines.Add(line);
        return line;
    }

    private void Return(MaterialIssueLine issue, decimal qty, string status) => _returnLines.Add(new MaterialReturnLine
    {
        MaterialIssueLine = issue, ConfirmedQty = status == HandoverStatuses.Confirmed ? qty : null, ReturnedQty = qty,
        MaterialReturn = new MaterialReturn { Status = status, MaterialIssue = issue.MaterialIssue },
    });

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
    public async Task FullPlan_NeedsNoReason()
    {
        Cut(100); Handover(HandoverStatuses.Confirmed, 100, 100);

        (await Sut().FinishCuttingAsync(7, TeamId, null, default)).IsSuccess.Should().BeTrue();

        _wo.Status.Should().Be(WorkOrderStatuses.Completed);
    }

    [Fact]
    public async Task APendingFabricReturn_BlocksFinishing_UntilTheWarehouseConfirmsIt()
    {
        Cut(100); Handover(HandoverStatuses.Confirmed, 100, 100);
        var issue = IssueReceived(120);
        _returns.Add(new MaterialReturn { Status = HandoverStatuses.Pending, MaterialIssue = issue.MaterialIssue });

        var blocked = await Sut().FinishCuttingAsync(7, TeamId, null, default);

        blocked.Message.Should().Be(WarehouseMessages.WorkOrderHasPendingReturn);
        _wo.Status.Should().Be(WorkOrderStatuses.Cutting);

        _returns.Clear();
        (await Sut().FinishCuttingAsync(7, TeamId, null, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Finishing_ComparesTheFabricActuallyUsedWithTheNormAndTellsTheManager()
    {
        Cut(100); Handover(HandoverStatuses.Confirmed, 100, 100);
        Norm(1m);                                   // 100 bộ → 100 m
        var issue = IssueReceived(120);             // nhận 120 m
        Return(issue, 15, HandoverStatuses.Confirmed);   // trả 15 m → dùng 105 m, vượt định mức 5 m
        Return(issue, 40, HandoverStatuses.Pending);     // phiếu trả chưa xác nhận không tính (và đang chờ → chặn)
        _returns.Add(new MaterialReturn { Status = HandoverStatuses.Cancelled, MaterialIssue = issue.MaterialIssue });
        _returnLines.RemoveAll(r => r.MaterialReturn.Status == HandoverStatuses.Pending);

        var result = await Sut().FinishCuttingAsync(7, TeamId, null, default);

        result.IsSuccess.Should().BeTrue();
        await Notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager,
            Arg.Is<NotificationMessage>(m => m.Message.Contains("V01") && m.Message.Contains("thực dùng 105")
                && m.Message.Contains("trả 15") && m.Message.Contains("định mức 100") && m.Message.Contains("vượt 5")),
            Arg.Any<CancellationToken>());
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
