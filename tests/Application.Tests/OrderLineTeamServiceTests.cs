using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-49: Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may.</summary>
public class OrderLineTeamServiceTests : ProductionTestBase
{
    private const int OrderId = 1;
    private const int ProductId = 3;
    private const int LineRed = 11;
    private const int LineBlue = 12;
    private const int TeamA = 5;
    private const int TeamB = 6;

    private readonly List<CustomerOrder> _orders =
    [
        new() { OrderId = OrderId, OrderCode = "DH001", Status = OrderStatuses.InProduction, DueDate = new DateOnly(2026, 11, 20) },
        new() { OrderId = 2, OrderCode = "DH002", Status = OrderStatuses.Cancelled },
    ];
    private readonly List<OrderLine> _lines = [];
    private readonly List<Team> _teams =
    [
        new() { TeamId = TeamA, TeamName = "May 1", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
        new() { TeamId = TeamB, TeamName = "May 2", TeamType = TeamTypes.Sewing, Status = RecordStatus.Active },
        new() { TeamId = 7, TeamName = "Cắt", TeamType = TeamTypes.Cutting, Status = RecordStatus.Active },
    ];
    private readonly List<OrderLineTeam> _allotted = [];
    private readonly List<OperationAssignment> _assignments = [];
    private readonly List<MaterialIssueLine> _issueLines = [];
    private readonly List<KpiTarget> _kpis = [];
    private readonly OrderLineTeamService _sut;

    public OrderLineTeamServiceTests()
    {
        SetupQueryable(_orders);
        SetupQueryable(_lines);
        SetupQueryable(_teams);
        SetupQueryable(_allotted);
        SetupQueryable(_assignments);
        SetupQueryable(_issueLines);
        SetupQueryable(_kpis);
        Line(LineRed, OrderId, ProductId, "MH3", "Đỏ", "M", 30);
        Line(LineBlue, OrderId, ProductId, "MH3", "Xanh", "L", 20);
        _sut = new OrderLineTeamService(Uow, Audit, Notifier);
    }

    private OrderLine Line(int id, int orderId, int productId, string code, string color, string size, int qty)
    {
        var line = new OrderLine
        {
            OrderLineId = id, OrderId = orderId, ProductId = productId, OrderedQty = qty,
            Order = _orders.First(o => o.OrderId == orderId),
        }.WithVariant(TestVariant.Make(productId, code, color, size));
        _lines.Add(line);
        return line;
    }

    private OrderLineTeam Allot(int teamId, int lineId = LineRed)
    {
        var row = new OrderLineTeam
        {
            OrderLineId = lineId, SewingTeamId = teamId, SewingTeam = _teams.First(t => t.TeamId == teamId),
        };
        _allotted.Add(row);
        return row;
    }

    private Task<Result> Set(params int[] teamIds) => _sut.SetTeamsAsync(new SetOrderLineTeamsDto(LineRed, teamIds), default);

    private void GivenAssignment(int teamId, DateOnly? validTo = null) => _assignments.Add(new OperationAssignment
    {
        OrderId = OrderId, SewingTeamId = teamId, ValidTo = validTo, Operation = new Operation { ProductId = ProductId },
    });

    [Fact]
    public async Task GetPaged_OneRowPerColorSizeWithItsOwnTeams_SkipsClosedOrders()
    {
        Line(21, 2, ProductId, "MH3", "Đỏ", "M", 99);
        Allot(TeamB);

        var page = (await _sut.GetPagedAsync(new OrderLineTeamQuery(), default)).Data!;

        page.Items.Select(r => (r.OrderLineId, r.Color, r.SizeCode, r.OrderedQty)).Should().Equal((LineRed, "Đỏ", "M", 30), (LineBlue, "Xanh", "L", 20));
        page.Items[0].Teams.Should().Equal(new AllottedTeamDto(TeamB, "May 2"));
        page.Items[1].Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPaged_OnlyUnallotted_HidesAllottedLines()
    {
        Allot(TeamA);

        var page = (await _sut.GetPagedAsync(new OrderLineTeamQuery { OnlyUnallotted = true }, default)).Data!;

        page.Items.Select(r => r.OrderLineId).Should().Equal(LineBlue);
    }

    [Theory]
    [InlineData("Xanh", LineBlue)]
    [InlineData("L", LineBlue)]
    public async Task GetPaged_KeywordMatchesColorOrSize(string keyword, int lineId)
    {
        var page = (await _sut.GetPagedAsync(new OrderLineTeamQuery { Keyword = keyword }, default)).Data!;

        page.Items.Select(r => r.OrderLineId).Should().Equal(lineId);
    }

    [Fact]
    public async Task SetTeams_AddsTeamsNotifiesLeadersWithColorSizeAndWritesAudit()
    {
        var result = await Set(TeamA, TeamB);

        result.IsSuccess.Should().BeTrue();
        await Repo<OrderLineTeam>().Received(1).AddRangeAsync(Arg.Is<IEnumerable<OrderLineTeam>>(rows =>
            rows.All(r => r.OrderLineId == LineRed) && rows.Select(r => r.SewingTeamId).SequenceEqual(new[] { TeamA, TeamB })));
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamA, Arg.Is<NotificationMessage>(m => m.Message.Contains("MH3 Đỏ/M") && m.Message.Contains("DH001")), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamB, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(OrderLineTeam) && e.RecordId == LineRed));
        await Uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SetTeams_SameTeams_DoesNothing()
    {
        Allot(TeamA);

        (await Set(TeamA)).IsSuccess.Should().BeTrue();

        await Uow.DidNotReceive().SaveChangesAsync();
        await Notifier.DidNotReceiveWithAnyArgs().NotifyTeamLeadersAsync(default, default!, default);
    }

    [Fact]
    public async Task SetTeams_RemovesTeamWithoutWork()
    {
        var row = Allot(TeamA);

        (await Set()).IsSuccess.Should().BeTrue();

        Repo<OrderLineTeam>().Received(1).RemoveRange(Arg.Is<IEnumerable<OrderLineTeam>>(rows => rows.Single() == row));
        await Uow.Received(1).SaveChangesAsync();
    }

    [Theory]
    [InlineData(7)]    // tổ cắt không chia
    [InlineData(99)]   // không tồn tại
    public async Task SetTeams_NotASewingTeam_IsRejected(int teamId)
    {
        (await Set(teamId)).ErrorCode.Should().Be(ErrorCodes.OrderLineTeamInvalid);

        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task SetTeams_InactiveSewingTeam_IsRejected()
    {
        _teams.First(t => t.TeamId == TeamB).Status = "INACTIVE";

        (await Set(TeamB)).Message.Should().Be(Messages.OrderLineTeamTeamInvalid);
    }

    [Fact]
    public async Task SetTeams_ClosedOrder_IsRejected()
    {
        Line(21, 2, ProductId, "MH3", "Đỏ", "M", 99);

        var result = await _sut.SetTeamsAsync(new SetOrderLineTeamsDto(21, [TeamA]), default);

        result.ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);
    }

    [Fact]
    public async Task SetTeams_LineNotFound_IsNotFound()
    {
        var result = await _sut.SetTeamsAsync(new SetOrderLineTeamsDto(999, [TeamA]), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task SetTeams_RemoveLastLineOfProductWithCurrentAssignment_IsRejected()
    {
        Allot(TeamA);
        GivenAssignment(TeamA);

        var result = await Set();

        result.ErrorCode.Should().Be(ErrorCodes.OrderLineTeamInUse);
        result.Message.Should().Be(string.Format(Messages.OrderLineTeamInUse, "May 1"));
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task SetTeams_RemoveLineWhileTeamKeepsOtherColorOfProduct_IsAllowedDespiteAssignmentAndKpi()
    {
        Allot(TeamA);
        Allot(TeamA, LineBlue);
        GivenAssignment(TeamA);
        _kpis.Add(new KpiTarget { OrderId = OrderId, ProductId = ProductId, SewingTeamId = TeamA, Status = KpiStatuses.Active });

        (await Set()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SetTeams_RemoveTeamWithClosedAssignmentOnly_IsAllowed()
    {
        Allot(TeamA);
        GivenAssignment(TeamA, Today);

        (await Set()).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(KpiStatuses.Active, false)]
    [InlineData(KpiStatuses.Closed, false)]
    [InlineData(KpiStatuses.Cancelled, true)]
    public async Task SetTeams_RemoveLastLineWithKpiTarget_DependsOnKpiStatus(string status, bool allowed)
    {
        Allot(TeamA);
        _kpis.Add(new KpiTarget { OrderId = OrderId, ProductId = ProductId, SewingTeamId = TeamA, Status = status });

        (await Set()).IsSuccess.Should().Be(allowed);
    }

    private void GivenIssue(int lineId, string status) => _issueLines.Add(new MaterialIssueLine
    {
        OrderLineId = lineId,
        MaterialIssue = new MaterialIssue
        {
            OrderId = OrderId, ReceivingTeamId = TeamA, WarehouseType = WarehouseTypes.SemiFinished, Status = status,
        },
    });

    [Theory]
    [InlineData(IssueStatuses.PendingReceipt, false)]
    [InlineData(IssueStatuses.Received, false)]
    [InlineData(IssueStatuses.Rejected, true)]
    public async Task SetTeams_RemoveTeamWithIssueOfThatLine_DependsOnIssueStatus(string status, bool allowed)
    {
        Allot(TeamA);
        Allot(TeamA, LineBlue);   // còn dòng khác của mã vẫn không bỏ được dòng đã cấp BTP
        GivenIssue(LineRed, status);

        (await Set()).IsSuccess.Should().Be(allowed);
    }

    [Fact]
    public async Task SetTeams_IssueOfOtherColorOnly_DoesNotBlockThisLine()
    {
        Allot(TeamA);
        Allot(TeamA, LineBlue);
        GivenIssue(LineBlue, IssueStatuses.Received);

        (await Set()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ListTeams_ReturnsEachActiveTeamOnceAcrossLinesOfThatProductOnly()
    {
        Allot(TeamA);
        Allot(TeamA, LineBlue);
        Allot(TeamB);
        _teams.First(t => t.TeamId == TeamB).Status = "DISCONTINUED";
        Line(13, OrderId, 4, "MH4", "Đỏ", "M", 10);
        Allot(7, 13);

        var result = await _sut.ListTeamsAsync(OrderId, ProductId, default);

        result.Data.Should().Equal(new AllottedTeamDto(TeamA, "May 1"));
    }

    private static bool IsMissingPairs(IEnumerable<OrderLineTeam> rows) =>
        rows.Select(r => (r.OrderLineId, r.SewingTeamId)).ToHashSet().SetEquals(new[] { (LineRed, TeamB), (LineBlue, TeamA), (LineBlue, TeamB) });

    [Fact]
    public async Task AddTeams_AddsOnlyMissingPairsNotifiesOncePerTeamAndKeepsExistingTeams()
    {
        var kept = Allot(TeamA);

        var result = await _sut.AddTeamsAsync(new AddOrderLineTeamsDto([LineRed, LineBlue], [TeamA, TeamB]), default);

        result.Data.Should().Be(3);
        await Repo<OrderLineTeam>().Received(1).AddRangeAsync(Arg.Is<IEnumerable<OrderLineTeam>>(rows => IsMissingPairs(rows)));
        Repo<OrderLineTeam>().DidNotReceiveWithAnyArgs().RemoveRange(default!);
        _allotted.Should().Contain(kept);
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamA, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await Notifier.Received(1).NotifyTeamLeadersAsync(TeamB, Arg.Is<NotificationMessage>(m => m.Message.Contains("MH3 Đỏ/M") && m.Message.Contains("MH3 Xanh/L")), Arg.Any<CancellationToken>());
        await Uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task AddTeams_AllAlreadyAllotted_ReturnsZeroAndSavesNothing()
    {
        Allot(TeamA);

        var result = await _sut.AddTeamsAsync(new AddOrderLineTeamsDto([LineRed], [TeamA]), default);

        result.Data.Should().Be(0);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task AddTeams_OneLineMissingOrClosed_RejectsWholeRequest()
    {
        Line(21, 2, ProductId, "MH3", "Đỏ", "M", 99);

        (await _sut.AddTeamsAsync(new AddOrderLineTeamsDto([LineRed, 999], [TeamA]), default)).Kind.Should().Be(ResultKind.NotFound);
        (await _sut.AddTeamsAsync(new AddOrderLineTeamsDto([LineRed, 21], [TeamA]), default)).ErrorCode.Should().Be(ErrorCodes.AssignmentOrderInvalid);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task AddTeams_CuttingTeam_IsRejected()
    {
        var result = await _sut.AddTeamsAsync(new AddOrderLineTeamsDto([LineRed], [7]), default);

        result.ErrorCode.Should().Be(ErrorCodes.OrderLineTeamInvalid);
    }

    public static TheoryData<int[], int[]> InvalidAddDtos => new()
    {
        { [], [TeamA] },
        { [LineRed], [] },
        { [0], [TeamA] },
        { [LineRed], [-1] },
        { [LineRed, LineRed], [TeamA] },
        { [LineRed], [TeamA, TeamA] },
        { Enumerable.Range(1, AssignmentLimits.MaxOrderLines + 1).ToArray(), [TeamA] },
    };

    [Theory]
    [MemberData(nameof(InvalidAddDtos))]
    public void AddValidator_RejectsBoundaryValues(int[] lineIds, int[] teamIds) =>
        new AddOrderLineTeamsDtoValidator().Validate(new AddOrderLineTeamsDto(lineIds, teamIds)).IsValid.Should().BeFalse();

    [Fact]
    public void AddValidator_AcceptsMaxLines() =>
        new AddOrderLineTeamsDtoValidator().Validate(new AddOrderLineTeamsDto(
            Enumerable.Range(1, AssignmentLimits.MaxOrderLines).ToArray(), [TeamA])).IsValid.Should().BeTrue();
}
