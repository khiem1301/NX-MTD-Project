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

/// <summary>Dòng và số lượng của đơn Xin NVL / Xin BTP (DEV-ALL-77): vải được lẻ 2 số, NVL khác vải và BTP chỉ số nguyên.</summary>
public class RequisitionRequestHandlerTests : ProductionTestBase
{
    private const int FabricId = 1;
    private const int ThreadId = 2;
    private const int OldFabricId = 3;
    private static readonly DateOnly Day = new(2026, 11, 5);

    private readonly List<Material> _materials =
    [
        new() { MaterialId = FabricId, MaterialName = "Vải kate", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Active },
        new() { MaterialId = ThreadId, MaterialName = "Chỉ trắng", MaterialType = MaterialTypes.Trim, Status = MaterialStatuses.Active },
        new() { MaterialId = OldFabricId, MaterialName = "Vải cũ", MaterialType = MaterialTypes.Fabric, Status = MaterialStatuses.Discontinued },
    ];

    public RequisitionRequestHandlerTests()
    {
        Clock.Today.Returns(Day);
        SetupQueryable(_materials);
    }

    private readonly IWorkOrderFabricNeeds _needs = Substitute.For<IWorkOrderFabricNeeds>();

    private MaterialRequisitionRequestHandler Nvl() => new(Clock, Uow, User, _needs);

    private SemiFinishedRequisitionRequestHandler Btp() => new(Clock);

    private static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);

    private static RequestLineInput Item(int materialId, decimal qty) => new(materialId, null, null, null, qty);

    private static RequestLineInput Text(string description, decimal qty) => new(null, null, null, description, qty);

    private static RequestDraft Draft(string type, IReadOnlyList<RequestLineInput> lines, DateOnly? target = null, int? requestId = null) =>
        new(type, null, null, null, null, null, null, target, "Xin", "Nội dung", lines, requestId);

    private Task<Result> CheckNvl(params RequestLineInput[] lines) =>
        Nvl().ValidateAsync(Draft(RequestTypes.MaterialRequisition, lines), default);

    private Task<Result> CheckBtp(params RequestLineInput[] lines) =>
        Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, lines), default);

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
        (await CheckBtp(Text("Áo AO-01 Đỏ M", D(qty)))).IsSuccess.Should().Be(expected);

    [Fact]
    public async Task Btp_SameItemTwice_IsRejected_IgnoringCaseAndSpaces() =>
        (await CheckBtp(Text("Áo đỏ M", 1), Text(" ÁO ĐỎ M ", 2))).Message.Should().Be(Messages.RequisitionDuplicateItem);

    [Fact]
    public async Task Btp_LineWithoutAnyItem_IsRejected() =>
        (await CheckBtp(Text("   ", 5))).Message.Should().Be(Messages.RequisitionItemRequired);

    [Fact]
    public async Task NoLines_IsRejected()
    {
        (await CheckNvl()).Message.Should().Be(Messages.RequisitionLinesRequired);
        (await CheckBtp()).Message.Should().Be(Messages.RequisitionLinesRequired);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task Btp_MaxLines50(int count, bool expected)
    {
        var lines = Enumerable.Range(1, count).Select(i => Text($"Hàng {i}", 1)).ToArray();

        (await CheckBtp(lines)).IsSuccess.Should().Be(expected);
    }

    [Fact]
    public async Task TargetDateInThePast_IsRejectedWhenCreating() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Text("Áo", 1)], Day.AddDays(-1)), default))
            .IsSuccess.Should().BeFalse();

    [Fact]
    public async Task TargetDateToday_IsAccepted() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Text("Áo", 1)], Day), default)).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task TargetDateInThePast_IsTolerated_WhenEditingAnExistingRequest() =>
        (await Btp().ValidateAsync(Draft(RequestTypes.SemiFinishedRequisition, [Text("Áo", 1)], Day.AddDays(-1), requestId: 9), default))
            .IsSuccess.Should().BeTrue();

    [Fact]
    public void HandlersCoverTheirOwnTypes()
    {
        Nvl().RequestType.Should().Be(RequestTypes.MaterialRequisition);
        Btp().RequestType.Should().Be(RequestTypes.SemiFinishedRequisition);
    }
}
