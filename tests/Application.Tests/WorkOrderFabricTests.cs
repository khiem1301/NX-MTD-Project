using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// DB 29/10 bỏ lượng vải theo định mức: lệnh cắt thường không còn kiểm vải khi lập. Lệnh cắt bù ước tính số mét theo kích thước chi tiết
/// và khổ vải (cùng công thức vw_RecutFabricNeed) rồi so với kho khả dụng + vải tổ cắt đang giữ.
/// </summary>
public class WorkOrderFabricTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, FabricId = 100, LineId = 11, ProductId = 1, PcmId = 900;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<Request> _requests = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly Material _fabric = new()
    {
        MaterialId = FabricId, MaterialCode = "V01", MaterialName = "Vải gió", Unit = "m", Color = "Đen", MaterialType = MaterialTypes.Fabric,
        WidthCm = 150,
    };

    public WorkOrderFabricTests()
    {
        SetupQueryable(_requests); SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_lines); SetupQueryable(_workOrders);
        SetupQueryable(_norms); SetupQueryable(_issueLines); SetupQueryable(_returnLines);
        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH001", Status = OrderStatuses.InProduction });
        _teams.Add(new Team { TeamId = TeamId, TeamCode = "CAT", TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = ProductId, OrderedQty = 200 }
            .WithVariant(TestVariant.Make(ProductId, "MH001", "Đen", "M")));
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(1m);
    }

    private WorkOrderService Sut()
    {
        var codes = Substitute.For<IDocumentCodeGenerator>();
        return new WorkOrderService(Uow, _stock, new FabricPlanner(Uow), codes, Notifier, NullLogger<WorkOrderService>.Instance);
    }

    /// <summary>Chi tiết cần cắt bù: dài 70 cm, rộng 50 cm → khổ 150 cm xếp được 3 miếng một hàng.</summary>
    private void RecutRequest(int pieces, decimal? length = 70, decimal? width = 50)
    {
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = PcmId, ProductVariantId = _lines[0].ProductVariantId, ProductComponentId = 1, MaterialId = FabricId,
            LengthCm = length, WidthCm = width, Material = _fabric,
        });
        _requests.Add(new Request
        {
            RequestId = 5, RequestCode = "YC5", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId,
            RequestLines = { new RequestLine { OrderLineId = LineId, ProductComponentMaterialId = PcmId, Quantity = pieces } },
        });
    }

    private static CreateReplacementWorkOrderDto Dto() => new(5, TeamId, Today.AddDays(3), null);

    [Theory]
    [InlineData(7, 70, 50, 150, 2.1)]    // 3 miếng / hàng → 3 hàng × 70 cm
    [InlineData(6, 70, 50, 150, 1.4)]    // 2 hàng
    [InlineData(1, 70, 50, 150, 0.7)]
    [InlineData(4, 100, 40, 150, 2.0)]   // 3 miếng / hàng → 2 hàng × 100 cm = 2 m
    public void Estimator_countsRowsAcrossTheFabricWidth(int pieces, double length, double width, double fabric, double expected)
    {
        var meters = RecutFabricEstimator.NeedMeters(pieces, (decimal)length, (decimal)width, (decimal)fabric);

        meters.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null, 50.0, 150.0)]
    [InlineData(70.0, null, 150.0)]
    [InlineData(70.0, 50.0, null)]
    [InlineData(70.0, 160.0, 150.0)]   // chi tiết rộng hơn khổ vải
    public void Estimator_returnsNullWhenASizeIsMissingOrThePieceDoesNotFit(double? length, double? width, double? fabric)
    {
        RecutFabricEstimator.NeedMeters(5, (decimal?)length, (decimal?)width, (decimal?)fabric).Should().BeNull();
    }

    [Fact]
    public async Task Recut_WhenStockIsShort_NamesTheFabricAndTheEstimate()
    {
        RecutRequest(7);   // cần khoảng 2,1 m, kho còn 1 m

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("V01").And.Contain("2,1").And.Contain("kho 1");
    }

    [Fact]
    public async Task Recut_TeamHoldingCountsTowardsTheAvailableFabric()
    {
        RecutRequest(7);
        var issue = new MaterialIssue
        {
            MaterialIssueId = 1, OrderId = OrderId, ReceivingTeamId = TeamId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received,
        };
        _issueLines.Add(new MaterialIssueLine { MaterialIssueId = 1, MaterialId = FabricId, IssuedQty = 5, ConfirmedQty = 5, MaterialIssue = issue });

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        // Đủ vải (kho 1 + tổ giữ 5 ≥ 2,1) nên không còn lỗi thiếu vải; bước đọc lại chi tiết lệnh của nguồn giả không chạy được.
        (result.ErrorCode == NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage).Should().BeFalse();
    }

    [Fact]
    public async Task Recut_WithoutSizesTheEstimateIsSkippedAndTheOrderIsNotBlocked()
    {
        RecutRequest(7, length: null, width: null);

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        (result.ErrorCode == NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage).Should().BeFalse();
    }

    [Fact]
    public async Task Fabrics_listsTheWorkOrderFabricsWithIssuedReceivedReturnedAndUsed()
    {
        var variantId = _lines[0].ProductVariantId;
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = PcmId, ProductVariantId = variantId, ProductComponentId = 1, MaterialId = FabricId, Material = _fabric,
            ProductComponent = new ProductComponent { ProductComponentId = 1, ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1 },
        });
        var wo = new WorkOrder
        {
            WorkOrderId = 7, WorkOrderCode = "LC7", OrderId = OrderId, ProductId = ProductId, CuttingTeamId = TeamId, Status = WorkOrderStatuses.Open,
            Order = _orders[0],
        };
        wo.WorkOrderLines.Add(new WorkOrderLine { WorkOrderId = 7, OrderLineId = LineId, PlannedQty = 10, OrderLine = _lines[0] });
        _workOrders.Add(wo);
        var issue = new MaterialIssue { MaterialIssueId = 1, WorkOrderId = 7, Status = IssueStatuses.Received };
        var line = new MaterialIssueLine { MaterialId = FabricId, IssuedQty = 20, ConfirmedQty = 20, MaterialIssue = issue };
        _issueLines.Add(line);
        _returnLines.Add(new MaterialReturnLine { MaterialIssueLine = line, ConfirmedQty = 4, ReturnedQty = 4,
            MaterialReturn = new MaterialReturn { Status = HandoverStatuses.Confirmed, MaterialIssue = issue } });

        var result = await Sut().GetFabricRequirementsAsync(7, default);

        var row = result.Data!.Should().ContainSingle().Subject;
        (row.IssuedQty, row.ReceivedQty, row.ReturnedQty, row.UsedQty).Should().Be((20m, 20m, 4m, 16m));
    }
}
