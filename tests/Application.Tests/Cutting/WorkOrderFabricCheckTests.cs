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

/// <summary>
/// Lệnh cắt thường ước vải theo bìa của đơn (DEV-ALL-85): cả bộ = số lớp × chiều dài bìa, số lớp = max theo size của ⌈cần ÷ số sản phẩm
/// size đó trên 1 lớp⌉, chọn bìa ít mét nhất; cắt lẻ theo bìa của nhóm chứa chi tiết, tối thiểu một lớp. Chỉ cảnh báo.
/// </summary>
public class WorkOrderFabricCheckTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, ProductId = 1, FabricId = 100, SizeM = 1, SizeL = 2, SizeXl = 3, RedM = 4, BlueM = 5,
        NormThan = 11, Body = 31, Sleeve = 32;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<ProductVariant> _variants =
    [
        new() { ProductVariantId = SizeM, ProductId = ProductId, ProductColorId = 1, ProductColor = new ProductColor { ProductColorId = 1, Color = "Đen" }, SizeCode = "M" },
        new() { ProductVariantId = SizeL, ProductId = ProductId, ProductColorId = 1, ProductColor = new ProductColor { ProductColorId = 1, Color = "Đen" }, SizeCode = "L" },
        new() { ProductVariantId = SizeXl, ProductId = ProductId, ProductColorId = 1, ProductColor = new ProductColor { ProductColorId = 1, Color = "Đen" }, SizeCode = "XL" },
        new() { ProductVariantId = RedM, ProductId = ProductId, ProductColorId = 2, ProductColor = new ProductColor { ProductColorId = 2, Color = "Đỏ" }, SizeCode = "M" },
        new() { ProductVariantId = BlueM, ProductId = ProductId, ProductColorId = 3, ProductColor = new ProductColor { ProductColorId = 3, Color = "Xanh" }, SizeCode = "M" },
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
        MaterialId = FabricId,
        MaterialCode = "V01",
        MaterialName = "Vải gió",
        Unit = "m",
        Color = "Đen",
        MaterialType = MaterialTypes.Fabric,
    };
    private readonly Material _white = new()
    {
        MaterialId = 200,
        MaterialCode = "V02",
        MaterialName = "Vải gió",
        Unit = "m",
        Color = "Trắng",
        MaterialType = MaterialTypes.Fabric,
    };

    public WorkOrderFabricCheckTests()
    {
        SetupQueryable(_norms); SetupQueryable(_lines); SetupQueryable(_issueLines); SetupQueryable(_returnLines);
        SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_workOrders); SetupQueryable(_workOrderLines);
        SetupQueryable(_markers); SetupQueryable(_markerFabrics); SetupQueryable(_markerComponents); SetupQueryable(_variants);
        Available(1000);
    }

    private void Available(decimal meters) =>
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(meters);

    /// <summary>Bìa của đơn: tỉ lệ dạng "M:2 L:1".</summary>
    private void Marker(string code, byte group, decimal length, string ratio, int order = OrderId, string status = RecordStatus.Active)
    {
        var marker = new ProductMarker
        {
            ProductMarkerId = _markers.Count + 1,
            OrderId = order,
            ProductId = ProductId,
            MarkerNo = group,
            MarkerCode = code,
            LengthM = length,
            Status = status,
        };
        foreach (var part in ratio.Split(' '))
        {
            var bits = part.Split(':');
            marker.ProductMarkerRatios.Add(new ProductMarkerRatio { SizeCode = bits[0], PiecesPerPly = short.Parse(bits[1]) });
        }
        _markers.Add(marker);
    }

    private void OnFabric(byte group, string color = "Đen", Material? material = null) =>
        _markerFabrics.Add(new ProductMarkerFabric
        {
            ProductId = ProductId,
            MarkerNo = group,
            ProductColor = new ProductColor { Color = color },
            MaterialId = (material ?? _fabric).MaterialId,
            Material = material ?? _fabric,
        });

    private void OnGroup(byte group, int componentId) =>
        _markerComponents.Add(new ProductMarkerComponent { ProductId = ProductId, MarkerNo = group, ProductComponentId = componentId });

    private ProductComponentMaterial Norm(int variantId, string name = "Thân", Material? material = null, int id = NormThan,
        string type = ComponentTypes.Panel, string status = RecordStatus.Active, int componentId = Body, short pieces = 1)
    {
        var norm = new ProductComponentMaterial
        {
            ProductComponentMaterialId = id,
            ProductColorId = _variants.Concat(_lines.Select(l => l.ProductVariant)).First(v => v.ProductVariantId == variantId).ProductColorId,
            ProductComponentId = componentId,
            MaterialId = (material ?? _fabric).MaterialId,
            Material = material ?? _fabric,
            ProductComponent = new ProductComponent
            {
                ProductComponentId = componentId,
                ComponentName = name,
                PiecesPerGarment = pieces,
                ComponentType = type,
                Status = status,
            },
        };
        _norms.Add(norm);
        return norm;
    }

    private WorkOrderFabricChecker Sut() => new(Uow, _stock, new FabricPlanner(Uow), new BranchAndBoundMarkerSolver());

    private Task<IReadOnlyList<WorkOrderFabricCheckDto>> CheckAll(params CutLine[] lines) => Sut().CheckAsync(OrderId, TeamId, lines, default);

    private async Task<WorkOrderFabricCheckDto> Check(params CutLine[] lines) => (await CheckAll(lines)).Single();

    private static CutLine Whole(int variant, int planned, string label = "Đen - M") => new(variant, planned, label);

    private static CutLine Extra(int planned, int norm = NormThan) => new(SizeM, planned, "Đen - M", norm);

    /// <summary>Áo Đen M, L trên nhóm bìa 1 (thân), vải V01.</summary>
    private void BodyOnGroupOne()
    {
        Norm(SizeM); Norm(SizeL, id: 12);
        OnFabric(1); OnGroup(1, Body);
    }

    // ---------- cả bộ theo bìa của đơn ----------

    [Fact]
    public async Task Whole_PliesFollowTheSizeThatNeedsTheMostLayers()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 1.65m, "M:2 L:1");

        var row = await Check(Whole(SizeM, 80), Whole(SizeL, 40, "Đen - L"));   // max(80/2, 40/1) = 40 lớp

        (row.Status, row.NeedMeters).Should().Be((FabricCheckStatuses.Enough, 66m));
    }

    [Fact]
    public async Task Whole_ChoosesTheMarkerNeedingTheFewestMeters()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2.2m, "M:2 L:1");   // M30: 15 lớp × 2,2 = 33 m
        Marker("1B", 1, 2.1m, "M:4");       // M30: 8 lớp × 2,1 = 16,8 m

        (await Check(Whole(SizeM, 30))).NeedMeters.Should().Be(16.8m);
    }

    [Fact]
    public async Task Whole_MarkerWithoutANeededSize_IsNotACandidate()
    {
        BodyOnGroupOne();
        Marker("1B", 1, 2.1m, "M:4");

        var row = await Check(Whole(SizeM, 10), Whole(SizeL, 10, "Đen - L"));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Contain("bìa của đơn cho nhóm 1").And.Contain("L");
    }

    [Theory]
    [InlineData(RecordStatus.Discontinued, OrderId)]
    [InlineData(RecordStatus.Active, 99)]
    public async Task Whole_DiscontinuedMarkersAndMarkersOfOtherOrders_AreIgnored(string status, int order)
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1", order, status);

        (await Check(Whole(SizeM, 10))).Status.Should().Be(FabricCheckStatuses.Unknown);
    }

    [Fact]
    public async Task CheckAsync_WholeWithEnoughFabric_ShowsMarkersToUseAndSurplus()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2.2m, "M:2 L:1");   // lệnh chỉ M30: 15 lớp, dư 15 áo L

        var row = await Check(Whole(SizeM, 30));

        row.Status.Should().Be(FabricCheckStatuses.Enough);
        row.Message.Should().BeNull();
        var plan = row.MarkerPlans.Should().ContainSingle().Subject;
        (plan.MarkerNo, plan.IsExtra, plan.Meters, plan.Surplus, plan.IsProvenOptimal).Should().Be((1, false, 33m, 15, true));
        var marker = plan.Markers.Should().ContainSingle().Subject;
        (marker.MarkerCode, marker.LengthM, marker.Plies, marker.Meters).Should().Be(("1A", 2.2m, 15, 33m));
        marker.Ratio.Select(r => $"{r.SizeCode}{r.PiecesPerPly}").Should().Equal("M2", "L1");   // theo thứ tự size, không theo chữ cái
    }

    [Fact]
    public async Task CheckAsync_WholeWithSeveralMarkers_CombinesForFewestMeters()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1 L:1");
        Marker("1B", 1, 1m, "L:1");   // M10, L30: 1A × 10 (20 m) + 1B × 20 (20 m) = 40 m, rẻ hơn 1A × 30 = 60 m

        var row = await Check(Whole(SizeM, 10), Whole(SizeL, 30));

        row.NeedMeters.Should().Be(40m);
        row.MarkerPlans!.Single().Markers.Select(m => (m.MarkerCode, m.Plies, m.Meters)).Should().Equal(("1A", 10, 20m), ("1B", 20, 20m));
        (row.WarehouseQty + row.HoldingQty).Should().Be(row.AvailableQty);
    }

    [Fact]
    public async Task Whole_TwoMarkerGroupsOnTheSameFabric_AreAddedTogether()
    {
        BodyOnGroupOne();
        OnFabric(2);
        Marker("1A", 1, 2m, "M:1");
        Marker("2A", 2, 1m, "M:1");

        (await Check(Whole(SizeM, 10))).NeedMeters.Should().Be(30m);
    }

    [Fact]
    public async Task Whole_SameGroupOnDifferentFabricsByShirtColor_EstimatesEachFabric()
    {
        Norm(SizeM); Norm(BlueM, material: _white, id: 13);
        OnFabric(1); OnFabric(1, "Xanh", _white); OnGroup(1, Body);
        Marker("1A", 1, 2m, "M:2");

        var rows = await CheckAll(Whole(SizeM, 10), Whole(BlueM, 4, "Xanh - M"));

        rows.Select(r => (r.MaterialCode, r.NeedMeters)).Should().BeEquivalentTo([("V01", (decimal?)10m), ("V02", (decimal?)4m)]);
    }

    [Fact]
    public async Task Whole_EachColorOnlyUsesTheMarkersOfTheGroupsOfItsOwnLayout()
    {
        // DEV-ALL-89: Đen dùng nhóm 1 (bìa 1A, 2 m); Xanh khác cách chia vải nên ở dạng bìa riêng, nhóm 3 (bìa 3C, 3 m).
        Norm(SizeM); Norm(BlueM, material: _white, id: 13);
        OnFabric(1); OnFabric(3, "Xanh", _white); OnGroup(1, Body); OnGroup(3, Body);
        Marker("1A", 1, 2m, "M:2");
        Marker("3C", 3, 3m, "M:2");

        var rows = await CheckAll(Whole(SizeM, 10), Whole(BlueM, 4, "Xanh - M"));

        rows.Select(r => (r.MaterialCode, r.NeedMeters)).Should().BeEquivalentTo([("V01", (decimal?)10m), ("V02", (decimal?)6m)]);
        rows.Single(r => r.MaterialCode == "V02").MarkerPlans!.Single().Markers.Single().MarkerCode.Should().Be("3C");
    }

    [Fact]
    public async Task Whole_GroupOfAColorWithoutMarkers_NamesTheGroupAndTheColor()
    {
        Norm(BlueM, material: _white, id: 13);
        OnFabric(3, "Xanh", _white); OnGroup(3, Body);
        Marker("1A", 1, 2m, "M:2");

        var row = await Check(Whole(BlueM, 4, "Xanh - M"));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Contain("bìa của đơn cho nhóm 3, màu Xanh");
    }

    [Fact]
    public async Task Whole_ColorCaseAndSpaces_DoNotBreakTheMatch()
    {
        Norm(SizeM);
        OnFabric(1, " đen "); OnGroup(1, Body);
        Marker("1A", 1, 2m, "M:1");

        (await Check(Whole(SizeM, 3))).NeedMeters.Should().Be(6m);
    }

    // ---------- cắt lẻ theo nhóm bìa chứa chi tiết ----------

    [Theory]
    [InlineData(7, 3.2)]   // 2 tay / áo × M2 = 4 tay / lớp → 2 lớp × 1,6 m
    [InlineData(1, 1.6)]   // tối thiểu một chiều dài bìa
    public async Task Extra_UsesTheMarkerOfTheGroupHoldingTheComponent(int pieces, double meters)
    {
        Norm(SizeM, "Tay áo", componentId: Sleeve, pieces: 2);
        OnFabric(2); OnGroup(2, Sleeve);
        Marker("2A", 2, 1.6m, "M:2");

        (await Check(Extra(pieces))).NeedMeters.Should().Be((decimal)meters);
    }

    [Fact]
    public async Task Extra_ComponentOnNoGroup_NamesTheComponent()
    {
        Norm(SizeM, "Tay áo", componentId: Sleeve);
        OnFabric(2);

        var row = await Check(Extra(3));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Contain("nhóm bìa cho chi tiết Tay áo");
    }

    [Fact]
    public async Task Extra_IsAddedToTheWholeSetsOnTheSameFabric()
    {
        Norm(SizeM); Norm(SizeM, "Tay áo", id: 14, componentId: Sleeve, pieces: 2);
        OnFabric(1); OnFabric(2); OnGroup(1, Body); OnGroup(2, Sleeve);
        Marker("1A", 1, 2m, "M:1");
        Marker("2A", 2, 1m, "M:2");

        // cả bộ: thân 5 lớp × 2 m + tay ⌈5 ÷ 2⌉ = 3 lớp × 1 m; cắt lẻ 3 tay: 1 lớp × 1 m
        (await Check(Whole(SizeM, 5), Extra(3, 14))).NeedMeters.Should().Be(14m);
    }

    // ---------- đủ / thiếu ----------

    [Fact]
    public async Task EnoughFabric_HasNoMessage()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1");

        var row = await Check(Whole(SizeM, 10));

        (row.Status, row.NeedMeters, row.AvailableQty, row.Message).Should().Be((FabricCheckStatuses.Enough, 20m, 1000m, (string?)null));
    }

    [Fact]
    public async Task ShortFabric_SaysHowMuchIsMissing()
    {
        Available(15);
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1");

        var row = await Check(Whole(SizeM, 10));

        row.Status.Should().Be(FabricCheckStatuses.Short);
        row.Message.Should().Contain("thiếu khoảng 5 m").And.Contain("cần khoảng 20 m");
    }

    [Fact]
    public async Task FabricHeldByTheTeam_Counts()
    {
        Available(15);
        var issue = new MaterialIssue { OrderId = OrderId, ReceivingTeamId = TeamId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received };
        _issueLines.Add(new MaterialIssueLine { MaterialId = FabricId, IssuedQty = 5, ConfirmedQty = 5, MaterialIssue = issue });
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1");

        (await Check(Whole(SizeM, 10))).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    // ---------- không tính được ----------

    [Fact]
    public async Task FabricInTheNormButOnNoMarkerGroup_IsNotSilentlyLeftOut()
    {
        BodyOnGroupOne();
        Norm(SizeM, "Lót", _white, id: 15, componentId: 33);
        Marker("1A", 1, 2m, "M:1");

        var white = (await CheckAll(Whole(SizeM, 10))).Single(r => r.MaterialId == _white.MaterialId);

        white.Status.Should().Be(FabricCheckStatuses.Unknown);
        white.Message.Should().Contain("bìa cho màu Đen");
    }

    [Fact]
    public async Task FabricNotInMeters_CannotBeEstimated()
    {
        var kg = new Material { MaterialId = 300, MaterialCode = "V03", MaterialName = "Vải cân", Unit = "kg", Color = "", MaterialType = MaterialTypes.Fabric };
        Norm(SizeM, material: kg);
        OnFabric(1, material: kg); OnGroup(1, Body);
        Marker("1A", 1, 2m, "M:1");

        var row = await Check(Whole(SizeM, 10));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Contain("không tính theo mét");
    }

    [Fact]
    public async Task ColorSizeWithoutFabricNorm_IsReportedNotTreatedAsEnough()
    {
        var row = await Check(Whole(RedM, 10, "Đỏ - M"));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Contain("Đỏ - M");
    }

    [Fact]
    public async Task TrimsAndDiscontinuedComponents_NeedNoMarker()
    {
        BodyOnGroupOne();
        Marker("1A", 1, 2m, "M:1");
        Norm(SizeM, "Nhãn", _white, id: 16, type: ComponentTypes.Trim, componentId: 34);
        Norm(SizeM, "Cổ cũ", _white, id: 17, status: RecordStatus.Discontinued, componentId: 35);

        (await CheckAll(Whole(SizeM, 10))).Should().ContainSingle(r => r.MaterialId == FabricId);
    }

    // ---------- lưu lệnh có cắt lẻ ----------

    private WorkOrderService Service() => new(Uow, _stock, new FabricPlanner(Uow), Substitute.For<IDocumentCodeGenerator>(), Notifier,
        NullLogger<WorkOrderService>.Instance, Clock, Sut());

    private int SeedOrderLine()
    {
        var variant = TestVariant.Make(1, "MH1", "Đen", "M");
        variant.ProductColorId = _variants[0].ProductColorId;   // cùng màu Đen với các size M, L, XL ở trên
        variant.ProductColor = _variants[0].ProductColor;
        var line = new OrderLine { OrderLineId = 21, OrderId = OrderId, ProductId = 1, OrderedQty = 10 }.WithVariant(variant);
        _lines.Add(line);
        _orders.Add(new CustomerOrder { OrderId = OrderId, OrderCode = "DH1", Status = OrderStatuses.InProduction, ReceivedDate = Today.AddDays(-5) });
        _teams.Add(new Team { TeamId = TeamId, TeamType = TeamTypes.Cutting, Status = RecordStatus.Active });
        return line.ProductVariantId;
    }

    private static SaveWorkOrderDto Save(params SaveWorkOrderLineDto[] lines) => new(OrderId, 1, TeamId, Today.AddDays(5), null, lines);

    [Fact]
    public async Task CreateAsync_ExtraComponentOfAnotherColor_IsRefused()
    {
        SeedOrderLine();
        Norm(RedM);

        var result = await Service().CreateAsync(Save(new(21, 5), new(21, 3, NormThan)), default);

        result.Message.Should().Be(WarehouseMessages.WorkOrderComponentInvalid);
    }

    [Fact]
    public async Task CreateAsync_ExtraComponentOfSameColorOtherSize_UsesTheColorNorm()
    {
        SeedOrderLine();
        Norm(SizeL);   // định mức theo màu (DEV-ALL-87): dòng định mức của màu Đen dùng được cho dòng đơn Đen – M

        var result = await Service().CreateAsync(Save(new(21, 5), new(21, 3, NormThan)), default);

        result.Message.Should().NotBe(WarehouseMessages.WorkOrderComponentInvalid);
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
