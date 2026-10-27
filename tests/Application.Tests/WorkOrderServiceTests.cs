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

/// <summary>UC19 (tổ cắt): lập lệnh cắt chặn khi dòng chưa có định mức vải (không tính vải của dòng đó là 0) hoặc thiếu vải.</summary>
public class WorkOrderServiceTests : ProductionTestBase
{
    private const int OrderId = 1;
    private const int ProductId = 1;
    private const int CuttingTeamId = 2;
    private const int BlackLineId = 31;
    private const int BlueLineId = 32;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly WorkOrderService _sut;

    public WorkOrderServiceTests()
    {
        SetupQueryable(new List<CustomerOrder> { new() { OrderId = OrderId, Status = OrderStatuses.InProduction } });
        SetupQueryable(new List<Team> { new() { TeamId = CuttingTeamId, TeamType = TeamTypes.Cutting, Status = RecordStatus.Active } });
        SetupQueryable(new List<OrderLine>
        {
            new() { OrderLineId = BlackLineId, OrderId = OrderId, ProductId = ProductId, Color = "Đen", SizeCode = "S", OrderedQty = 300 },
            new() { OrderLineId = BlueLineId, OrderId = OrderId, ProductId = ProductId, Color = "Xanh", SizeCode = "L", OrderedQty = 200 },
        });
        SetupQueryable(new List<WorkOrderLine>());
        SetupQueryable(_orderMaterials);
        SetupQueryable(new List<MaterialIssueLine>());
        SetupQueryable(new List<MaterialReturnLine>());
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(10_000m);
        _sut = new WorkOrderService(Uow, _stock, new FabricPlanner(Uow), _codes, NullLogger<WorkOrderService>.Instance);
    }

    private void GivenBlackFabric(params OrderMaterialNorm[] norms) => _orderMaterials.Add(new OrderMaterial
    {
        OrderMaterialId = 10, OrderId = OrderId, MaterialId = 100, MaterialColor = "Đen",
        Material = new Material { MaterialId = 100, MaterialCode = "VAI01", MaterialName = "Vải gió", MaterialType = MaterialTypes.Fabric, Unit = "m" },
        OrderMaterialNorms = norms,
    });

    private static OrderMaterialNorm BlackNorm(decimal perPiece = 1.5m) => new()
    {
        OrderMaterialId = 10, ProductId = ProductId, ComponentName = "Thân", GarmentColor = "Đen", QtyPerPiece = perPiece,
    };

    private static SaveWorkOrderDto Dto(params (int OrderLineId, int Qty)[] lines) => new(OrderId, ProductId, CuttingTeamId,
        new DateOnly(2026, 10, 25), null, lines.Select(l => new SaveWorkOrderLineDto(l.OrderLineId, l.Qty)).ToList());

    [Fact]
    public async Task Create_OrderHasNoNorm_IsRejectedNamingTheLine()
    {
        GivenBlackFabric();

        var result = await _sut.CreateAsync(Dto((BlackLineId, 100)), default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricNormMissing);
        result.Message.Should().StartWith(WarehouseMessages.FabricNormMissingFor).And.Contain("Đen-S");
    }

    [Fact]
    public async Task Create_SomeLinesHaveNoNorm_IsRejectedNamingOnlyThoseLines()
    {
        GivenBlackFabric(BlackNorm());

        var result = await _sut.CreateAsync(Dto((BlackLineId, 100), (BlueLineId, 50)), default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricNormMissing);
        result.Message.Should().Contain("Xanh-L").And.NotContain("Đen-S");
    }

    [Fact]
    public async Task Create_OnlyLineWithoutNorm_IsRejectedEvenThoughOrderHasNorms()
    {
        GivenBlackFabric(BlackNorm());

        var result = await _sut.CreateAsync(Dto((BlueLineId, 50)), default);

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricNormMissing);
        result.Message.Should().Contain("Xanh-L");
    }

    [Fact]
    public async Task Create_AllLinesCovered_MovesOnToStockCheck()
    {
        GivenBlackFabric(BlackNorm(perPiece: 1.5m));
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(100m);

        var result = await _sut.CreateAsync(Dto((BlackLineId, 100)), default);   // cần 100 × 1,5 = 150 m, kho có 100

        result.ErrorCode.Should().Be(ErrorCodes.CuttingFabricShortage);
        result.Message.Should().Contain("VAI01").And.Contain("thiếu 50");
    }
}
