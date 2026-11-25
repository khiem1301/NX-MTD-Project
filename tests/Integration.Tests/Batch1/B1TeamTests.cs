using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_118–141 — Teams: danh sách, tạo/sửa, ngừng/khôi phục, thành viên, điều chuyển (qua prefix manager).</summary>
public sealed class B1TeamTests(ITestOutputHelper output) : B1TestBase(output)
{
    private string TeamUrl(int teamId, string action) => M + $"Teams/{teamId}/{action}";

    private async Task<int?> TeamOfAsync(int employeeId) => (await EmployeeAsync(employeeId)).TeamId;

    private async Task<bool> TeamExistsAsync(string code, string? typeSqlLiteral = null)
    {
        await using var db = NewDb();
        return await db.Teams.AsNoTracking().AnyAsync(t => t.TeamCode == code && (typeSqlLiteral == null || t.TeamType == typeSqlLiteral));
    }

    // ---------------------------------------------------------------- 118–119 list

    [Fact]
    public async Task IT_B1_118_Manager_Team_List_Hides_Discontinued_Teams()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await GetAsync(M + "Teams", token);

        r.Code.Should().Be(200, r.ToString());
        var codes = StrSet(r, "teamCode");
        codes.Should().Contain(["T_SEW01", "T_SEW02", "T_CUT01", "T_EMPTY"]);
        codes.Should().NotContain("T_OLD");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_119_Team_List_IncludeDiscontinued_And_Type_Filter()
    {
        // Cô lập S0: Test DB có thể đã có tổ Cutting khác (vd. "CAT"); ngừng chúng trong transaction (được Rollback) để S0 là tổ Cutting duy nhất.
        foreach (var foreign in await Db.Teams.Where(t => t.TeamType == "CUTTING" && t.TeamCode != "T_CUT01").ToListAsync())
            foreign.Status = "DISCONTINUED";
        await Db.SaveChangesAsync();
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var withOld = await GetAsync(M + "Teams?includeDiscontinued=true", token);
        var cutting = await GetAsync(M + "Teams?type=Cutting", token);

        withOld.Code.Should().Be(200, withOld.ToString());
        StrSet(withOld, "teamCode").Should().Contain("T_OLD");
        cutting.Code.Should().Be(200, cutting.ToString());
        StrSet(cutting, "teamCode").Should().BeEquivalentTo(new[] { "T_CUT01" });
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 120–125 create / update

    [Fact]
    public async Task IT_B1_120_Manager_Creates_Team_Active_With_CreatedAt()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(M + "Teams", token, new { TeamCode = "T_SEW03", TeamType = "Sewing" });

        ShouldBeCreated(r);
        await using var db = NewDb();
        var team = await db.Teams.AsNoTracking().SingleAsync(t => t.TeamCode == "T_SEW03");
        team.Status.Should().Be("ACTIVE");
        team.CreatedAt.Should().NotBe(default);
        await AssertSingleAuditAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B1_121_Create_Team_Code_Differing_Only_By_Case_Returns_409()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(M + "Teams", token, new { TeamCode = "t_sew01", TeamType = "Sewing" });

        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_122_Create_Team_With_Invalid_Type_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var packing = await PostAsync(M + "Teams", token, new { TeamCode = "T_PACK1", TeamType = "Packing" });

        packing.Code.Should().Be(400, packing.ToString());
        (await TeamExistsAsync("T_PACK1")).Should().BeFalse();
        await AssertDbUnchangedAsync(before);

        var lower = await PostAsync(M + "Teams", token, new { TeamCode = "T_LOWER", TeamType = "sewing" });

        lower.ShouldNotBeServerError();
        Note($"IT_B1_122 TeamType 'sewing' => {lower.Code}");
        (await TeamExistsAsync("T_LOWER")).Should().Be(lower.Code is 200 or 201);
    }

    [Fact]
    public async Task IT_B1_123_Team_Code_Length_20_Accepted_21_And_Empty_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var code20 = "T" + new string('A', 19);
        var code21 = "T" + new string('B', 20);
        var before = await SnapshotAsync();

        var tooLong = await PostAsync(M + "Teams", token, new { TeamCode = code21, TeamType = "Sewing" });
        var empty = await PostAsync(M + "Teams", token, new { TeamCode = "", TeamType = "Sewing" });

        tooLong.Code.Should().Be(400, tooLong.ToString());
        empty.Code.Should().Be(400, empty.ToString());
        await AssertDbUnchangedAsync(before);

        var ok = await PostAsync(M + "Teams", token, new { TeamCode = code20, TeamType = "Sewing" });

        ShouldBeCreated(ok);
        (await TeamExistsAsync(code20)).Should().BeTrue();
        (await TeamExistsAsync(code21)).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B1_124_Admin_Updates_Team_Type()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PutAsync(A + $"Teams/{S0.TEmpty.TeamId}", token, new { TeamCode = "T_EMPTY", TeamType = "Office" });

