using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_039–053 — C/Lookups, C/HrLookups, C/GradeCodeOptions.</summary>
public sealed class B1LookupTests(ITestOutputHelper output) : B1TestBase(output)
{
    private static HashSet<int> Ids(Api r, string name) =>
        r.Items().Select(i => B1Json.Int(i, name)).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();

    private static HashSet<string> Strs(Api r, string name) =>
        r.Items().Select(i => B1Json.Str(i, name)).Where(v => v is not null).Select(v => v!).ToHashSet();

    // ---------------------------------------------------------------- 039–046 Lookups

    [Fact]
    public async Task IT_B1_039_Lookups_Teams_Is_Open_To_Every_Authenticated_Role()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Lookups/teams", token);

        r.Code.Should().Be(200, r.ToString());
        Strs(r, "teamCode").Should().Contain(["T_SEW01", "T_SEW02", "T_CUT01", "T_EMPTY"]);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_040_Lookups_Teams_Filtered_By_Type_Matches_Db_Query()
    {
        var token = await TokenAsync(S0.W1);

        var r = await GetAsync(C + "Lookups/teams?teamType=Sewing", token);

        r.Code.Should().Be(200, r.ToString());
        r.Items().Select(i => B1Json.Str(i, "teamType")).Should().OnlyContain(t => string.Equals(t, "Sewing", StringComparison.OrdinalIgnoreCase));
        var codes = Strs(r, "teamCode");
        codes.Should().Contain(["T_SEW01", "T_SEW02", "T_EMPTY"]);
        codes.Should().NotContain("T_CUT01");
        codes.Should().NotContain("T_OLD", "[?] tổ đã ngừng không xuất hiện");

        await using var db = NewDb();
        var expected = await db.Teams.AsNoTracking()
            .Where(t => t.TeamType == TeamTypes.Sewing && t.Status != RecordStatus.Discontinued)
            .Select(t => t.TeamId).ToListAsync();
        Ids(r, "teamId").Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IT_B1_041_Lookups_Orders_Returns_Only_Open_Orders()
    {
        // Worker chỉ thấy đơn của tổ mình; Admin thấy toàn bộ đơn mở nên dùng TK_ADM để kiểm tra bộ lọc "đơn mở".
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Lookups/orders?keyword=ORD-IT", token);

        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "orderId");
        ids.Should().Contain(Ctx.OrderNew.OrderId);
        ids.Should().Contain(Ctx.OrderInProduction.OrderId);
        ids.Should().NotContain(Ctx.OrderCompleted.OrderId);
        ids.Should().NotContain(Ctx.OrderCancelled.OrderId);

        await using var db = NewDb();
        var statuses = await db.CustomerOrders.AsNoTracking().Where(o => ids.Contains(o.OrderId)).Select(o => o.Status).ToListAsync();
        statuses.Should().NotContain([OrderStatuses.Completed, OrderStatuses.Cancelled]);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_042_Lookups_Materials_Returns_Only_Active_Materials()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Lookups/materials", token);

        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "materialId");
        ids.Should().Contain(Ctx.MatFab01.MaterialId);
        ids.Should().Contain(Ctx.MatFab02.MaterialId);
        ids.Should().NotContain(Ctx.MatTrimOld.MaterialId);

        await using var db = NewDb();
        var statuses = await db.Materials.AsNoTracking().Where(m => ids.Contains(m.MaterialId)).Select(m => m.Status).ToListAsync();
        statuses.Should().OnlyContain(s => s == MaterialStatuses.Active);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_043_Lookups_Requisition_Materials_Scoped_To_Order_With_NonNegative_Available()
    {
        Db.OrderMaterials.AddRange(
            new OrderMaterial { OrderId = Ctx.OrderNew.OrderId, MaterialId = Ctx.MatFab01.MaterialId, RequiredQty = 100m, CustomerQty = 0m },
            new OrderMaterial { OrderId = Ctx.OrderNew.OrderId, MaterialId = Ctx.MatFab02.MaterialId, RequiredQty = 100m, CustomerQty = 0m });
        await Db.SaveChangesAsync();
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + $"Lookups/requisition-materials?orderId={Ctx.OrderNew.OrderId}", token);

        r.Code.Should().Be(200, r.ToString());
        r.Items().Should().NotBeEmpty();
        foreach (var item in r.Items())
            B1Json.Dec(item, "availableQty").Should().BeGreaterThanOrEqualTo(0m);

