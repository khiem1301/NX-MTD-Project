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

/// <summary>Lệnh cắt thường: số mét tính từ kích thước từng chi tiết và khổ vải; cắt lẻ thêm thành phần cộng vào cùng NVL; chỉ cảnh báo.</summary>
public class WorkOrderFabricCheckTests : ProductionTestBase
{
    private const int OrderId = 1, TeamId = 2, FabricId = 100, SizeM = 1, SizeL = 2, NormThan = 11;

    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly List<ProductComponentMaterial> _norms = [];
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

    public WorkOrderFabricCheckTests()
    {
        SetupQueryable(_norms); SetupQueryable(_lines); SetupQueryable(_issueLines); SetupQueryable(_returnLines);
        SetupQueryable(_orders); SetupQueryable(_teams); SetupQueryable(_workOrders); SetupQueryable(_workOrderLines);
        Available(1000);
    }

    private void Available(decimal meters) =>
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(meters);

    private static ProductComponent Component(string name, short pieces = 1, string type = ComponentTypes.Panel, string status = RecordStatus.Active) =>
        new() { ComponentName = name, PiecesPerGarment = pieces, ComponentType = type, Status = status };

    private ProductComponentMaterial Norm(int variantId, string name = "Thân", short pieces = 1, decimal? length = 70, decimal? width = 50,
        Material? material = null, int id = NormThan, string type = ComponentTypes.Panel, string status = RecordStatus.Active)
    {
        var norm = new ProductComponentMaterial
        {
            ProductComponentMaterialId = id, ProductVariantId = variantId, MaterialId = (material ?? _fabric).MaterialId, Material = material ?? _fabric,
            ProductComponent = Component(name, pieces, type, status), LengthCm = length, WidthCm = width,
        };
        _norms.Add(norm);
        return norm;
    }

    private WorkOrderFabricChecker Sut() => new(Uow, _stock, new FabricPlanner(Uow));

    private async Task<WorkOrderFabricCheckDto> Check(params CutLine[] lines) =>
        (await Sut().CheckAsync(OrderId, TeamId, lines, default)).Single();

    private static CutLine Whole(int variant, int planned, string label = "Đen - M") => new(variant, planned, label);

    // ---------- tính số mét ----------

    [Fact]
    public async Task WholeSet_MultipliesByPiecesPerGarment_AndFitsPiecesAcrossTheFabric()
    {
        Norm(SizeM, "Thân trước", pieces: 2);                                   // 200 miếng, 3 miếng/hàng → 67 hàng × 0,7 m = 46,9 m
        Norm(SizeM, "Tay", pieces: 2, length: 58, width: 40, id: 12);           // 200 miếng, 3 miếng/hàng → 67 hàng × 0,58 m = 38,86 m

        (await Check(Whole(SizeM, 100))).NeedMeters.Should().Be(85.76m);
    }

    [Fact]
    public async Task EachColorSize_UsesItsOwnPieceSize()
    {
        Norm(SizeM, length: 70);
        Norm(SizeL, length: 80, id: 12);

        (await Check(Whole(SizeM, 3), Whole(SizeL, 3, "Đen - L"))).NeedMeters.Should().Be(1.5m);    // mỗi size 1 hàng
    }

    [Fact]
    public async Task ExtraComponent_IsAdded_WithoutMultiplyingAndOnlyForThatComponent()
    {
        Norm(SizeM, "Thân", pieces: 2);                                         // lẻ 6 miếng + cả bộ 3 × 2 = 12 miếng → 4 hàng × 0,7 = 2,8 m
        Norm(SizeM, "Tay", pieces: 2, length: 58, width: 40, id: 12);           // chỉ cả bộ: 6 miếng → 2 hàng × 0,58 = 1,16 m

        (await Check(Whole(SizeM, 3), new CutLine(SizeM, 6, "Đen - M", NormThan))).NeedMeters.Should().Be(3.96m);
    }

    [Fact]
    public async Task OnlyExtraComponent_CountsOnlyThatComponent()
    {
        Norm(SizeM, "Thân");
        Norm(SizeM, "Tay", length: 58, width: 40, id: 12);

        (await Check(new CutLine(SizeM, 3, "Đen - M", NormThan))).NeedMeters.Should().Be(0.7m);
    }

    // ---------- đủ / thiếu ----------

