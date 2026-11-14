using System.Globalization;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Dòng và số lượng của đơn Xin NVL / Xin BTP (DEV-ALL-77): vải được lẻ 2 số, NVL khác vải và BTP chỉ số nguyên.
/// DEV-ALL-110: Xin BTP chọn dòng đơn của đơn hàng; tổ may xin NVL phải chọn đơn và chỉ NVL của đơn; số xin không vượt khả dụng kho.
/// </summary>
public class RequisitionRequestHandlerTests : ProductionTestBase
{
    private const int FabricId = 1;
    private const int ThreadId = 2;
    private const int OldFabricId = 3;
    private const int OrderId = 4;
    private const int LineId = 11;
    private const int OtherLineId = 12;
    private static readonly DateOnly Day = new(2026, 11, 5);

    private readonly List<Material> _materials =
    [
        new() { MaterialId = FabricId, MaterialName = "Vải kate", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
        new() { MaterialId = ThreadId, MaterialName = "Chỉ trắng", MaterialType = MaterialTypes.Trim, Status = MaterialStatuses.Active },
        new() { MaterialId = OldFabricId, MaterialName = "Vải cũ", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Discontinued },
    ];

    private readonly List<OrderMaterial> _orderMaterials = [new OrderMaterial { OrderId = OrderId, MaterialId = ThreadId }];

    private readonly List<OrderLine> _lines =
    [
        new OrderLine { OrderLineId = LineId, OrderId = OrderId, ProductId = 3 }.WithVariant(TestVariant.Make(3, "MH3")),
        new OrderLine { OrderLineId = OtherLineId, OrderId = OrderId + 1, ProductId = 3 }.WithVariant(TestVariant.Make(3, "MH3")),
    ];

    private readonly IStockService _stock = Substitute.For<IStockService>();

    public RequisitionRequestHandlerTests()
    {
        Clock.Today.Returns(Day);
        AsCuttingTeamLeader();
        SetupQueryable(_materials);
        SetupQueryable(_orderMaterials);
        SetupQueryable(_lines);
        GivenStock(Plenty);
    }

    private const decimal Plenty = 100_000_000m;

    private void GivenStock(decimal available)
    {
        _stock.GetMaterialAvailableAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IReadOnlyCollection<int>>().ToDictionary(id => id, _ => available));
        _stock.GetSetAvailableAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IReadOnlyCollection<int>>().ToDictionary(id => id, _ => available));
    }

    private readonly IWorkOrderFabricNeeds _needs = Substitute.For<IWorkOrderFabricNeeds>();

    private MaterialRequisitionRequestHandler Nvl() => new(Clock, Uow, User, _needs, _stock);

    private SemiFinishedRequisitionRequestHandler Btp() => new(Clock, Uow, _stock);

    private static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);

    private static RequestLineInput Item(int materialId, decimal qty) => new(materialId, null, null, null, qty);

    private static RequestLineInput Text(string description, decimal qty) => new(null, null, null, description, qty);

    private static RequestLineInput Piece(decimal qty, int orderLineId = LineId) => new(null, orderLineId, null, null, qty);

    private static RequestDraft Draft(string type, IReadOnlyList<RequestLineInput> lines, DateOnly? target = null, int? requestId = null,
        int? orderId = null) =>
        new(type, orderId, null, null, null, null, null, target, "Xin", "Nội dung", lines, requestId);

    private Task<Result> CheckNvl(params RequestLineInput[] lines) =>
        Nvl().ValidateAsync(Draft(RequestTypes.MaterialRequisition, lines), default);

    private Task<Result> CheckBtp(params RequestLineInput[] lines) =>
        Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, lines, orderId: OrderId), default);

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("1", true)]
    [InlineData("12.5", true)]
    [InlineData("12.34", true)]
    [InlineData("9999999", true)]
    [InlineData("12.345", false)]
    [InlineData("0.001", false)]
    [InlineData("0.005", false)]
    [InlineData("9999999.01", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    public async Task Fabric_AllowsUpToTwoDecimals(string qty, bool expected)
    {
        var result = await CheckNvl(Item(FabricId, D(qty)));

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.ErrorCode.Should().Be(ErrorCodes.RequestLinesInvalid);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("250", true)]
    [InlineData("9999999", true)]
    [InlineData("1.5", false)]
    [InlineData("0.5", false)]
    [InlineData("2.01", false)]
    [InlineData("0", false)]
    [InlineData("10000000", false)]
    public async Task NonFabricMaterial_MustBeWholeNumber(string qty, bool expected)
    {
        var result = await CheckNvl(Item(ThreadId, D(qty)));

        result.IsSuccess.Should().Be(expected);
    }

    [Fact]
    public async Task NonFabricMessage_NamesTheMaterial() =>
        (await CheckNvl(Item(ThreadId, 1.5m))).Message.Should().Contain("Chỉ trắng").And.Contain("số nguyên");

    [Fact]
    public async Task FabricMessage_NamesTheMaterial() =>
        (await CheckNvl(Item(FabricId, 1.234m))).Message.Should().Contain("Vải kate").And.Contain("2 chữ số");

    [Fact]
    public async Task MixedLines_EachFollowsItsOwnMaterialType()
    {
        (await CheckNvl(Item(FabricId, 12.5m), Item(ThreadId, 30))).IsSuccess.Should().BeTrue();
        (await CheckNvl(Item(FabricId, 12.5m), Item(ThreadId, 30.5m))).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Nvl_LineWithoutMaterialFromCatalog_IsRejected() =>
        (await CheckNvl(Text("Vải đỏ", 5))).Message.Should().Be(Messages.RequisitionMaterialRequired);

    [Theory]
    [InlineData(99)]
    [InlineData(OldFabricId)]
    public async Task Nvl_UnknownOrDiscontinuedMaterial_IsRejected(int materialId) =>
        (await CheckNvl(Item(materialId, 5))).Message.Should().Be(Messages.RequisitionMaterialInvalid);

    [Fact]
    public async Task Nvl_SameMaterialTwice_IsRejected() =>
        (await CheckNvl(Item(FabricId, 1), Item(FabricId, 2))).Message.Should().Be(Messages.RequisitionDuplicateItem);

    [Theory]
    [InlineData("1", true)]
    [InlineData("500", true)]
    [InlineData("1000000", true)]
    [InlineData("1000001", false)]
    [InlineData("0", false)]
    [InlineData("0.5", false)]
    [InlineData("2.5", false)]
    [InlineData("-3", false)]
    public async Task Btp_QuantityIsWholeNumberUpToOneMillion(string qty, bool expected) =>
        (await CheckBtp(Piece(D(qty)))).IsSuccess.Should().Be(expected);

    [Fact]
    public async Task Btp_SameOrderLineTwice_IsRejected() =>
        (await CheckBtp(Piece(1), Piece(2))).Message.Should().Be(Messages.RequisitionDuplicateItem);

    [Fact]
    public async Task Btp_LineWithoutAnyItem_IsRejected() =>
        (await CheckBtp(Text("   ", 5))).Message.Should().Be(Messages.RequisitionItemRequired);

    [Fact]
    public async Task ValidateAsync_BtpFreeTextLine_ReturnsFailure() =>
        (await CheckBtp(Text("Áo đỏ M", 5))).Message.Should().Be(Messages.RequisitionPieceLineRequired);

    [Fact]
    public async Task ValidateAsync_BtpWithoutOrder_ReturnsFailure() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Piece(1)]), default))
            .Message.Should().Be(Messages.RequisitionOrderRequired);

    [Fact]
    public async Task ValidateAsync_BtpLineOfOtherOrder_ReturnsFailure() =>
        (await CheckBtp(Piece(1, OtherLineId))).Message.Should().Be(Messages.RequisitionOrderLineNotInOrder);

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task ValidateAsync_BtpOverSetStock_ReturnsFailure(int qty, bool expected)
    {
        GivenStock(30);

        var result = await CheckBtp(Piece(qty));

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.Message.Should().Contain("MH3").And.Contain("khả dụng 30");
    }

    [Fact]
    public async Task NoLines_IsRejected()
    {
        (await CheckNvl()).Message.Should().Be(Messages.RequisitionLinesRequired);
        (await CheckBtp()).Message.Should().Be(Messages.RequisitionLinesRequired);
    }

    [Fact]
    public async Task Btp_MoreThan50Lines_IsRejected()
    {
        var lines = Enumerable.Range(1, 51).Select(i => Piece(1, i)).ToArray();

        (await CheckBtp(lines)).Message.Should().Be(string.Format(Messages.RequisitionTooManyLines, 50));
    }

    [Fact]
    public async Task TargetDateInThePast_IsRejectedWhenCreating() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Piece(1)], Day.AddDays(-1), orderId: OrderId), default))
            .IsSuccess.Should().BeFalse();

    [Fact]
    public async Task TargetDateToday_IsAccepted() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Piece(1)], Day, orderId: OrderId), default)).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task TargetDateInThePast_IsTolerated_WhenEditingAnExistingRequest() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Piece(1)], Day.AddDays(-1), requestId: 9, orderId: OrderId), default))
            .IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("12.5", true)]
    [InlineData("12.51", false)]
    public async Task ValidateAsync_NvlOverStock_ReturnsFailureNamingTheMaterial(string qty, bool expected)
    {
        GivenStock(12.5m);

        var result = await CheckNvl(Item(FabricId, D(qty)));

        result.IsSuccess.Should().Be(expected);
        if (!expected) result.Message.Should().Contain("Vải kate").And.Contain("khả dụng 12,5");
    }

    [Fact]
    public async Task ValidateAsync_NvlMaterialWithoutStock_ReturnsFailure()
    {
        _stock.GetMaterialAvailableAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, decimal>());

        (await CheckNvl(Item(ThreadId, 1))).Message.Should().Contain("khả dụng 0");
    }

    [Fact]
    public async Task ValidateAsync_SewingTeamNvlWithoutOrder_ReturnsFailure()
    {
        AsTeamLeader();

        (await CheckNvl(Item(ThreadId, 1))).Message.Should().Be(Messages.RequisitionOrderRequired);
    }

    [Fact]
    public async Task ValidateAsync_SewingTeamNvlNotInOrder_ReturnsFailure()
    {
        AsTeamLeader();

        var result = await Nvl().ValidateAsync(
            Draft(RequestTypes.MaterialRequisition, [Item(ThreadId, 1), Item(FabricId, 1)], orderId: OrderId), default);

        result.Message.Should().Be(string.Format(Messages.RequisitionMaterialNotInOrder, "Vải kate"));
    }

    [Fact]
    public async Task ValidateAsync_SewingTeamNvlOfOrder_Succeeds()
    {
        AsTeamLeader();

        (await Nvl().ValidateAsync(Draft(RequestTypes.MaterialRequisition, [Item(ThreadId, 5)], orderId: OrderId), default))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void HandlersCoverTheirOwnTypes()
    {
        Nvl().RequestType.Should().Be(RequestTypes.MaterialRequisition);
        Btp().RequestType.Should().Be(RequestTypes.SemiFinishedRequisition);
    }
}
