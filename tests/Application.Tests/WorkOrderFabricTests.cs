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

/// <summary>
/// Lập lệnh cắt: vải cần (định mức) phải đủ — kho khả dụng trừ phần các lệnh cắt khác đã giữ chỗ, cộng vải tổ cắt đang giữ còn trống;
/// dòng đơn chưa có định mức vải thân thì chặn; có thể xem trước vải trong form.
/// </summary>
public class WorkOrderFabricTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, FabricId = 100, LineId = 11, ProductId = 1;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly ProductVariant _variant = TestVariant.Make(ProductId, "MH001", "Đen", "M");
    private readonly Material _fabric = new()
    {
        MaterialId = FabricId, MaterialCode = "V01", MaterialName = "Vải gió", Unit = "m", Color = "Đen", MaterialType = MaterialTypes.Fabric,
    };
    private int _nextWorkOrderId = 50;

    public WorkOrderFabricTests()
    {
        SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_lines); SetupQueryable(_workOrders);
        SetupQueryable(_workOrderLines); SetupQueryable(_norms); SetupQueryable(_issueLines); SetupQueryable(_returnLines);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        Repo<WorkOrder>().When(r => r.AddAsync(Arg.Any<WorkOrder>())).Do(c =>
        {
            var wo = c.Arg<WorkOrder>();
            wo.WorkOrderId = _nextWorkOrderId++;
            _workOrders.Add(wo);
        });

        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH001", Status = OrderStatuses.InProduction });
        _teams.Add(new Team { TeamId = TeamId, TeamCode = "CAT", TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, OrderedQty = 200 }.WithVariant(_variant));
        _norms.Add(Norm(_variant.ProductVariantId, perGarment: 1m));
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(100m);
    }

    private ProductComponentMaterial Norm(int variantId, decimal perGarment) => new()
    {
        ProductComponentMaterialId = 1000 + variantId, ProductVariantId = variantId, ProductComponentId = 1, MaterialId = FabricId,
        QtyPerGarment = perGarment, WastagePercent = 0, Material = _fabric,
        ProductComponent = new ProductComponent { ProductComponentId = 1, ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1 },
    };

    private WorkOrderService Sut()
    {
        var codes = Substitute.For<IDocumentCodeGenerator>();
        codes.NextAsync<WorkOrder>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, string>>>(), Arg.Any<CancellationToken>())
            .Returns("LC-TEST");
        return new WorkOrderService(Uow, _stock, new FabricPlanner(Uow), codes, Notifier, NullLogger<WorkOrderService>.Instance);
    }

    private static SaveWorkOrderDto Plan(int qty) =>
        new(OrderId, ProductId, TeamId, Today.AddDays(5), null, [new SaveWorkOrderLineDto(LineId, qty)]);

    /// <summary>Lệnh cắt đang chạy của cùng đơn / tổ cắt, chưa được cấp vải.</summary>
    private WorkOrder ExistingWorkOrder(int plannedQty, int id = 7, string status = WorkOrderStatuses.Open)
    {
        var wo = new WorkOrder
        {
            WorkOrderId = id, WorkOrderCode = "LC" + id, OrderId = OrderId, ProductId = ProductId, CuttingTeamId = TeamId, Status = status,
        };
        var line = new WorkOrderLine { WorkOrderId = id, OrderLineId = LineId, PlannedQty = plannedQty, WorkOrder = wo, OrderLine = _lines[0] };
        wo.WorkOrderLines.Add(line);
        _workOrders.Add(wo);
        _workOrderLines.Add(line);
        return wo;
    }

    /// <summary>Đường "đủ vải" của CreateAsync kết thúc bằng đọc lại chi tiết lệnh (nhóm theo bàn cắt) mà nguồn giả không hỗ trợ,
    /// nên kiểm kết quả đủ / thiếu qua cùng bộ kiểm tra mà bản xem trước dùng.</summary>
    private async Task<bool> Enough(int qty, int? exceptWorkOrderId = null) =>
        (await Sut().PreviewFabricAsync(Plan(qty), exceptWorkOrderId, default)).Data!.Enough;

    [Fact]
    public async Task Preview_WhenStockCoversTheNeed_IsEnough() => (await Enough(100)).Should().BeTrue();

    [Fact]
    public async Task Create_WhenStockIsShort_NamesTheFabricAndShortfall()
    {
        var result = await Sut().CreateAsync(Plan(150), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("V01").And.Contain("cần 150").And.Contain("thiếu 50");
    }

    [Fact]
    public async Task Create_CountsFabricAlreadyEarmarkedByAnotherOpenWorkOrder()
    {
        // Kho còn 100 m, lệnh LC7 cần 80 m nhưng chưa được cấp → chỉ còn 20 m cho lệnh mới.
        ExistingWorkOrder(80);

        var result = await Sut().CreateAsync(Plan(50), default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("đã giữ cho lệnh cắt khác 80").And.Contain("thiếu 30");
    }

    [Fact]
    public async Task Create_IgnoresCancelledWorkOrders()
    {
        ExistingWorkOrder(80, status: WorkOrderStatuses.Cancelled);

        (await Enough(100)).Should().BeTrue();
    }

    [Fact]
    public async Task Create_FabricAlreadyIssuedToAnotherWorkOrder_IsNotEarmarkedTwice()
    {
        // LC7 cần 80 m, đã được cấp đủ 80 m (kho khả dụng 100 m là phần còn lại sau khi cấp, tổ đang giữ 80 m cho chính LC7).
        var other = ExistingWorkOrder(80);
        var issue = new MaterialIssue
        {
            MaterialIssueId = 1, WorkOrderId = other.WorkOrderId, OrderId = OrderId, ReceivingTeamId = TeamId,
            WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received,
        };
        var line = new MaterialIssueLine { MaterialIssueId = 1, MaterialId = FabricId, IssuedQty = 80, ConfirmedQty = 80, MaterialIssue = issue };
        issue.MaterialIssueLines.Add(line);
        _issueLines.Add(line);

        // Kho 100 − chưa giữ gì thêm + tổ giữ 80 − đã dành cho LC7 80 = 100 m đủ cho 100 m.
        (await Enough(100)).Should().BeTrue();
    }

    [Fact]
    public async Task Create_TeamHoldingCoversTheNeedWhenWarehouseIsEmpty()
    {
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(0m);
        var issue = new MaterialIssue
        {
            MaterialIssueId = 1, OrderId = OrderId, ReceivingTeamId = TeamId, WarehouseType = WarehouseTypes.Material,
            Status = IssueStatuses.Received,
        };
        _issueLines.Add(new MaterialIssueLine { MaterialIssueId = 1, MaterialId = FabricId, IssuedQty = 60, ConfirmedQty = 60, MaterialIssue = issue });

        (await Enough(60)).Should().BeTrue();
        (await Sut().CreateAsync(Plan(61), default)).ErrorCode.Should().Be(ErrorCodes.CuttingFabricShortage);
    }

    [Fact]
    public async Task Update_DoesNotCountTheWorkOrderBeingEdited()
    {
        var self = ExistingWorkOrder(100, id: 7);

        (await Enough(100)).Should().BeFalse();                              // tính cả chính lệnh 7: kho 100 − 100 giữ chỗ
        (await Enough(100, exceptWorkOrderId: self.WorkOrderId)).Should().BeTrue();   // sửa lệnh 7 thì bỏ nó ra
    }

    [Fact]
    public async Task Create_WhenALineHasNoPanelFabricNorm_NamesTheColorAndSize()
    {
        var noNorm = TestVariant.Make(ProductId, "MH001", "Xanh", "L");
        _lines.Add(new OrderLine { OrderLineId = 12, OrderId = OrderId, OrderedQty = 100 }.WithVariant(noNorm));
        var dto = new SaveWorkOrderDto(OrderId, ProductId, TeamId, Today.AddDays(5), null,
            [new SaveWorkOrderLineDto(LineId, 10), new SaveWorkOrderLineDto(12, 10)]);

        var result = await Sut().CreateAsync(dto, default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("Xanh-L").And.NotContain("Đen-M");
    }

    [Fact]
    public async Task Preview_ReturnsNeedAvailableAndShortWithoutSaving()
    {
        ExistingWorkOrder(30);

        var result = await Sut().PreviewFabricAsync(Plan(90), null, default);

        var row = result.Data!.Rows.Should().ContainSingle().Subject;
        row.RequiredQty.Should().Be(90m);
        row.WarehouseAvailable.Should().Be(100m);
        row.ReservedByOtherWorkOrders.Should().Be(30m);
        row.AvailableQty.Should().Be(70m);
        row.ShortQty.Should().Be(20m);
        result.Data.Enough.Should().BeFalse();
        _workOrders.Should().ContainSingle();   // chỉ lệnh có sẵn, preview không tạo thêm
    }

    [Fact]
    public async Task FabricByOrder_GroupsOpenWorkOrdersByOrderAndFabric()
    {
        ExistingWorkOrder(40, id: 7);
        ExistingWorkOrder(60, id: 8);
        ExistingWorkOrder(10, id: 9, status: WorkOrderStatuses.Cancelled);
        _workOrders.ForEach(w => w.Order = _orders[0]);

        var result = await Sut().GetFabricByOrderAsync(null, default);

        var row = result.Data!.Should().ContainSingle().Subject;
        row.OrderCode.Should().Be("DH001");
        row.RequiredQty.Should().Be(100m);
        row.RemainingQty.Should().Be(100m);
        row.WorkOrderCodes.Should().BeEquivalentTo("LC7", "LC8");
    }
}
