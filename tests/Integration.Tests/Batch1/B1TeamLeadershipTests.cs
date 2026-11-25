using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>
/// IT_B1_142–164 (trừ 151 ở B1ConcurrencyTests) — trigger trg_Employee_TeamLeadership: bổ nhiệm / miễn nhiệm / va chạm chức vụ.
/// Mọi ca: không 5xx, không lộ lỗi SQL; bất biến ≤1 Leader và ≤1 Deputy mỗi tổ.
/// </summary>
public sealed class B1TeamLeadershipTests(ITestOutputHelper output) : B1TestBase(output)
{
    private Task<Api> AppointAsync(string token, int teamId, int employeeId, string position) =>
        PostAsync(M + $"Teams/{teamId}/appoint", token, new { EmployeeId = employeeId, Position = position });

    private Task<Api> DismissAsync(string token, int teamId, int employeeId) =>
        PostAsync(M + $"Teams/{teamId}/dismiss", token, new { EmployeeId = employeeId });

    private Task<Api> TransferAsync(string token, int teamId, params int[] ids) =>
        PostAsync(M + $"Teams/{teamId}/transfer-in", token, new { EmployeeIds = ids, EffectiveDate = Clock.Today, Reason = "Điều chuyển" });

    private async Task<int?> TeamOfAsync(int employeeId) => (await EmployeeAsync(employeeId)).TeamId;

    // ---------------------------------------------------------------- 142–150 appoint