        await using var db = NewDb();
        var orderMaterialIds = await db.OrderMaterials.AsNoTracking()
            .Where(m => m.OrderId == Ctx.OrderNew.OrderId).Select(m => m.MaterialId).ToListAsync();
        Ids(r, "materialId").Should().BeSubsetOf(orderMaterialIds, "chỉ NVL của đơn X");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_044_Lookups_Order_Lines_NonInteger_Route_Returns_404()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Lookups/orders/abc/lines", token);

        r.Code.Should().Be(404, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_045_Lookups_Unknown_Order_And_OrderLine_Return_Empty_Or_404_Not_500()
    {
        var token = await TokenAsync(S0.W1);
        await using var db = NewDb();
        var missingOrder = (await db.CustomerOrders.AsNoTracking().MaxAsync(o => o.OrderId)) + 1_000_000;
        var before = await SnapshotAsync();

        var lines = await GetAsync(C + $"Lookups/orders/{missingOrder}/lines", token);
        var components = await GetAsync(C + $"Lookups/order-lines/{missingOrder}/components", token);

        lines.Code.Should().BeOneOf(200, 404);
        components.Code.Should().BeOneOf(200, 404);
        if (lines.Code == 200) lines.Items().Should().BeEmpty();
        if (components.Code == 200) components.Items().Should().BeEmpty();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_046_Lookups_Materials_Without_Token_Returns_401()
    {
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Lookups/materials");

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 047–051 HrLookups

    [Fact]
    public async Task IT_B1_047_HrLookups_Teams_Allowed_For_TeamLeader()
    {
        var token = await TokenAsync(S0.Tl1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "hr-lookups/teams", token);

        r.Code.Should().Be(200, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_048_HrLookups_Forbidden_For_Worker()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var teams = await GetAsync(C + "hr-lookups/teams", token);
        var members = await GetAsync(C + $"hr-lookups/team-members?teamId={S0.TSew01.TeamId}", token);

        teams.Code.Should().Be(403, teams.ToString());
        members.Code.Should().Be(403, members.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_049_HrLookups_TeamMembers_Missing_TeamId_Returns_400()
    {
        var token = await TokenAsync(S0.Acc);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "hr-lookups/team-members", token);

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_050_HrLookups_TeamMembers_Returns_Active_Members_Matching_Db()
    {
        var token = await TokenAsync(S0.Acc);

        var r = await GetAsync(C + $"hr-lookups/team-members?teamId={S0.TSew01.TeamId}", token);

        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "employeeId");
        ids.Should().Contain([S0.Tl1.EmployeeId, S0.Td1.EmployeeId, S0.W1.EmployeeId]);
        ids.Should().NotContain(S0.Rs.EmployeeId);

        await using var db = NewDb();
        var expected = await db.Employees.AsNoTracking()
            .Where(e => e.TeamId == S0.TSew01.TeamId && e.Status == RecordStatus.Active).Select(e => e.EmployeeId).ToListAsync();
        ids.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IT_B1_051_HrLookups_TeamMembers_Boundary_Team_Ids_Never_500()
    {
        var token = await TokenAsync(S0.Acc);
        var before = await SnapshotAsync();

        var empty = await GetAsync(C + $"hr-lookups/team-members?teamId={S0.TEmpty.TeamId}", token);
        var zero = await GetAsync(C + "hr-lookups/team-members?teamId=0", token);
        var max = await GetAsync(C + $"hr-lookups/team-members?teamId={int.MaxValue}", token);

        empty.Code.Should().Be(200, empty.ToString());
        empty.Items().Should().BeEmpty();
        zero.Code.Should().BeOneOf(200, 404);
        max.Code.Should().BeOneOf(200, 404);
        if (zero.Code == 200) zero.Items().Should().BeEmpty();
        if (max.Code == 200) max.Items().Should().BeEmpty();
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 052–053 GradeCodeOptions

    [Fact]
    public async Task IT_B1_052_GradeCodeOptions_Returns_Only_Active_Codes_Matching_Db()
    {
        var token = await TokenAsync(S0.Acc);

        var r = await GetAsync(C + "grade-code-options", token);

        r.Code.Should().Be(200, r.ToString());
        var codes = Strs(r, "gradeCode");
        codes.Should().Contain(["B1", "B2", "B3"]);
        codes.Should().NotContain("B9");

        await using var db = NewDb();
        var expected = await db.GradeLevelCodes.AsNoTracking()
            .Where(g => g.Status == GradeCodes.Active).Select(g => g.GradeCode).ToListAsync();
        codes.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IT_B1_053_GradeCodeOptions_Forbidden_For_Worker()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "grade-code-options", token);

        r.Code.Should().Be(403, r.ToString());
        await AssertDbUnchangedAsync(before);
    }
}
