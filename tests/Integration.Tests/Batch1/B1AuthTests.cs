using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_001–023 — C/Auth (login, refresh, logout, me). Assert kép: HTTP/JSON + Test DB thật.</summary>
public sealed class B1AuthTests(ITestOutputHelper output) : B1TestBase(output)
{
    private static string Pw => Seed.S0Context.Password;

    private static string ExpectNoToken(Api r)
    {
        r.Body.Should().NotContain("accessToken", "không được cấp token khi từ chối");
        return r.Body;
    }

    // ---------------------------------------------------------------- 001–003

    [Fact]
    public async Task IT_B1_001_Login_Admin_Returns_Tokens_And_Db_Credentials_Unchanged()
    {
        var before = await AccountAsync(S0.Adm.AccountId);

        var r = await LoginAsync("admin01", Pw);

        r.Code.Should().Be(200, r.ToString());
        var access = B1Json.Str(r.Data, "accessToken");
        var refresh = B1Json.Str(r.Data, "refreshToken");
        access.Should().NotBeNullOrWhiteSpace();
        refresh.Should().NotBeNullOrWhiteSpace();
        var accessExp = B1Json.Prop(r.Data, "accessExpiresAt")!.Value.GetDateTime();
        var refreshExp = B1Json.Prop(r.Data, "refreshExpiresAt")!.Value.GetDateTime();
        accessExp.Should().BeBefore(refreshExp);
        var handler = new JwtSecurityTokenHandler();
        handler.ReadJwtToken(access).ValidTo.Should().BeAfter(DateTime.UtcNow);
        handler.ReadJwtToken(refresh).ValidTo.Should().BeAfter(DateTime.UtcNow);
        var user = B1Json.Prop(r.Data, "user")!.Value;
        B1Json.Str(user, "role").Should().Be("Admin");
        B1Json.Str(user, "roleName").Should().NotBeNullOrWhiteSpace();
        B1Json.Bool(user, "isTemporaryPassword").Should().BeFalse();

        var after = await AccountAsync(S0.Adm.AccountId);
        after.PasswordHash.Should().Be(before.PasswordHash);
        after.SecurityStamp.Should().Be(before.SecurityStamp);
        after.Status.Should().Be("ACTIVE");
        // [?] Login có cập nhật cột nào của Account hay không — ghi lại.
        Note($"IT_B1_001 Account LockedAt/LockReason/IsTemporaryPassword đổi: " +
             $"{after.LockedAt != before.LockedAt || after.LockReason != before.LockReason || after.IsTemporaryPassword != before.IsTemporaryPassword}");
    }

    [Fact]
    public async Task IT_B1_002_Login_Temporary_Password_Account_Flags_IsTemporaryPassword()
    {
        var r = await LoginAsync("tmp01", Pw);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Bool(B1Json.Prop(r.Data, "user")!.Value, "isTemporaryPassword").Should().BeTrue();
        (await AccountAsync(S0.Tmp.AccountId)).IsTemporaryPassword.Should().BeTrue();
    }

    [Fact]
    public async Task IT_B1_003_Login_TeamLeader_Returns_Team_Info_Matching_Db()
    {
        var r = await LoginAsync("tl01", Pw);

        r.Code.Should().Be(200, r.ToString());
        var user = B1Json.Prop(r.Data, "user")!.Value;
        B1Json.Int(user, "teamId").Should().Be(S0.TSew01.TeamId);
        B1Json.Str(user, "teamCode").Should().Be("T_SEW01");
        B1Json.Str(user, "teamType").Should().Be("Sewing");
        B1Json.Str(user, "role").Should().Be("TeamLeader");

        await using var db = NewDb();
        var row = await db.Employees.Where(e => e.EmployeeId == S0.Tl1.EmployeeId)
            .Select(e => new { e.TeamId, e.Team!.TeamCode, RoleCode = e.Role.RoleCode }).SingleAsync();
        row.TeamId.Should().Be(S0.TSew01.TeamId);
        row.TeamCode.Should().Be("T_SEW01");
        row.RoleCode.Should().Be("TEAM_LEADER");
    }

    // ---------------------------------------------------------------- 004–011