    [Fact]
    public async Task IT_B1_142_Appoint_Second_Leader_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await AppointAsync(token, S0.TSew01.TeamId, S0.W1.EmployeeId, "Leader");

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        r.Message.Should().NotBeNullOrWhiteSpace();
        (await RoleCodeOfAsync(S0.W1.EmployeeId)).Should().Be(RoleCodes.Worker);
        (await RoleCodeOfAsync(S0.Tl1.EmployeeId)).Should().Be(RoleCodes.TeamLeader);
        await AssertLeadershipInvariantAsync();
        await AssertDbUnchangedAsync(before, "AuditLogs không thêm dòng");
    }

    [Fact]
    public async Task IT_B1_143_Appoint_Second_Deputy_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await AppointAsync(token, S0.TSew01.TeamId, S0.W1.EmployeeId, "Deputy");

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        await AssertLeadershipInvariantAsync();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_144_Appoint_Leader_To_Team_Without_Leader()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await AppointAsync(token, S0.TSew02.TeamId, S0.W2.EmployeeId, "Leader");

        r.Code.Should().Be(200, r.ToString());
        var w2 = await EmployeeAsync(S0.W2.EmployeeId);
        w2.Role.RoleCode.Should().Be(RoleCodes.TeamLeader);
        w2.TeamId.Should().Be(S0.TSew02.TeamId);
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.W2.EmployeeId);
        await AssertLeadershipInvariantAsync();

        var detail = await GetAsync(M + $"Teams/{S0.TSew02.TeamId}", token);
        detail.Code.Should().Be(200, detail.ToString());
        var history = B1Json.Prop(detail.Data, "history");
        history.Should().NotBeNull();
        history!.Value.GetArrayLength().Should().BeGreaterThanOrEqualTo(1, "TeamDetailDto.History có mục bổ nhiệm");
    }

    [Fact]
    public async Task IT_B1_145_Appoint_Deputy_To_Team_Without_Deputy()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await AppointAsync(token, S0.TCut01.TeamId, S0.W4.EmployeeId, "Deputy");

        r.Code.Should().Be(200, r.ToString());
        (await RoleCodeOfAsync(S0.W4.EmployeeId)).Should().Be(RoleCodes.TeamDeputy);
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.W4.EmployeeId);
        await AssertLeadershipInvariantAsync();
    }

    [Fact]
    public async Task IT_B1_146_Appoint_Non_Member_Of_Team_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await AppointAsync(token, S0.TSew02.TeamId, S0.W1.EmployeeId, "Leader");

        AssertGraceful(r);
        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_147_Leader_Cannot_Be_Appointed_Deputy_Of_Same_Team()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await AppointAsync(token, S0.TSew01.TeamId, S0.Tl1.EmployeeId, "Deputy");

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        (await RoleCodeOfAsync(S0.Tl1.EmployeeId)).Should().Be(RoleCodes.TeamLeader);
        (await CountRoleInTeamAsync(S0.TSew01.TeamId, RoleCodes.TeamLeader)).Should().Be(1);
        (await CountRoleInTeamAsync(S0.TSew01.TeamId, RoleCodes.TeamDeputy)).Should().Be(1);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_148_Leader_Of_Another_Team_Cannot_Be_Appointed_Here()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await AppointAsync(token, S0.TSew02.TeamId, S0.Tl2.EmployeeId, "Leader");

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_149_Appoint_With_Invalid_Position_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        foreach (var position in new[] { "Manager", "leader", "" })
        {
            var r = await AppointAsync(token, S0.TSew02.TeamId, S0.W2.EmployeeId, position);
            AssertGraceful(r);
            r.Code.Should().Be(400, $"Position='{position}': {r}");
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_150_Appoint_Resigned_Employee_Discontinued_Team_Or_Unknown_Employee()
    {
        var token = await TokenAsync(S0.Mgr);
        var missing = await NonExistingEmployeeIdAsync();
        var before = await SnapshotAsync();

        var resigned = await AppointAsync(token, S0.TSew02.TeamId, S0.Rs.EmployeeId, "Leader");
        var intoOld = await AppointAsync(token, S0.TOld.TeamId, S0.W3.EmployeeId, "Leader");
        var unknown = await AppointAsync(token, S0.TSew02.TeamId, missing, "Leader");

        resigned.Code.Should().Be(400, resigned.ToString());
        intoOld.Code.Should().Be(400, intoOld.ToString());
        unknown.Code.Should().BeOneOf(404, 400);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 152–155 dismiss

    [Fact]
    public async Task IT_B1_152_Dismiss_Deputy_Returns_Him_To_Worker_Of_Same_Team()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await DismissAsync(token, S0.TSew01.TeamId, S0.Td1.EmployeeId);

        r.Code.Should().Be(200, r.ToString());
        var td1 = await EmployeeAsync(S0.Td1.EmployeeId);
        td1.Role.RoleCode.Should().Be(RoleCodes.Worker);
        td1.TeamId.Should().Be(S0.TSew01.TeamId);
        await AssertSingleEmployeeAuditAsync(lastAudit, S0.Td1.EmployeeId);
    }

    [Fact]
    public async Task IT_B1_153_Dismiss_Employee_Without_Position_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await DismissAsync(token, S0.TSew01.TeamId, S0.W1.EmployeeId);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before, "AuditLogs không thêm dòng");
    }

    [Fact]
    public async Task IT_B1_154_Dismiss_Leader_Of_Other_Team_Via_Wrong_Route_Returns_400()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await DismissAsync(token, S0.TSew01.TeamId, S0.Tl2.EmployeeId);

        AssertGraceful(r);
        r.Code.Should().Be(400, r.ToString());
        (await RoleCodeOfAsync(S0.Tl2.EmployeeId)).Should().Be(RoleCodes.TeamLeader);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_155_Dismiss_Unknown_Employee_Or_Empty_Body_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var missing = await NonExistingEmployeeIdAsync();
        var before = await SnapshotAsync();

        var unknown = await DismissAsync(token, S0.TSew01.TeamId, missing);
        var empty = await PostAsync(M + $"Teams/{S0.TSew01.TeamId}/dismiss", token, "{}");

        AssertGraceful(unknown);
        AssertGraceful(empty);
        unknown.Code.Should().BeOneOf(404, 400);
        empty.Code.Should().BeOneOf(404, 400);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 156–159 transfer / remove

    [Fact]
    public async Task IT_B1_156_Transfer_Leader_Out_Is_Rejected_Or_Demotes_Consistently()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await TransferAsync(token, S0.TSew02.TeamId, S0.Tl1.EmployeeId);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(200, 400, 409);
        Note($"IT_B1_156 transfer-in Leader => {r.Code}");
        if (r.Code == 200)
        {
            var tl1 = await EmployeeAsync(S0.Tl1.EmployeeId);
            tl1.Role.RoleCode.Should().Be(RoleCodes.Worker);
            tl1.TeamId.Should().Be(S0.TSew02.TeamId);
            (await CountRoleInTeamAsync(S0.TSew01.TeamId, RoleCodes.TeamLeader)).Should().Be(0);
        }
        else
        {
            await AssertDbUnchangedAsync(before);
        }

        await AssertLeadershipInvariantAsync();
    }

    [Fact]
    public async Task IT_B1_157_Transfer_Deputy_Out_Never_Carries_Position_To_New_Team()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await TransferAsync(token, S0.TCut01.TeamId, S0.Td1.EmployeeId);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(200, 400, 409);
        Note($"IT_B1_157 transfer-in Deputy => {r.Code}");
        if (r.Code == 200)
            (await RoleCodeOfAsync(S0.Td1.EmployeeId)).Should().Be(RoleCodes.Worker, "chức vụ chỉ được cấp qua appoint");
        else
            await AssertDbUnchangedAsync(before);
        await AssertLeadershipInvariantAsync();
        (await CountRoleInTeamAsync(S0.TCut01.TeamId, RoleCodes.TeamDeputy)).Should().Be(0);
    }

    [Fact]
    public async Task IT_B1_158_Transfer_Mixed_Batch_With_Leader_Rolls_Back_All()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await TransferAsync(token, S0.TSew02.TeamId, S0.W1.EmployeeId, S0.Tl1.EmployeeId);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        (await TeamOfAsync(S0.W1.EmployeeId)).Should().Be(S0.TSew01.TeamId);
        (await TeamOfAsync(S0.Tl1.EmployeeId)).Should().Be(S0.TSew01.TeamId);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_159_Remove_Leader_From_Team_Is_Rejected()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(M + $"Teams/{S0.TSew01.TeamId}/remove-members", token, new { EmployeeIds = new[] { S0.Tl1.EmployeeId } });

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        var tl1 = await EmployeeAsync(S0.Tl1.EmployeeId);
        tl1.TeamId.Should().Be(S0.TSew01.TeamId);
        tl1.Role.RoleCode.Should().Be(RoleCodes.TeamLeader);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 160–163 đường vòng

    [Fact]
    public async Task IT_B1_160_Admin_Update_Cannot_Move_Leader_To_Other_Team()
    {
        var token = await TokenAsync(S0.Adm);
        var body = await UpdateBodyAsync(S0.Tl1.EmployeeId, keepTeam: false, teamId: S0.TSew02.TeamId);
        var before = await SnapshotAsync();
        var rowVer = await RowVerAsync(S0.Tl1.EmployeeId);

        var r = await PutAsync(A + $"Employees/{S0.Tl1.EmployeeId}", token, body);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        (await RowVerAsync(S0.Tl1.EmployeeId)).Should().Equal(rowVer);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_161_Admin_Update_Cannot_Bypass_Appoint_To_Make_Second_Leader()
    {
        var token = await TokenAsync(S0.Adm);
        var body = await UpdateBodyAsync(S0.W1.EmployeeId, role: "TeamLeader");
        var before = await SnapshotAsync();

        var r = await PutAsync(A + $"Employees/{S0.W1.EmployeeId}", token, body);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(400, 409);
        (await RoleCodeOfAsync(S0.W1.EmployeeId)).Should().Be(RoleCodes.Worker);
        await AssertLeadershipInvariantAsync();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_162_Resign_Leader_Never_Leaves_Resigned_Leader_In_Team()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + $"Employees/{S0.Tl1.EmployeeId}/resign", token,
            new { ResignDate = Clock.Today, Reason = "Nghỉ việc", RowVer = await RowVerAsync(S0.Tl1.EmployeeId) });

        AssertGraceful(r);
        r.Code.Should().BeOneOf(200, 400, 409);
        Note($"IT_B1_162 resign Leader => {r.Code}");
        if (r.Code != 200) await AssertDbUnchangedAsync(before);

        await using var db = NewDb();
        var stuck = await db.Employees.AsNoTracking().CountAsync(e => e.Status == "RESIGNED" && e.TeamId != null
            && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy));
        stuck.Should().Be(0, "không nhân viên RESIGNED nào còn giữ chức của tổ");
    }

    [Fact]
    public async Task IT_B1_163_Discontinue_Team_With_Leader_And_Deputy_Is_Consistent()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await PostAsync(M + $"Teams/{S0.TSew01.TeamId}/discontinue", token);

        AssertGraceful(r);
        r.Code.Should().BeOneOf(200, 400, 409);
        Note($"IT_B1_163 discontinue tổ có chức vụ => {r.Code}");
        if (r.Code == 200)
        {
            await using var db = NewDb();
            var active = await db.Employees.AsNoTracking().CountAsync(e => e.TeamId == S0.TSew01.TeamId && e.Status == "ACTIVE"
                && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy));
            active.Should().Be(0, "[?] không còn Leader/Deputy ACTIVE trong tổ đã ngừng");
        }
        else
        {
            await AssertDbUnchangedAsync(before);
        }
    }

    [Fact]
    public async Task IT_B1_164_Dismiss_Then_Appoint_New_Leader_Sequentially()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var dismiss = await DismissAsync(token, S0.TSew01.TeamId, S0.Tl1.EmployeeId);
        var appoint = await AppointAsync(token, S0.TSew01.TeamId, S0.W1.EmployeeId, "Leader");

        dismiss.Code.Should().Be(200, dismiss.ToString());
        appoint.Code.Should().Be(200, appoint.ToString());
        (await RoleCodeOfAsync(S0.Tl1.EmployeeId)).Should().Be(RoleCodes.Worker);
        (await RoleCodeOfAsync(S0.W1.EmployeeId)).Should().Be(RoleCodes.TeamLeader);
        (await CountRoleInTeamAsync(S0.TSew01.TeamId, RoleCodes.TeamLeader)).Should().Be(1);
        var rows = (await AuditRowsAfterAsync(lastAudit)).Where(a => a.TableName == "Employee").ToList();
        rows.Should().HaveCount(2, "AuditLogs +2 (mỗi thao tác 1 dòng Employee)");
    }
}
