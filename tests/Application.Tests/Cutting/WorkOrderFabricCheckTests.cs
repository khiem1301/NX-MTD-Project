using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Lệnh cắt thường: cả bộ tính số mét theo bìa của mã hàng và khổ vải (DEV-ALL-42), cắt lẻ theo kích thước chi tiết; chỉ cảnh báo.</summary>
public class WorkOrderFabricCheckTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, ProductId = 1, FabricId = 100, SizeM = 1, SizeL = 2, SizeXl = 3, RedM = 4, BlueM = 5, NormThan = 11;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductVariant> _variants =
    [
        new() { ProductVariantId = SizeM, ProductId = ProductId, ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "M" },
        new() { ProductVariantId = SizeL, ProductId = ProductId, ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "L" },
        new() { ProductVariantId = SizeXl, ProductId = ProductId, ProductColor = new ProductColor { Color = "Đen" }, SizeCode = "XL" },
        new() { ProductVariantId = RedM, ProductId = ProductId, ProductColor = new ProductColor { Color = "Đỏ" }, SizeCode = "M" },
        new() { ProductVariantId = BlueM, ProductId = ProductId, ProductColor = new ProductColor { Color = "Xanh" }, SizeCode = "M" },
    ];
    private readonly List<OrderLine> _lines = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<MaterialReturnLine> _returnLines = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<Team> _teams = [];
    private readonly List<WorkOrder> _workOrders = [];
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private readonly Material _fabric = new()
    {
        MaterialId = FabricId, MaterialCode = "V01", MaterialName = "Vải gió", Unit = "m", Color = "Đen", MaterialType = MaterialTypes.Fabric, WidthCm = 150,
    };
    private readonly Material _white = new()
    {
        MaterialId = 200, MaterialCode = "V02", MaterialName = "Vải gió", Unit = "m", Color = "Trắng", MaterialType = MaterialTypes.Fabric, WidthCm = 120,
    };

    public WorkOrderFabricCheckTests()
    {
        SetupQueryable(_norms); SetupQueryable(_lines); SetupQueryable(_issueLines); SetupQueryable(_returnLines);
        SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_workOrders); SetupQueryable(_workOrderLines);
        SetupQueryable(_markers); SetupQueryable(_markerFabrics); SetupQueryable(_variants);
        Available(1000);
    }

    private void Available(decimal meters) =>
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(meters);

    private void Marker(byte no, string size, decimal length, decimal width) =>
        _markers.Add(new ProductMarker { ProductId = ProductId, MarkerNo = no, SizeCode = size, LengthCm = length, WidthCm = width });

    private void OnFabric(byte no, string color = "Đen", Material? material = null) =>
        _markerFabrics.Add(new ProductMarkerFabric { ProductId = ProductId, MarkerNo = no, ProductColor = new ProductColor { Color = color }, MaterialId = (material ?? _fabric).MaterialId, Material = material ?? _fabric });

    private static ProductComponent Component(string name, string type = ComponentTypes.Panel, string status = RecordStatus.Active) =>
        new() { ComponentName = name, PiecesPerGarment = 1, ComponentType = type, Status = status };

    private ProductComponentMaterial Norm(int variantId, string name = "Thân", decimal? length = 70, decimal? width = 50,
        Material? material = null, int id = NormThan, string type = ComponentTypes.Panel, string status = RecordStatus.Active)
    {
        var norm = new ProductComponentMaterial
        {
            ProductComponentMaterialId = id, ProductVariantId = variantId, MaterialId = (material ?? _fabric).MaterialId, Material = material ?? _fabric,
            ProductComponent = Component(name, type, status), LengthCm = length, WidthCm = width,
        };
        _norms.Add(norm);
        return norm;
    }

    private WorkOrderFabricChecker Sut() => new(Uow, _stock, new FabricPlanner(Uow));

    private Task<IReadOnlyList<WorkOrderFabricCheckDto>> CheckAll(params CutLine[] lines) => Sut().CheckAsync(OrderId, TeamId, lines, default);

    private async Task<WorkOrderFabricCheckDto> Check(params CutLine[] lines) => (await CheckAll(lines)).Single();

    private static CutLine Whole(int variant, int planned, string label = "Đen - M") => new(variant, planned, label);

    private static CutLine Extra(int planned, int norm = NormThan) => new(SizeM, planned, "Đen - M", norm);

    // ---------- cả bộ theo bìa ----------

    [Fact]
    public async Task CheckAsync_WholeSet_FitsMarkersAcrossTheFabric()
    {
        Marker(1, "M", 120, 70);    // khổ 150: 2 bìa một hàng; 100 áo → 50 hàng × 1,2 m
        OnFabric(1);

        (await Check(Whole(SizeM, 100))).NeedMeters.Should().Be(60m);
    }

    [Fact]
    public async Task CheckAsync_EachSize_UsesItsOwnMarkerSize()
    {
        Marker(1, "M", 120, 70); Marker(1, "L", 125, 75); Marker(1, "XL", 130, 80);
        OnFabric(1);

        (await Check(Whole(SizeM, 100), Whole(SizeL, 80, "Đen - L"), Whole(SizeXl, 30, "Đen - XL"))).NeedMeters.Should().Be(149m);
    }

    [Fact]
    public async Task CheckAsync_TwoMarkersOnTheSameFabric_AreAddedTogether()
    {
        Marker(1, "M", 100, 75); Marker(2, "M", 50, 75);
        OnFabric(1); OnFabric(2);

        (await Check(Whole(SizeM, 2))).NeedMeters.Should().Be(1.5m);
    }

    [Fact]
    public async Task CheckAsync_ColorsWithDifferentFabrics_NameTheFabricThatIsShort()
    {
        Marker(1, "M", 120, 70); Marker(2, "M", 40, 30);
        OnFabric(1); OnFabric(2, material: _white);
        _stock.GetAvailableAsync(StockKey.Material(_white.MaterialId), Arg.Any<CancellationToken>()).Returns(0.1m);

        var rows = await CheckAll(Whole(SizeM, 4));

        rows.Select(r => (r.MaterialCode, r.Status)).Should().Equal(("V01", FabricCheckStatuses.Enough), ("V02", FabricCheckStatuses.Short));
        rows[1].Message.Should().StartWith("V02 Vải gió (Trắng): thiếu khoảng 0,3 m");
    }

    [Fact]
    public async Task CheckAsync_SameMarker_UsesTheFabricOfEachShirtColor()
    {
        Marker(1, "M", 100, 75);
        OnFabric(1); OnFabric(1, "Đỏ", _white);

        var rows = await CheckAll(Whole(SizeM, 2), Whole(RedM, 4, "Đỏ - M"));

        rows.Select(r => (r.MaterialCode, r.NeedMeters)).Should().Equal(("V01", 1m), ("V02", 4m));   // khổ 120 chỉ xếp được 1 bìa một hàng
    }

    [Fact]
    public async Task CheckAsync_ColorsSharingAFabric_ArePooledBeforeRoundingTheRows()
    {
        Marker(2, "M", 40, 30);     // khổ 120: 4 bìa một hàng; 5 + 3 = 8 áo → 2 hàng, tính riêng từng màu sẽ thành 3 hàng
        OnFabric(2, "Đỏ", _white); OnFabric(2, "Xanh", _white);

        (await Check(Whole(RedM, 5, "Đỏ - M"), Whole(BlueM, 3, "Xanh - M"))).NeedMeters.Should().Be(0.8m);
    }

    [Fact]
    public async Task CheckAsync_FabricUsedAsMainAndAsContrast_IsSummedAcrossMarkers()
    {
        Marker(1, "M", 100, 75); Marker(2, "M", 50, 75);
        OnFabric(1); OnFabric(2, "Đỏ");   // vải Đen: bìa 1 của áo Đen, bìa 2 của áo Đỏ

        (await Check(Whole(SizeM, 2), Whole(RedM, 2, "Đỏ - M"))).NeedMeters.Should().Be(1.5m);
    }

    [Fact]
    public async Task CheckAsync_ColorCaseAndSpaces_DoNotBreakTheMatch()
    {
        Marker(1, "m", 100, 75);
        OnFabric(1, " đen ");

        (await Check(Whole(SizeM, 2))).NeedMeters.Should().Be(1m);
    }

    // ---------- cắt lẻ theo chi tiết ----------

    [Fact]
    public async Task CheckAsync_ExtraComponent_IsAddedToTheSameFabric()
    {
        Marker(1, "M", 100, 75);
        OnFabric(1);
        Norm(SizeM);                // 6 miếng, 3 miếng một hàng → 2 hàng × 0,7 m

        (await Check(Whole(SizeM, 2), Extra(6))).NeedMeters.Should().Be(2.4m);
    }

    [Fact]
    public async Task CheckAsync_OnlyExtraComponent_CountsOnlyThatComponent()
    {
        Norm(SizeM, "Thân");
        Norm(SizeM, "Tay", length: 58, width: 40, id: 12);

        (await Check(Extra(3))).NeedMeters.Should().Be(0.7m);
    }

    // ---------- đủ / thiếu ----------

    [Fact]
    public async Task CheckAsync_EnoughFabric_HasNoMessage()
    {
        Marker(1, "M", 70, 75); OnFabric(1);
        Available(0.7m);    // đúng bằng số cần

        var row = await Check(Whole(SizeM, 2));

        row.Status.Should().Be(FabricCheckStatuses.Enough);
        row.Message.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ShortFabric_SaysHowMuchIsMissing()
    {
        Marker(1, "M", 70, 75); OnFabric(1);
        Available(0.5m);

        var row = await Check(Whole(SizeM, 2));

        row.Status.Should().Be(FabricCheckStatuses.Short);
        row.Message.Should().Be("V01 Vải gió (Đen): thiếu khoảng 0,2 m (cần khoảng 0,7 m, có 0,5 m: kho 0,5, tổ đang giữ 0).");
    }

    [Fact]
    public async Task CheckAsync_FabricHeldByTheTeam_Counts()
    {
        Marker(1, "M", 70, 75); OnFabric(1);
        Available(0.2m);
        _issueLines.Add(new MaterialIssueLine
        {
            MaterialId = FabricId, ConfirmedQty = 0.5m,
            MaterialIssue = new MaterialIssue { ReceivingTeamId = TeamId, OrderId = OrderId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received },
        });

        (await Check(Whole(SizeM, 2))).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    // ---------- không tính được ----------

    [Fact]
    public async Task CheckAsync_MarkerWiderThanTheFabric_NamesTheMarkerAndSize()
    {
        Marker(1, "M", 70, 151); OnFabric(1);

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.TooWide);
        row.Message.Should().Be("V01 Vải gió (Đen): rộng hơn khổ vải 1,5 m: bìa 1 size M (151 cm).");
    }

    [Fact]
    public async Task CheckAsync_MarkerAsWideAsTheFabric_IsFine()
    {
        Marker(1, "M", 70, 150); OnFabric(1);

        (await Check(Whole(SizeM, 1))).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    [Fact]
    public async Task CheckAsync_MarkerWithoutThisSize_NamesTheMarkerAndSize()
    {
        Marker(1, "L", 70, 75); OnFabric(1);

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Be("V01 Vải gió (Đen): chưa tính được vì còn thiếu: bìa 1 size M.");
    }

    [Fact]
    public async Task CheckAsync_FabricInTheNormButOnNoMarker_IsNotSilentlyLeftOut()
    {
        Marker(1, "M", 70, 75); OnFabric(1);
        Norm(SizeM);
        Norm(SizeM, "Cổ", material: _white, id: 12);    // vải phối chưa có bìa: không được báo đủ vải

        var rows = await CheckAll(Whole(SizeM, 2));

        rows.Select(r => (r.MaterialCode, r.Status)).Should().Equal(("V01", FabricCheckStatuses.Enough), ("V02", FabricCheckStatuses.Unknown));
        rows[1].Message.Should().Be("V02 Vải gió (Trắng): chưa tính được vì còn thiếu: bìa cho màu Đen.");
    }

    [Fact]
    public async Task CheckAsync_ExtraComponentWithoutSize_NamesTheComponent()
    {
        Norm(SizeM, "Tay", length: null);

        (await Check(Extra(3))).Message.Should().Be("V01 Vải gió (Đen): chưa tính được vì còn thiếu: Tay Đen - M.");
    }

    [Fact]
    public async Task CheckAsync_LongLists_AreShortened()
    {
        for (var i = 1; i <= 5; i++) Norm(SizeM, $"CT{i}", length: null, id: i);

        (await Check(Enumerable.Range(1, 5).Select(i => Extra(1, i)).ToArray())).Message
            .Should().EndWith("CT1 Đen - M, CT2 Đen - M, CT3 Đen - M và 2 mục khác.");
    }

    [Fact]
    public async Task CheckAsync_FabricWithoutWidthOrNotInMeters_CannotBeEstimated()
    {
        Marker(1, "M", 70, 75);
        OnFabric(1, material: new Material { MaterialId = 7, MaterialCode = "V07", MaterialName = "Vải", Unit = "m", Color = "", MaterialType = MaterialTypes.Fabric });
        (await Check(Whole(SizeM, 1))).Message.Should().Be("V07 Vải: chưa tính được vì NVL chưa có khổ vải.");

        _markerFabrics.Clear();
        OnFabric(1, material: new Material { MaterialId = 8, MaterialCode = "V08", MaterialName = "Vải", Unit = "kg", Color = "", MaterialType = MaterialTypes.Fabric, WidthCm = 150 });
        (await Check(Whole(SizeM, 1))).Message.Should().Be("V08 Vải: chưa tính được vì NVL không tính theo mét.");
    }

    [Fact]
    public async Task CheckAsync_ColorSizeWithoutFabricNorm_IsReportedNotTreatedAsEnough()
    {
        var row = await Check(Whole(SizeM, 3));

        row.MaterialId.Should().Be(0);
        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Be("Chưa tính được vải cho Đen - M vì chưa có định mức vải.");
    }

    // ---------- phạm vi ----------

    [Fact]
    public async Task CheckAsync_TrimsDiscontinuedComponentsAndNonFabrics_NeedNoMarker()
    {
        Marker(1, "M", 70, 75); OnFabric(1);
        Norm(SizeM);
        Norm(SizeM, "Khóa", type: ComponentTypes.Trim, material: _white, id: 12);
        Norm(SizeM, "Cũ", status: RecordStatus.Discontinued, material: _white, id: 13);
        Norm(SizeM, "Mex", material: new Material { MaterialId = 8, MaterialCode = "K1", MaterialName = "Mex", Unit = "m", Color = "", MaterialType = "TRIM" }, id: 14);

        (await Check(Whole(SizeM, 2))).NeedMeters.Should().Be(0.7m);
    }

    // ---------- lưu lệnh có cắt lẻ ----------

    private WorkOrderService Service() => new(Uow, _stock, new FabricPlanner(Uow), Substitute.For<IDocumentCodeGenerator>(), Notifier,
        NullLogger<WorkOrderService>.Instance, Clock, Sut());

    private int SeedOrderLine()
    {
        var line = new OrderLine { OrderLineId = 21, OrderId = OrderId, ProductId = 1, OrderedQty = 10 }.WithVariant(TestVariant.Make(1, "MH1", "Đen", "M"));
        _lines.Add(line);
        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH1", Status = OrderStatuses.InProduction, ReceivedDate = Today.AddDays(-5) });
        _teams.Add(new Team { TeamId = TeamId, TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        return line.ProductVariantId;
    }

    private static SaveWorkOrderDto Save(params SaveWorkOrderLineDto[] lines) => new(OrderId, 1, TeamId, Today.AddDays(5), null, lines);

    [Fact]
    public async Task ExtraComponent_OfAnotherColorSize_IsRefusedWithAClearMessage()
    {
        SeedOrderLine();
        Norm(SizeL);

        var result = await Service().CreateAsync(Save(new(21, 5), new(21, 3, NormThan)), default);

        result.Message.Should().Be(WarehouseMessages.WorkOrderComponentInvalid);
    }

    [Fact]
    public async Task ExtraComponent_DoesNotCountAgainstTheOrderedQuantity_ButWholeSetsDo()
    {
        var variant = SeedOrderLine();
        Norm(variant);

        var over = await Service().CreateAsync(Save(new SaveWorkOrderLineDto(21, 11)), default);
        over.Message.Should().Contain("đặt 10");

        var within = await Service().CreateAsync(Save(new(21, 10), new(21, 500, NormThan)), default);
        within.Message.Should().NotContain("đặt 10");
    }

    [Fact]
    public void WholeAndExtraOfTheSameLine_AreAllowed_ButTheSameLineTwiceIsNot()
    {
        var validator = new SaveWorkOrderDtoValidator();

        validator.Validate(Save(new(21, 5), new(21, 3, NormThan))).IsValid.Should().BeTrue();
        validator.Validate(Save(new(21, 5), new(21, 3))).IsValid.Should().BeFalse();
        validator.Validate(Save(new(21, 5, NormThan), new(21, 3, NormThan))).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(1_000_001, false)]
    public void PlannedQty_Boundaries(int qty, bool expected) =>
        new SaveWorkOrderLineDtoValidator().Validate(new SaveWorkOrderLineDto(1, qty, NormThan)).IsValid.Should().Be(expected);

    [Fact]
    public void FabricCheck_NeedsLines_AndLimitsThem()
    {
        var validator = new CheckWorkOrderFabricDtoValidator();
        var lines = Enumerable.Range(1, 1001).Select(i => new SaveWorkOrderLineDto(i, 1)).ToList();

        validator.Validate(new CheckWorkOrderFabricDto(1, 1, [])).IsValid.Should().BeFalse();
        validator.Validate(new CheckWorkOrderFabricDto(1, 1, lines.Take(1000).ToList())).IsValid.Should().BeTrue();
        validator.Validate(new CheckWorkOrderFabricDto(1, 1, lines)).IsValid.Should().BeFalse();
    }
}
