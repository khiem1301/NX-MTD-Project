using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_080–093 — M/order-line-teams: phân tổ may cho dòng đơn (UQ_OrderLineTeam, rollback).</summary>
public sealed class B4OrderLineTeamTests(ITestOutputHelper output) : B4TestBase(output)
{
    private const string Url = M + "order-line-teams";

    private Task<Api> SetTeams(int lineId, params int[] teams) => Mgr(Put, Url, new { OrderLineId = lineId, TeamIds = teams });
    private Task<Api> AddTeams(int[] lines, int[] teams) => Mgr(Post, Url + "/bulk", new { OrderLineIds = lines, TeamIds = teams });

    private async Task GiveTeamAsync(int lineId, int teamId)
    {
        await using var db = NewDb();
        db.OrderLineTeams.Add(new OrderLineTeam { OrderLineId = lineId, SewingTeamId = teamId, AssignedAt = Clock.Now });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IT_B4_080_OnlyUnallotted_Filter()
    {
        var before = await Snap();

        var only = await Mgr(Get, Url + "?OnlyUnallotted=true&PageSize=100");
        var all = await Mgr(Get, Url + "?OnlyUnallotted=false&PageSize=100");

        only.Code.Should().Be(200, only.ToString());
        all.Code.Should().Be(200, all.ToString());
        var onlyIds = IdSet(only, "orderLineId");
        onlyIds.Should().Contain([D.Ol1.OrderLineId, D.Ol2.OrderLineId, D.Ol9.OrderLineId]);
        onlyIds.Should().NotContain(D.Ol3.OrderLineId);
        IdSet(all, "orderLineId").Should().Contain(D.Ol3.OrderLineId);
        await using (var db = NewDb())
        {
            var allotted = await db.OrderLineTeams.AsNoTracking().Select(t => t.OrderLineId).Distinct().ToListAsync();
            onlyIds.Should().NotIntersectWith(allotted);
        }

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_081_Teams_Of_Order_Product()
    {
        var before = await Snap();

        var ok = await Mgr(Get, Url + $"/teams?orderId={D.Od2.OrderId}&productId={B.Pr1.ProductId}");
        var noOrder = await Mgr(Get, Url + $"/teams?productId={B.Pr1.ProductId}");
        var wrongProduct = await Mgr(Get, Url + $"/teams?orderId={D.Od2.OrderId}&productId={B.Pr2.ProductId}");

        ok.Code.Should().Be(200, ok.ToString());
        ok.Body.Should().Contain(S0.TSew01.TeamCode);
        Rejected(noOrder, 400);
        wrongProduct.ShouldNotBeServerError();
        wrongProduct.Code.Should().BeOneOf(200, 400);
        if (wrongProduct.Code == 200) wrongProduct.Items().Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_082_Set_Two_Teams_On_Unallotted_Line()
    {
        var r = await SetTeams(D.Ol1.OrderLineId, S0.TSew01.TeamId, S0.TSew02.TeamId);

        r.Code.Should().Be(200, r.ToString());
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().BeEquivalentTo([S0.TSew01.TeamId, S0.TSew02.TeamId]);
        await using var db = NewDb();
        foreach (var t in await db.OrderLineTeams.AsNoTracking().Where(t => t.OrderLineId == D.Ol1.OrderLineId).ToListAsync()) AssertCreatedNow(t.AssignedAt);
    }

    [Fact]
    public async Task IT_B4_083_Set_Replaces_Whole_List()
    {
        var r = await SetTeams(D.Ol3.OrderLineId, S0.TSew02.TeamId);

        r.Code.Should().Be(200, r.ToString());
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().Equal(S0.TSew02.TeamId);
    }

    [Fact]
    public async Task IT_B4_084_Empty_List_Removes_Allotment()
    {
        var r = await SetTeams(D.Ol3.OrderLineId);

        r.Code.Should().Be(200, r.ToString());
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B4_085_Cutting_Team_Is_Rejected()
    {
        var before = await Snap();

        var r = await SetTeams(D.Ol1.OrderLineId, S0.TCut01.TeamId);

        Rejected(r, 400);
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_086_Discontinued_Or_Unknown_Team_Is_Rejected()
    {
        var before = await Snap();
        var missing = await NonExistingTeamIdAsync();

        Rejected(await SetTeams(D.Ol1.OrderLineId, S0.TOld.TeamId), 400);
        Rejected(await SetTeams(D.Ol1.OrderLineId, missing), 400, 404);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_087_Duplicate_Team_In_List_Does_Not_Violate_Unique()
    {
        var r = await SetTeams(D.Ol1.OrderLineId, S0.TSew01.TeamId, S0.TSew01.TeamId);

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        r.Body.Should().NotContainAny(Leaks);
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Count(t => t == S0.TSew01.TeamId).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task IT_B4_088_Failed_Set_Keeps_Old_Teams()
    {
        await GiveTeamAsync(D.Ol1.OrderLineId, S0.TSew01.TeamId);
        var before = await Snap();
        var missing = await NonExistingTeamIdAsync();

        var r = await SetTeams(D.Ol1.OrderLineId, S0.TSew02.TeamId, missing);

        Rejected(r, 400, 404);
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().Equal(S0.TSew01.TeamId);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_089_Unknown_Line_And_Cancelled_Order_Line()
    {
        var before = await Snap();

        Rejected(await SetTeams(await MissingOrderLineIdAsync(), S0.TSew01.TeamId), 404);
        Rejected(await SetTeams(D.Ol3Cancelled.OrderLineId, S0.TSew01.TeamId), 400, 409);

        (await TeamIdsAsync(D.Ol3Cancelled.OrderLineId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_090_Bulk_Adds_Without_Reinserting_Existing_Pairs()
    {
        var r = await AddTeams([D.Ol1.OrderLineId, D.Ol3.OrderLineId], [S0.TSew01.TeamId, S0.TSew02.TeamId]);

        r.Code.Should().Be(200, r.ToString());
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().BeEquivalentTo([S0.TSew01.TeamId, S0.TSew02.TeamId]);
        (await TeamIdsAsync(D.Ol3.OrderLineId)).Should().BeEquivalentTo([S0.TSew01.TeamId, S0.TSew02.TeamId]);
        await using var db = NewDb();
        (await db.OrderLineTeams.AsNoTracking().CountAsync(t => t.OrderLineTeamId == D.Olt3.OrderLineTeamId && t.SewingTeamId == S0.TSew01.TeamId)).Should().Be(1, "cặp cũ giữ nguyên");
    }

    [Fact]
    public async Task IT_B4_091_Bulk_With_Unknown_Line_Rolls_Back_Whole_Batch()
    {
        var before = await Snap();

        var r = await AddTeams([D.Ol1.OrderLineId, await MissingOrderLineIdAsync()], [S0.TSew01.TeamId]);

        Rejected(r, 400, 404);
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_092_Bulk_With_Cutting_Team_Rolls_Back()
    {
        var before = await Snap();

        var r = await AddTeams([D.Ol1.OrderLineId], [S0.TSew01.TeamId, S0.TCut01.TeamId]);

        Rejected(r, 400);
        (await TeamIdsAsync(D.Ol1.OrderLineId)).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_093_Bulk_Empty_Or_Null_Lists_Return_400()
    {
        var before = await Snap();

        Rejected(await AddTeams([], [S0.TSew01.TeamId]), 400);
        Rejected(await AddTeams([D.Ol1.OrderLineId], []), 400);
        Rejected(await Mgr(Post, Url + "/bulk", new { OrderLineIds = (int[]?)null, TeamIds = new[] { S0.TSew01.TeamId } }), 400);

        await Same(before);
    }
}

