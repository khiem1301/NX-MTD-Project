using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_054–059 — RBAC theo prefix admin / manager / common.</summary>
public sealed class B1RbacTests(ITestOutputHelper output) : B1TestBase(output)
{
    [Fact]
    public async Task IT_B1_054_Admin_Manager_And_Common_Endpoints_Without_Token_Return_401()
    {
        var before = await SnapshotAsync();

        var admin = await GetAsync(A + "Employees");
        var manager = await GetAsync(M + "Employees");
        var profile = await GetAsync(C + "Profile");

        admin.Code.Should().Be(401, admin.ToString());
        manager.Code.Should().Be(401, manager.ToString());
        profile.Code.Should().Be(401, profile.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_055_Manager_Cannot_Call_Admin_Prefix_And_Nothing_Is_Created()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var list = await GetAsync(A + "Employees", token);
        var level = await PostAsync(A + "grade-levels", token,
            new { GradeCode = "B2", MonthlyAmount = 1000m, EffectiveDate = Clock.Today.AddDays(5) });
        var code = await PostAsync(A + "grade-codes", token, new { GradeCode = "B7" });

        list.Code.Should().Be(403, list.ToString());
        level.Code.Should().Be(403, level.ToString());
        code.Code.Should().Be(403, code.ToString());

        await using var db = NewDb();
        (await db.GradeLevels.AsNoTracking().AnyAsync(g => g.GradeCode == "B2" && g.EffectiveDate == Clock.Today.AddDays(5))).Should().BeFalse();
        (await db.GradeLevelCodes.AsNoTracking().AnyAsync(g => g.GradeCode == "B7")).Should().BeFalse();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_056_Admin_Can_Call_Manager_Prefix()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var employees = await GetAsync(M + "Employees", token);
        var teams = await GetAsync(M + "Teams", token);

        employees.Code.Should().Be(200, employees.ToString());
        teams.Code.Should().Be(200, teams.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_057_Non_Management_Roles_Cannot_Call_Manager_Prefix()
    {
        var before = await SnapshotAsync();

        foreach (var account in new[] { S0.Acc, S0.Wh, S0.Tl1, S0.W1 })
        {
            var r = await GetAsync(M + "Employees", await TokenAsync(account));
            r.Code.Should().Be(403, $"{account.Username}: {r}");
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_058_TeamLeader_Cannot_Appoint_Deputy()
    {
        var token = await TokenAsync(S0.Tl1);
        var before = await SnapshotAsync();

        var r = await PostAsync(M + $"Teams/{S0.TSew01.TeamId}/appoint", token,
            new { EmployeeId = S0.W1.EmployeeId, Position = "Deputy" });

        r.Code.Should().Be(403, r.ToString());
        var w1 = await EmployeeAsync(S0.W1.EmployeeId);
        w1.Role.RoleCode.Should().Be(RoleCodes.Worker);
        await AssertDbUnchangedAsync(before, "AuditLogs không được thêm dòng");
    }

    [Fact]
    public async Task IT_B1_059_Worker_Cannot_Reset_Own_Password()
    {
        var token = await TokenAsync(S0.W1);
        var before = await AccountAsync(S0.W1.AccountId);
        var snap = await SnapshotAsync();

        var r = await PostAsync(M + $"Employees/{S0.W1.EmployeeId}/account/reset-password", token);

        r.Code.Should().Be(403, r.ToString());
        var after = await AccountAsync(S0.W1.AccountId);
        after.PasswordHash.Should().Be(before.PasswordHash);
        after.IsTemporaryPassword.Should().Be(before.IsTemporaryPassword);
        await AssertDbUnchangedAsync(snap);
    }
}