    [Fact]
    public async Task EnoughFabric_HasNoMessage()
    {
        Norm(SizeM);
        Available(0.7m);    // đúng bằng số cần

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.Enough);
        row.Message.Should().BeNull();
    }

    [Fact]
    public async Task ShortFabric_SaysHowMuchIsMissing()
    {
        Norm(SizeM);
        Available(0.5m);

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.Short);
        row.Message.Should().Be("V01 Vải gió (Đen): thiếu khoảng 0,2 m (cần khoảng 0,7 m, có 0,5 m: kho 0,5, tổ đang giữ 0).");
    }

    [Fact]
    public async Task FabricHeldByTheTeam_Counts()
    {
        Norm(SizeM);
        Available(0.2m);
        _issueLines.Add(new MaterialIssueLine
        {
            MaterialId = FabricId, ConfirmedQty = 0.5m,
            MaterialIssue = new MaterialIssue { ReceivingTeamId = TeamId, OrderId = OrderId, WarehouseType = WarehouseTypes.Material, Status = IssueStatuses.Received },
        });

        (await Check(Whole(SizeM, 3))).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    // ---------- không tính được ----------

    [Fact]
    public async Task PieceWiderThanTheFabric_NamesThePiece()
    {
        Norm(SizeM, "Thân", width: 151);

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.TooWide);
        row.Message.Should().Be("V01 Vải gió (Đen): chi tiết rộng hơn khổ vải 150 cm: Thân Đen - M (151 cm).");
    }

    [Fact]
    public async Task PieceAsWideAsTheFabric_IsFine()
    {
        Norm(SizeM, width: 150);

        (await Check(Whole(SizeM, 1))).Status.Should().Be(FabricCheckStatuses.Enough);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(70, null)]
    public async Task MissingPieceSize_NamesThePiece(int? length, int? width)
    {
        Norm(SizeM, "Tay", length: length, width: width);

        var row = await Check(Whole(SizeM, 3));

        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Be("V01 Vải gió (Đen): chưa tính được vì chưa có kích thước chi tiết: Tay Đen - M.");
    }

    [Fact]
    public async Task LongLists_AreShortened()
    {
        for (var i = 1; i <= 5; i++) Norm(SizeM, $"CT{i}", length: null, id: i);

        (await Check(Whole(SizeM, 1))).Message.Should().EndWith("CT1 Đen - M, CT2 Đen - M, CT3 Đen - M và 2 mục khác.");
    }

    [Fact]
    public async Task FabricWithoutWidth_OrNotInMeters_CannotBeEstimated()
    {
        Norm(SizeM, material: new Material { MaterialId = 7, MaterialCode = "V07", MaterialName = "Vải", Unit = "m", Color = "", MaterialType = MaterialTypes.Fabric });
        (await Check(Whole(SizeM, 1))).Message.Should().Be("V07 Vải: chưa tính được vì NVL chưa có khổ vải.");

        _norms.Clear();
        Norm(SizeM, material: new Material { MaterialId = 8, MaterialCode = "V08", MaterialName = "Vải", Unit = "kg", Color = "", MaterialType = MaterialTypes.Fabric, WidthCm = 150 });
        (await Check(Whole(SizeM, 1))).Message.Should().Be("V08 Vải: chưa tính được vì NVL không tính theo mét.");
    }

    [Fact]
    public async Task ColorSizeWithoutFabricNorm_IsReported_NotSilentlyTreatedAsEnough()
    {
        var row = await Check(Whole(SizeM, 3));

        row.MaterialId.Should().Be(0);
        row.Status.Should().Be(FabricCheckStatuses.Unknown);
        row.Message.Should().Be("Chưa tính được vải cho Đen - M vì chưa có định mức vải.");
    }

    // ---------- phạm vi ----------

    [Fact]
    public async Task EachFabric_IsCheckedSeparately()
    {
        var lining = new Material { MaterialId = 7, MaterialCode = "V07", MaterialName = "Vải lót", Unit = "m", Color = "", MaterialType = MaterialTypes.Fabric, WidthCm = 150 };
        Norm(SizeM);
        Norm(SizeM, "Lót", length: 30, width: 50, material: lining, id: 12);

        var rows = await Sut().CheckAsync(OrderId, TeamId, [Whole(SizeM, 3)], default);

        rows.Select(r => (r.MaterialCode, r.NeedMeters)).Should().Equal(("V01", 0.7m), ("V07", 0.3m));
    }

    [Fact]
    public async Task TrimsDiscontinuedComponentsAndNonFabrics_AreIgnored()
    {
        Norm(SizeM);
        Norm(SizeM, "Khóa", type: ComponentTypes.Trim, id: 12);
        Norm(SizeM, "Cũ", status: RecordStatus.Discontinued, id: 13);
        Norm(SizeM, "Mex", material: new Material { MaterialId = 8, MaterialCode = "K1", MaterialName = "Mex", Unit = "m", Color = "", MaterialType = "TRIM" }, id: 14);

        (await Check(Whole(SizeM, 3))).NeedMeters.Should().Be(0.7m);
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
