using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-49: người thuộc tổ may chỉ thấy đơn + mã hàng được chia cho tổ trong các ô chọn đơn.</summary>
public class SewingTeamLookupScopeTests : ProductionTestBase
{
    private const int TeamId = 5;

    private readonly List<CustomerOrder> _orders = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderLineTeam> _allotted = [];
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly LookupService _sut;

    public SewingTeamLookupScopeTests()
    {
        SetupQueryable(_orders);
        SetupQueryable(_lines);
        SetupQueryable(_allotted);
        Order(1, (11, 3, "MH3"), (12, 4, "MH4"));
        Order(2, (21, 3, "MH3"));
        Order(3, (31, 3, "MH3"));
        _orders[2].Status = OrderStatuses.Cancelled;
        _allotted.Add(new OrderLineTeam { OrderLineId = 11, SewingTeamId = TeamId });
        _allotted.Add(new OrderLineTeam { OrderLineId = 21, SewingTeamId = TeamId + 1 });
        _allotted.Add(new OrderLineTeam { OrderLineId = 31, SewingTeamId = TeamId });
        _sut = new LookupService(Uow, User, _stock);
    }

    private void Order(int id, params (int LineId, int ProductId, string Code)[] lines)
    {
        var order = new CustomerOrder { OrderId = id, OrderCode = "DH00" + id, Status = OrderStatuses.InProduction };
        foreach (var (lineId, productId, code) in lines)
        {
            var line = new OrderLine { OrderLineId = lineId, OrderId = id, ProductId = productId, Order = order }
                .WithVariant(TestVariant.Make(productId, code));
            order.OrderLines.Add(line);
            _lines.Add(line);
        }
        _orders.Add(order);
    }

    private void AsManagerOrOther(string role)
    {
        User.Role.Returns(role);
        User.TeamId.Returns((int?)null);
        User.TeamTypeCode.Returns((string?)null);
    }

    [Fact]
    public async Task GetRequisitionPiecesAsync_SewingTeam_ReturnsAllottedLinesWithSetStock()
    {
        AsTeamLeader(TeamId);
        _stock.GetSetAvailableAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, decimal> { [11] = 40m, [12] = 7m });

        var pieces = await _sut.GetRequisitionPiecesAsync(1, default);

        pieces.Select(p => (p.OrderLineId, p.AvailableQty)).Should().Equal((11, 40m));
    }

    [Fact]
    public async Task Orders_SewingTeamLeader_SeesOnlyOpenOrdersAllottedToOwnTeam()
    {
        AsTeamLeader(TeamId);

        (await _sut.GetOpenOrdersAsync(null, null, default)).Select(o => o.OrderId).Should().Equal(1);
    }

    [Fact]
    public async Task Orders_AllottedLineNoLongerInOrder_IsHidden()
    {
        AsTeamLeader(TeamId);
        _allotted[0].OrderLineId = 99;

        (await _sut.GetOpenOrdersAsync(null, null, default)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.WarehouseKeeper)]
    public async Task Orders_OtherRoles_SeeAllOpenOrders(string role)
    {
        AsManagerOrOther(role);

        (await _sut.GetOpenOrdersAsync(null, null, default)).Select(o => o.OrderId).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Orders_ManagerPicksSewingTeam_SeesOnlyOrdersAllottedToThatTeam()
    {
        AsManagerOrOther(Roles.Manager);

        (await _sut.GetOpenOrdersAsync(null, TeamId + 1, default)).Select(o => o.OrderId).Should().Equal(2);
    }

    [Fact]
    public async Task Orders_SewingTeamLeaderPassesAnotherTeam_StillSeesOwnTeamOnly()
    {
        AsTeamLeader(TeamId);

        (await _sut.GetOpenOrdersAsync(null, TeamId + 1, default)).Select(o => o.OrderId).Should().Equal(1);
    }

    [Fact]
    public async Task OrderLines_SewingTeamLeader_SeesOnlyAllottedLines()
    {
        AsTeamLeader(TeamId);

        (await _sut.GetOrderLinesAsync(1, default)).Select(l => l.OrderLineId).Should().Equal(11);
    }

    [Fact]
    public async Task OrderLines_SameProductOtherColorSize_IsHiddenFromTeam()
    {
        AsTeamLeader(TeamId);
        Order(4, (41, 3, "MH3"), (42, 3, "MH3"));
        _allotted.Add(new OrderLineTeam { OrderLineId = 41, SewingTeamId = TeamId });

        (await _sut.GetOrderLinesAsync(4, default)).Select(l => l.OrderLineId).Should().Equal(41);
    }

    [Fact]
    public async Task OrderLines_Manager_SeesAllLines()
    {
        AsManagerOrOther(Roles.Manager);

        (await _sut.GetOrderLinesAsync(1, default)).Select(l => l.OrderLineId).Should().BeEquivalentTo([11, 12]);
    }

    [Fact]
    public void Scope_CuttingTeamLeader_IsNotLimited()
    {
        User.Role.Returns(Roles.TeamLeader);
        User.TeamId.Returns(7);
        User.TeamTypeCode.Returns(TeamTypes.Cutting);

        SewingTeamScope.Of(User).Should().BeNull();
    }
}