        r.Code.Should().Be(200, r.ToString());
        (await TeamExistsAsync("T_EMPTY", "Office")).Should().BeTrue("so sánh SQL theo collation CI: 'Office' = 'OFFICE'");
    }

    [Fact]
    public async Task IT_B1_125_Update_Team_To_Existing_Code_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(A + $"Teams/{S0.TEmpty.TeamId}", token, new { TeamCode = "T_SEW01", TeamType = "Sewing" });

        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 126–130 lifecycle

    [Fact]
    public async Task IT_B1_126_Discontinue_Empty_Team_Removes_It_From_Default_List()
    {
        var token = await TokenAsync(S0.Mgr);

        var r = await PostAsync(TeamUrl(S0.TEmpty.TeamId, "discontinue"), token);

        r.Code.Should().Be(200, r.ToString());
        var list = await GetAsync(M + "Teams", token);
        StrSet(list, "teamCode").Should().NotContain("T_EMPTY");
        (await TeamAsync(S0.TEmpty.TeamId)).Status.Should().Be("DISCONTINUED");
    }

    [Fact]
    public async Task IT_B1_127_Discontinue_Team_With_Members_Is_Consistent()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TSew02.TeamId, "discontinue"), token);

        r.Code.Should().BeOneOf(200, 400, 409);
        if (r.Code == 200)
        {
            await using var db = NewDb();
            (await db.Employees.AsNoTracking().CountAsync(e => e.TeamId == S0.TSew02.TeamId && e.Status == "ACTIVE")).Should().Be(0);
            (await TeamAsync(S0.TSew02.TeamId)).Status.Should().Be("DISCONTINUED");
        }
        else
        {
            await AssertDbUnchangedAsync(before);
        }

        Note($"IT_B1_127 discontinue tổ có thành viên => {r.Code}");
    }

    [Fact]
    public async Task IT_B1_128_Discontinue_Already_Discontinued_Team_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TOld.TeamId, "discontinue"), token);

        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_129_Reactivate_Discontinued_Team()
    {
        var token = await TokenAsync(S0.Mgr);

        var r = await PostAsync(TeamUrl(S0.TOld.TeamId, "reactivate"), token);

        r.Code.Should().Be(200, r.ToString());
        (await TeamAsync(S0.TOld.TeamId)).Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B1_130_Reactivate_Active_Team_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TSew01.TeamId, "reactivate"), token);

        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 131–137 members

    [Fact]
    public async Task IT_B1_131_Candidates_Without_And_With_TeamId()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var noTeam = await GetAsync(M + "Teams/candidates", token);
        var withTeam = await GetAsync(M + $"Teams/candidates?teamId={S0.TSew02.TeamId}", token);

        noTeam.Code.Should().Be(200, noTeam.ToString());
        var ids = IdSet(noTeam, "employeeId");
        ids.Should().Contain(S0.W3.EmployeeId);
        ids.Should().NotContain(S0.Rs.EmployeeId);
        withTeam.Code.Should().Be(200, withTeam.ToString());
        Note($"IT_B1_131 candidates?teamId=T_SEW02 => {IdSet(withTeam, "employeeId").Count} người");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_132_Add_Member_Without_Team_To_Team()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(TeamUrl(S0.TEmpty.TeamId, "add-members"), token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId }, Reason = "Bổ sung" });

        r.Code.Should().Be(200, r.ToString());
        (await TeamOfAsync(S0.W3.EmployeeId)).Should().Be(S0.TEmpty.TeamId);
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.W3.EmployeeId);
    }

    [Fact]
    public async Task IT_B1_133_Add_Members_With_One_Already_In_Team_Rolls_Back_All()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TEmpty.TeamId, "add-members"), token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId, S0.W1.EmployeeId }, Reason = "Bổ sung" });

        r.Code.Should().BeOneOf(400, 409);
        (await TeamOfAsync(S0.W3.EmployeeId)).Should().BeNull();
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(S0.TSew01.TeamId);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_134_Add_Members_Empty_List_Rejected_Duplicate_Id_Never_500()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var empty = await PostAsync(TeamUrl(S0.TEmpty.TeamId, "add-members"), token, new { EmployeeIds = Array.Empty<int>(), Reason = "Rỗng" });

        empty.Code.Should().Be(400, empty.ToString());
        await AssertDbUnchangedAsync(before);

        var dup = await PostAsync(TeamUrl(S0.TEmpty.TeamId, "add-members"), token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId, S0.W3.EmployeeId }, Reason = "Trùng" });

        dup.Code.Should().BeOneOf(200, 400);
        Note($"IT_B1_134 danh sách trùng id => {dup.Code}");
        if (dup.Code == 200) (await TeamOfAsync(S0.W3.EmployeeId)).Should().Be(S0.TEmpty.TeamId);
        else await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_135_Add_Resigned_Employee_Or_Add_Into_Discontinued_Team_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var resigned = await PostAsync(TeamUrl(S0.TSew02.TeamId, "add-members"), token,
            new { EmployeeIds = new[] { S0.Rs.EmployeeId }, Reason = "Đã nghỉ" });
        var intoOld = await PostAsync(TeamUrl(S0.TOld.TeamId, "add-members"), token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId }, Reason = "Vào tổ ngừng" });

        resigned.Code.Should().Be(400, resigned.ToString());
        intoOld.Code.Should().Be(400, intoOld.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_136_Remove_Member_Clears_TeamId()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(TeamUrl(S0.TSew02.TeamId, "remove-members"), token, new { EmployeeIds = new[] { S0.W2.EmployeeId } });

        r.Code.Should().Be(200, r.ToString());
        (await TeamOfAsync(S0.W2.EmployeeId)).Should().BeNull();
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.W2.EmployeeId);
    }

    [Fact]
    public async Task IT_B1_137_Remove_Employee_Not_In_Team_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TSew02.TeamId, "remove-members"), token, new { EmployeeIds = new[] { S0.W1.EmployeeId } });

        r.Code.Should().Be(400, r.ToString());
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(S0.TSew01.TeamId);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 138–141 transfer-in

    [Fact]
    public async Task IT_B1_138_Transfer_In_Moves_Worker_To_Target_Team()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(TeamUrl(S0.TSew02.TeamId, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W1.EmployeeId }, EffectiveDate = Clock.Today, Reason = "Điều chuyển" });

        r.Code.Should().Be(200, r.ToString());
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(S0.TSew02.TeamId);
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.W1.EmployeeId);
    }

    [Fact]
    public async Task IT_B1_139_Transfer_In_With_Unknown_Employee_Rolls_Back_All()
    {
        var token = await TokenAsync(S0.Mgr);
        var missing = await NonExistingEmployeeIdAsync();
        var before = await SnapshotAsync();

        var r = await PostAsync(TeamUrl(S0.TSew02.TeamId, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W1.EmployeeId, missing }, EffectiveDate = Clock.Today, Reason = "Điều chuyển" });

        r.Code.Should().BeOneOf(400, 404);
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(S0.TSew01.TeamId);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_140_Transfer_Into_Discontinued_Or_Current_Team_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var intoOld = await PostAsync(TeamUrl(S0.TOld.TeamId, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W1.EmployeeId }, EffectiveDate = Clock.Today, Reason = "Vào tổ ngừng" });
        var sameTeam = await PostAsync(TeamUrl(S0.TSew02.TeamId, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W2.EmployeeId }, EffectiveDate = Clock.Today, Reason = "Cùng tổ" });

        intoOld.Code.Should().Be(400, intoOld.ToString());
        sameTeam.Code.Should().Be(400, sameTeam.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_141_Transfer_In_Effective_Date_Boundaries_Never_500_And_Db_Consistent()
    {
        var token = await TokenAsync(S0.Mgr);
        var target = S0.TSew02.TeamId;
        string snapshotBeforeSilly;
        using var scope = new FluentAssertions.Execution.AssertionScope(); // gom lỗi cả 3 mốc ngày, không để ca đầu che ca sau

        // ngày hôm qua: W1 (T_SEW01) → T_SEW02
        var yesterday = await PostAsync(TeamUrl(target, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W1.EmployeeId }, EffectiveDate = PastDay(Clock.Today), Reason = "Hôm qua" });
        yesterday.Code.Should().BeOneOf(200, 400);
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(yesterday.Code == 200 ? target : S0.TSew01.TeamId);

        // ngày mai: W4 (T_CUT01) → T_SEW02
        var tomorrow = await PostAsync(TeamUrl(target, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W4.EmployeeId }, EffectiveDate = FutureDay(Clock.Today), Reason = "Ngày mai" });
        tomorrow.Code.Should().BeOneOf(200, 400);
        (await TeamOfAsync(S0.W4.EmployeeId)).Should().Be(tomorrow.Code == 200 ? target : S0.TCut01.TeamId);

        // ngày vô lý: W3 (chưa có tổ) → T_SEW02, bắt buộc bị từ chối và không đổi
        snapshotBeforeSilly = await SnapshotAsync();
        var silly = await PostAsync(TeamUrl(target, "transfer-in"), token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId }, EffectiveDate = new DateOnly(1, 1, 1), Reason = "Vô lý" });
        silly.Code.Should().Be(400, silly.ToString());
        await AssertDbUnchangedAsync(snapshotBeforeSilly);
        Note($"IT_B1_141 hôm qua => {yesterday.Code}; ngày mai => {tomorrow.Code}");
    }
}