    [Fact]
    public async Task IT_B1_004_Login_Wrong_Password_Returns_401_Without_Token()
    {
        var before = await SnapshotAsync();

        var r = await LoginAsync("admin01", "SaiMatKhau");

        r.Code.Should().Be(401, r.ToString());
        ExpectNoToken(r);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_005_Login_Unknown_Username_Has_Same_Message_As_Wrong_Password()
    {
        var before = await SnapshotAsync();

        var wrongPassword = await LoginAsync("admin01", "SaiMatKhau");
        var unknownUser = await LoginAsync("khongtontai", Pw);

        unknownUser.Code.Should().Be(401, unknownUser.ToString());
        wrongPassword.Code.Should().Be(401);
        unknownUser.Message.Should().NotBeNullOrWhiteSpace();
        unknownUser.Message.Should().Be(wrongPassword.Message, "chống dò username");
        unknownUser.ErrorCode.Should().Be(wrongPassword.ErrorCode);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_006_Login_Empty_Credentials_Or_Empty_Body_Is_Rejected_Without_500()
    {
        var before = await SnapshotAsync();

        var emptyStrings = await LoginAsync("", "");
        var emptyBody = await PostAsync(C + "Auth/login", null, "{}");

        emptyStrings.Code.Should().BeOneOf(400, 401);
        emptyBody.Code.Should().BeOneOf(400, 401);
        ExpectNoToken(emptyStrings);
        ExpectNoToken(emptyBody);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_007_Login_Locked_Account_Is_Rejected_And_Stays_Locked()
    {
        var before = await AccountAsync(S0.Lck.AccountId);
        var snap = await SnapshotAsync();

        var r = await LoginAsync("lck01", Pw);

        r.Code.Should().BeOneOf(401, 403);
        ExpectNoToken(r);
        var after = await AccountAsync(S0.Lck.AccountId);
        after.Status.Should().Be("LOCKED");
        after.LockedAt.Should().Be(before.LockedAt);
        await AssertDbUnchangedAsync(snap);
    }

    [Fact]
    public async Task IT_B1_008_Login_Resigned_Employee_Is_Rejected_Db_Unchanged()
    {
        var before = await SnapshotAsync();

        var r = await LoginAsync("rs01", Pw);

        r.Code.Should().BeOneOf(401, 403);
        ExpectNoToken(r);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_009_Login_Sql_Injection_Returns_401_Db_Unchanged()
    {
        var before = await SnapshotAsync();

        var r = await LoginAsync("admin01' OR '1'='1' --", "x");

        r.Code.Should().Be(401, r.ToString());
        ExpectNoToken(r);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_010_Login_Username_Length_50_And_51_Rejected_Without_500()
    {
        var before = await SnapshotAsync();

        var r50 = await LoginAsync(new string('a', 50), Pw);
        var r51 = await LoginAsync(new string('a', 51), Pw);

        r50.Code.Should().Be(401, r50.ToString());
        r51.Code.Should().BeOneOf(400, 401);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_011_Login_Username_Different_Case_Does_Not_Fail_With_500()
    {
        var before = await SnapshotAsync();

        var r = await LoginAsync("ADMIN01", Pw);

        r.Code.Should().BeOneOf(200, 401);
        Note($"IT_B1_011 login 'ADMIN01' => {r.Code} (collation Vietnamese_100_CI_AS)");
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 012–017 refresh

    [Fact]
    public async Task IT_B1_012_Refresh_With_Valid_RefreshToken_Issues_Working_AccessToken()
    {
        var login = await LoginAsync("admin01", Pw);
        var refreshToken = B1Json.Str(login.Data, "refreshToken");
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = refreshToken });

        r.Code.Should().Be(200, r.ToString());
        var newAccess = B1Json.Str(r.Data, "accessToken");
        newAccess.Should().NotBeNullOrWhiteSpace();
        var me = await GetAsync(C + "Auth/me", newAccess);
        me.Code.Should().Be(200, me.ToString());
        B1Json.Int(me.Data, "accountId").Should().Be(S0.Adm.AccountId);
        await AssertDbUnchangedAsync(before, "không có bảng RefreshToken nên không có INSERT");
    }

    [Fact]
    public async Task IT_B1_013_Refresh_With_Garbage_Token_Returns_401()
    {
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = "abc.def.ghi" });

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_014_Refresh_Using_AccessToken_Returns_401()
    {
        var login = await LoginAsync("admin01", Pw);
        var accessToken = B1Json.Str(login.Data, "accessToken");
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = accessToken });

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_015_Refresh_With_Old_Token_After_Password_Change_Returns_401()
    {
        var login = await LoginAsync("wk01", Pw);
        var oldRefresh = B1Json.Str(login.Data, "refreshToken");
        var accessToken = B1Json.Str(login.Data, "accessToken");
        var change = await PostAsync(C + "Profile/change-password", accessToken,
            new { CurrentPassword = Pw, NewPassword = "N3w@Pass!", ConfirmPassword = "N3w@Pass!" });
        change.Code.Should().Be(200, change.ToString());
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = oldRefresh });

        r.Code.Should().Be(401, r.ToString());
        Note($"IT_B1_015 refresh token cũ sau đổi mật khẩu => {r.Code}");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_016_Refresh_After_Admin_Locks_Account_Is_Rejected_And_Account_Stays_Locked()
    {
        var whLogin = await LoginAsync("wh01", Pw);
        var refreshToken = B1Json.Str(whLogin.Data, "refreshToken");
        var lockRes = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/account/lock", await TokenAsync(S0.Adm), new { Reason = "IT_B1_016 khóa tài khoản" });
        lockRes.Code.Should().Be(200, lockRes.ToString());
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = refreshToken });

        r.Code.Should().BeOneOf(401, 403);
        (await AccountAsync(S0.Wh.AccountId)).Status.Should().Be("LOCKED");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_017_Refresh_Expiry_Boundary_One_Second_Before_And_After_RefreshExpiresAt()
    {
        var before = await SnapshotAsync();
        var now = DateTime.UtcNow;
        var expired = await CraftJwtAsync(S0.Adm, "refresh", now.AddHours(-1), now.AddSeconds(-1));
        var alive = await CraftJwtAsync(S0.Adm, "refresh", now.AddHours(-1), now.AddSeconds(1));

        var rExpired = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = expired });
        var rAlive = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = alive });

        rExpired.Code.Should().Be(401, "quá hạn 1 giây phải bị từ chối. " + rExpired);
        rAlive.Code.Should().Be(200, "còn 1 giây trước hạn phải được chấp nhận. " + rAlive);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 018–023

    [Fact]
    public async Task IT_B1_018_Logout_Returns_LoggedOut_And_Db_Unchanged()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/logout", token);

        r.Code.Should().Be(200, r.ToString());
        // Spec ghi `LoggedOut` = tên hằng số; chuỗi thực tế là Messages.LoggedOut.
        r.Message.Should().Be(NX_MTD.Application.Common.Messages.LoggedOut);
        await AssertDbUnchangedAsync(before, "phiên không lưu ở BE (D3)");
    }

    [Fact]
    public async Task IT_B1_019_Logout_Without_Token_Returns_401()
    {
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Auth/logout");

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_020_Me_Returns_Current_User_Matching_Account_Employee_Team()
    {
        var token = await TokenAsync(S0.W1);

        var r = await GetAsync(C + "Auth/me", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "accountId").Should().Be(S0.W1.AccountId);
        B1Json.Int(r.Data, "employeeId").Should().Be(S0.W1.EmployeeId);
        B1Json.Str(r.Data, "role").Should().Be("Worker");
        B1Json.Str(r.Data, "teamCode").Should().Be("T_SEW01");

        await using var db = NewDb();
        var row = await db.Accounts.Where(a => a.AccountId == S0.W1.AccountId)
            .Select(a => new { a.Username, a.Employee.FullName, a.EmployeeId, a.Employee.Team!.TeamCode }).SingleAsync();
        B1Json.Str(r.Data, "username").Should().Be(row.Username);
        B1Json.Str(r.Data, "fullName").Should().Be(row.FullName);
        B1Json.Int(r.Data, "employeeId").Should().Be(row.EmployeeId);
        B1Json.Str(r.Data, "teamCode").Should().Be(row.TeamCode);
    }

    [Fact]
    public async Task IT_B1_021_Me_With_Expired_Token_Returns_401()
    {
        var now = DateTime.UtcNow;
        var expired = await CraftJwtAsync(S0.W1, "access", now.AddHours(-3), now.AddHours(-2));
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Auth/me", expired);

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_022_Forged_Role_Claim_Is_Rejected_With_401_And_No_Data()
    {
        var valid = await TokenAsync(S0.W1);
        var forged = ForgeRole(valid, "Admin");
        var before = await SnapshotAsync();

        var r = await GetAsync(A + "Employees", forged);

        r.Code.Should().Be(401, r.ToString());
        r.Body.Should().NotContain(S0.Adm.Employee.EmployeeCode);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_023_Me_Ignores_AccountId_Query_And_Returns_Callers_Own_Data()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + $"Auth/me?accountId={S0.Adm.AccountId}", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "accountId").Should().Be(S0.W1.AccountId);
        B1Json.Str(r.Data, "username").Should().Be("wk01");
        r.Body.Should().NotContain("admin01");
        await AssertDbUnchangedAsync(before);
    }
}
