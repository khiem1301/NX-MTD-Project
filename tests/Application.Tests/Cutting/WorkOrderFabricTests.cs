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
/// Lệnh cắt bù ước số mét vải theo bìa của đơn chứa chi tiết (DEV-ALL-85): số miếng trên một lớp = số sản phẩm size đó × số chi tiết
/// trên một sản phẩm, tối thiểu một lớp; so với kho khả dụng + vải tổ cắt đang giữ. Vải của lệnh: đã cấp, đã cắt, còn lại.
/// </summary>
public class WorkOrderFabricTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, FabricId = 100, LineId = 11, ProductId = 1, PcmId = 900, SleeveId = 1;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<Request> _requests = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Team> _teams = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly List<CuttingLay> _lays = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductMarkerFabric> _groupFabrics = [];
    private readonly List<ProductMarkerComponent> _groupComponents = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly Material _fabric = new()
    {
        MaterialId = FabricId, MaterialCode = "V01", MaterialName = "Vải gió", Unit = "m", Color = "Đen", MaterialType = MaterialTypes.Fabric,
    };

    public WorkOrderFabricTests()
    {
        SetupQueryable(_requests); SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_lines); SetupQueryable(_workOrders);
        SetupQueryable(_norms); SetupQueryable(_issueLines); SetupQueryable(_returnLines); SetupQueryable(_lays);
        SetupQueryable(_variants); SetupQueryable(_groupFabrics); SetupQueryable(_groupComponents); SetupQueryable(_markers);
        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH001", Status = OrderStatuses.InProduction });
        _teams.Add(new Team { TeamId = TeamId, TeamCode = "CAT", TeamName = "Tổ cắt", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        _lines.Add(new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = ProductId, OrderedQty = 200 }
            .WithVariant(TestVariant.Make(ProductId, "MH001", "Đen", "M")));
        _variants.Add(_lines[0].ProductVariant);
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(1m);
    }

    private WorkOrderService Sut()
    {
        var codes = Substitute.For<IDocumentCodeGenerator>();
        return new WorkOrderService(Uow, _stock, new FabricPlanner(Uow), codes, Notifier, NullLogger<WorkOrderService>.Instance, Clock,
            new WorkOrderFabricChecker(Uow, _stock, new FabricPlanner(Uow), Settings));
    }

    /// <summary>Cắt bù tay áo (2 chi tiết / sản phẩm); bìa tay 2A của đơn dài 1,6 m, M2 → 4 tay M trên một lớp.</summary>
    private void RecutRequest(int pieces, bool withMarker = true)
    {
        var variant = _lines[0].ProductVariant;
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = PcmId, ProductId = ProductId, ProductVariantId = variant.ProductVariantId, ProductVariant = variant,
            ProductComponentId = SleeveId, MaterialId = FabricId, Material = _fabric,
            ProductComponent = new ProductComponent
            {
                ProductComponentId = SleeveId, ComponentName = "Tay áo", ComponentType = ComponentTypes.Panel, PiecesPerGarment = 2,
                Status = RecordStatus.Active,
            },
        });
        _groupFabrics.Add(new ProductMarkerFabric
        {
            ProductId = ProductId, MarkerNo = 2, ProductColorId = variant.ProductColorId, ProductColor = variant.ProductColor,
            MaterialId = FabricId, Material = _fabric,
        });
        _groupComponents.Add(new ProductMarkerComponent { ProductId = ProductId, MarkerNo = 2, ProductComponentId = SleeveId });
        if (withMarker)
        {
            var marker = new ProductMarker
            {
                ProductMarkerId = 1, OrderId = OrderId, ProductId = ProductId, MarkerNo = 2, MarkerCode = "2A", LengthM = 1.6m,
                Status = RecordStatus.Active,
            };
            marker.ProductMarkerRatios.Add(new ProductMarkerRatio { ProductMarkerId = 1, SizeCode = "M", PiecesPerPly = 2 });
            _markers.Add(marker);
        }
        _requests.Add(new Request
        {
            RequestId = 5, RequestCode = "YC5", RequestTypeCode = RequestTypes.ReplacementCutting, Status = RequestStatuses.Approved, OrderId = OrderId,
            RequestLines = { new RequestLine { OrderLineId = LineId, ProductComponentMaterialId = PcmId, Quantity = pieces } },
        });
    }

    private static CreateReplacementWorkOrderDto Dto() => new(5, TeamId, Today.AddDays(3), null);

    [Fact]
    public async Task Recut_DueDateInThePast_IsRejected()
    {
        RecutRequest(7);

        var result = await Sut().CreateReplacementAsync(Dto() with { DueDate = Today.AddDays(-1) }, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(NX_MTD.Application.Common.WarehouseMessages.WorkOrderDueInPast);
    }

    [Fact]
    public async Task Recut_WhenStockIsShort_NamesTheFabricAndTheEstimate()
    {
        RecutRequest(7);   // 7 tay ÷ 4 tay / lớp → 2 lớp × 1,6 m = 3,2 m, kho còn 1 m

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("V01").And.Contain("3,2").And.Contain("kho 1");
    }

    [Fact]
    public async Task Recut_FewPieces_StillNeedOneMarkerLength()
    {
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(1.5m);
        RecutRequest(1);   // 1 tay vẫn cần một lớp = 1,6 m > 1,5 m

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        result.ErrorCode.Should().Be(NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("1,6");
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

        // Đủ vải (kho 1 + tổ giữ 5 ≥ 3,2) nên không còn lỗi thiếu vải; bước đọc lại chi tiết lệnh của nguồn giả không chạy được.
        (result.ErrorCode == NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage).Should().BeFalse();
    }

    [Fact]
    public async Task Recut_WithoutAnOrderMarkerTheEstimateIsSkippedAndTheOrderIsNotBlocked()
    {
        RecutRequest(7, withMarker: false);

        var result = await Sut().CreateReplacementAsync(Dto(), default);

        (result.ErrorCode == NX_MTD.Domain.Constants.ErrorCodes.CuttingFabricShortage).Should().BeFalse();
    }

    private void AddLay(short plies, decimal? length, int team = TeamId, int order = OrderId, int fabric = FabricId) =>
        _lays.Add(new CuttingLay { CuttingTeamId = team, FabricMaterialId = fabric, PlyCount = plies, LayLengthM = length, WorkOrder = new WorkOrder { OrderId = order } });

    private void HeldFabric(decimal received, decimal returned)
    {
        var issue = new MaterialIssue { WorkOrderId = 7, ReceivingTeamId = TeamId, OrderId = OrderId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received };
        var line = new MaterialIssueLine { MaterialId = FabricId, IssuedQty = received, ConfirmedQty = received, MaterialIssue = issue };
        _issueLines.Add(line);
        _returnLines.Add(new MaterialReturnLine { MaterialIssueLine = line, ConfirmedQty = returned, ReturnedQty = returned,
            MaterialReturn = new MaterialReturn { Status = HandoverStatuses.Confirmed, MaterialIssue = issue } });
    }

    private void SeedWorkOrderWithFabric()
    {
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = PcmId, ProductVariantId = _lines[0].ProductVariantId, ProductComponentId = 1, MaterialId = FabricId, Material = _fabric,
            ProductComponent = new ProductComponent { ProductComponentId = 1, ComponentType = ComponentTypes.Panel, PiecesPerGarment = 1 },
        });
        var wo = new WorkOrder { WorkOrderId = 7, OrderId = OrderId, ProductId = ProductId, CuttingTeamId = TeamId, Status = WorkOrderStatuses.Cutting, Order = _orders[0] };
        wo.WorkOrderLines.Add(new WorkOrderLine { WorkOrderId = 7, OrderLineId = LineId, PlannedQty = 10, OrderLine = _lines[0] });
        _workOrders.Add(wo);
    }

    [Fact]
    public async Task Fabrics_CutIsPliesTimesLength_AndRemainingIsWhatTheTeamHoldsMinusCut()
    {
        SeedWorkOrderWithFabric();
        HeldFabric(100, 10);        // đang giữ 90 m
        AddLay(5, 6);               // 30 m
        AddLay(2, 10.5m);           // 21 m

        var row = (await Sut().GetFabricRequirementsAsync(7, default)).Data!.Single();

        (row.HeldQty, row.CutQty, row.RemainingQty, row.UnmeasuredLayCount).Should().Be((90m, 51m, 39m, 0));
    }

    [Fact]
    public async Task Fabrics_OnlyCountsLaysOfThisTeamOrderAndFabric()
    {
        SeedWorkOrderWithFabric();
        HeldFabric(100, 0);
        AddLay(1, 10);
        AddLay(1, 500, team: 99);
        AddLay(1, 500, order: 99);
        AddLay(1, 500, fabric: 99);

        (await Sut().GetFabricRequirementsAsync(7, default)).Data!.Single().CutQty.Should().Be(10m);
    }

    [Fact]
    public async Task Fabrics_LayWithoutLength_IsCountedSeparately_NotAsZeroCut()
    {
        SeedWorkOrderWithFabric();
        HeldFabric(100, 0);
        AddLay(3, null);

        var row = (await Sut().GetFabricRequirementsAsync(7, default)).Data!.Single();

        (row.CutQty, row.UnmeasuredLayCount).Should().Be((0m, 1));
    }

    [Fact]
    public async Task Fabrics_CuttingMoreThanHeld_ShowsANegativeRemainder()
    {
        SeedWorkOrderWithFabric();
        HeldFabric(10, 0);
        AddLay(2, 10);

        (await Sut().GetFabricRequirementsAsync(7, default)).Data!.Single().RemainingQty.Should().Be(-10m);
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
