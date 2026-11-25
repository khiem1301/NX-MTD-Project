using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_106–117 — khóa / mở khóa / đặt lại mật khẩu tài khoản ({id} = EmployeeId).</summary>
public sealed class B1AccountTests(ITestOutputHelper output) : B1TestBase(output)
{
    private static string Pw => Seed.S0Context.Password;

    private string LockUrl(int employeeId) => A + $"Employees/{employeeId}/account/lock";

    // ---------------------------------------------------------------- 106–110 lock

    [Fact]
    public async Task IT_B1_106_Admin_Locks_Account_With_Reason()
    {
        var token = await TokenAsync(S0.Adm);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(LockUrl(S0.Wh.EmployeeId), token, new { Reason = "Vi phạm nội quy" });

        r.Code.Should().Be(200, r.ToString());
        var account = await AccountAsync(S0.Wh.AccountId);
        account.Status.Should().Be("LOCKED");
        account.LockReason.Should().Be("Vi phạm nội quy");
        account.LockedAt.Should().NotBeNull();
        var lockedAt = account.LockedAt!.Value;
        var nearest = Math.Min(Math.Abs((lockedAt - Clock.Now).TotalMinutes), Math.Abs((lockedAt - VnNow()).TotalMinutes));
        nearest.Should().BeLessThan(5, "LockedAt ≈ giờ hiện tại theo giờ VN");
        (await EmployeeAsync(S0.Wh.EmployeeId)).Status.Should().Be("ACTIVE");
        await AssertSingleAuditAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B1_107_Locked_Account_Cannot_Login_Refresh_And_Me_Behaviour_Recorded()
    {
        var login = await LoginAsync("wh01", Pw);
        var oldAccess = B1Json.Str(login.Data, "accessToken");
        var oldRefresh = B1Json.Str(login.Data, "refreshToken");
        var lock1 = await PostAsync(LockUrl(S0.Wh.EmployeeId), await TokenAsync(S0.Adm), new { Reason = "Khóa để kiểm thử" });
        lock1.Code.Should().Be(200, lock1.ToString());
        var after = await SnapshotAsync();

        var newLogin = await LoginAsync("wh01", Pw);
        var refresh = await PostAsync(C + "Auth/refresh", null, new { RefreshToken = oldRefresh });
        var me = await GetAsync(C + "Auth/me", oldAccess);

        newLogin.Code.Should().BeOneOf(401, 403);
        refresh.Code.Should().Be(401, refresh.ToString());
        me.Code.Should().BeOneOf(200, 401);
        Note($"IT_B1_107 me bằng access token cũ sau khi khóa => {me.Code}");
        await AssertDbUnchangedAsync(after);
    }

    [Fact]
    public async Task IT_B1_108_Lock_With_Empty_Reason_Or_Empty_Body_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var empty = await PostAsync(LockUrl(S0.Wh.EmployeeId), token, new { Reason = "" });
        var noBody = await PostAsync(LockUrl(S0.Wh.EmployeeId), token, "{}");

        empty.Code.Should().Be(400, empty.ToString());
        noBody.Code.Should().Be(400, noBody.ToString());
        (await AccountAsync(S0.Wh.AccountId)).Status.Should().Be("ACTIVE");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_109_Lock_Reason_Length_300_Stored_In_Full_301_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var reason300 = LongVietnamese(300);
        var reason301 = LongVietnamese(301);
        var before = await SnapshotAsync();

        var tooLong = await PostAsync(LockUrl(S0.Wh.EmployeeId), token, new { Reason = reason301 });

        tooLong.Code.Should().Be(400, tooLong.ToString());
        await AssertDbUnchangedAsync(before);

        var ok = await PostAsync(LockUrl(S0.Wh.EmployeeId), token, new { Reason = reason300 });

        ok.Code.Should().Be(200, ok.ToString());
        var stored = (await AccountAsync(S0.Wh.AccountId)).LockReason;
        stored.Should().Be(reason300);
        stored!.Length.Should().Be(300);
    }

    [Fact]
    public async Task IT_B1_110_Lock_Already_Locked_Account_Keeps_Original_Lock_Data()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();
        var original = await AccountAsync(S0.Lck.AccountId);

        var r = await PostAsync(LockUrl(S0.Lck.EmployeeId), token, new { Reason = "Khóa lần hai" });

        r.Code.Should().BeOneOf(400, 409);
        var after = await AccountAsync(S0.Lck.AccountId);
        after.LockedAt.Should().Be(original.LockedAt);
        after.LockReason.Should().Be(original.LockReason);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 111–113 id semantics + IDOR

    [Fact]
    public async Task IT_B1_111_Lock_Uses_EmployeeId_Not_AccountId_When_Ids_Collide()
    {
        // Dựng va chạm id: AccountId của "decoy" (tài khoản kiểu wh01) trùng EmployeeId của nhân viên EX khác.
        var k = Math.Max(await Db.Employees.MaxAsync(e => e.EmployeeId), await Db.Accounts.MaxAsync(a => a.AccountId)) + 10_000;
        var workerRole = Ctx.Roles[RoleCodes.Worker].RoleId;

        await Db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Employee ON");
        var ex = new Employee
        {
            EmployeeId = k, EmployeeCode = "S0-EX", FullName = "Nhan Vien Khac", RoleId = Ctx.Roles[RoleCodes.Electrician].RoleId,
            PayType = PayTypes.Monthly, StartDate = Clock.Today.AddYears(-1), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        };
        Db.Employees.Add(ex);
        await Db.SaveChangesAsync();
        await Db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Employee OFF");
        var exAccount = new Account
        {
            EmployeeId = ex.EmployeeId, Username = "exacc01", PasswordHash = S0.Adm.PasswordHash, IsTemporaryPassword = false,
            SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        };
        Db.Accounts.Add(exAccount);
        var decoyEmployee = new Employee
        {
            EmployeeCode = "S0-DECOY", FullName = "Thu Kho Mo Phong", RoleId = Ctx.Roles[RoleCodes.WarehouseKeeper].RoleId,
            PayType = PayTypes.Monthly, StartDate = Clock.Today.AddYears(-1), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        };
        Db.Employees.Add(decoyEmployee);
        await Db.SaveChangesAsync();
        await Db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Account ON");
        var decoy = new Account
        {
            AccountId = k, EmployeeId = decoyEmployee.EmployeeId, Username = "decoywh", PasswordHash = S0.Adm.PasswordHash,
            IsTemporaryPassword = false, SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        };
        Db.Accounts.Add(decoy);
        await Db.SaveChangesAsync();
        await Db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Account OFF");
        decoy.AccountId.Should().Be(ex.EmployeeId, "điều kiện tiên quyết: AccountId của decoy == EmployeeId của EX");
        var token = await TokenAsync(S0.Adm);

        var r = await PostAsync(LockUrl(k), token, new { Reason = "Kiểm tra id" });

        r.Code.Should().Be(200, r.ToString());
        (await AccountAsync(exAccount.AccountId)).Status.Should().Be("LOCKED");
        (await AccountAsync(decoy.AccountId)).Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B1_112_Manager_Cannot_Lock_Admin_Or_Reset_Accountant_Password()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var lockAdmin = await PostAsync(M + $"Employees/{S0.Adm2.EmployeeId}/account/lock", token, new { Reason = "Không được phép" });
        var resetAcc = await PostAsync(M + $"Employees/{S0.Acc.EmployeeId}/account/reset-password", token);

        lockAdmin.Code.Should().BeOneOf(403, 404);
        resetAcc.Code.Should().BeOneOf(403, 404);
        resetAcc.Body.Should().NotContain("temporaryPassword");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_113_Admin_Cannot_Lock_Own_Account()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(LockUrl(S0.Adm.EmployeeId), token, new { Reason = "Tự khóa" });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(400, r.ToString());
        Note($"IT_B1_113 Account.Status thực tế = {(await AccountAsync(S0.Adm.AccountId)).Status}");
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 114–117 unlock / reset

    [Fact]
    public async Task IT_B1_114_Admin_Unlocks_Locked_Account_And_Login_Works()
    {
        var token = await TokenAsync(S0.Adm);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(A + $"Employees/{S0.Lck.EmployeeId}/account/unlock", token);

        r.Code.Should().Be(200, r.ToString());
        var account = await AccountAsync(S0.Lck.AccountId);
        account.Status.Should().Be("ACTIVE");
        account.LockedAt.Should().BeNull();
        account.LockReason.Should().BeNull();
        await AssertSingleAuditAsync(lastAudit);
        (await LoginAsync("lck01", Pw)).Code.Should().Be(200);
    }

    [Fact]
    public async Task IT_B1_115_Unlock_Active_Account_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/account/unlock", token);

        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_116_Manager_Resets_Password_Returns_Temporary_Password_Once()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await AccountAsync(S0.W1.AccountId);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(M + $"Employees/{S0.W1.EmployeeId}/account/reset-password", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "username").Should().Be("wk01");
        var temp = B1Json.Str(r.Data, "temporaryPassword");
        temp.Should().NotBeNullOrWhiteSpace();
        r.CacheControl.Should().Contain("no-store");

        var after = await AccountAsync(S0.W1.AccountId);
        after.PasswordHash.Should().NotBe(before.PasswordHash);
        after.PasswordHash.Should().NotBe(temp);
        after.IsTemporaryPassword.Should().BeTrue();
        after.SecurityStamp.Should().NotBe(before.SecurityStamp);
        await AssertSingleAuditAsync(lastAudit);

        (await LoginAsync("wk01", Pw)).Code.Should().Be(401);
        var tempLogin = await LoginAsync("wk01", temp!);
        tempLogin.Code.Should().Be(200, tempLogin.ToString());
        B1Json.Bool(B1Json.Prop(tempLogin.Data, "user")!.Value, "isTemporaryPassword").Should().BeTrue();
    }

    [Fact]
    public async Task IT_B1_117_Reset_Password_For_Resigned_Or_Unknown_Employee_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var missing = await NonExistingEmployeeIdAsync();
        var before = await SnapshotAsync();

        var resigned = await PostAsync(A + $"Employees/{S0.Rs.EmployeeId}/account/reset-password", token);
        var unknown = await PostAsync(A + $"Employees/{missing}/account/reset-password", token);

        resigned.Code.Should().Be(400, resigned.ToString());
        unknown.Code.Should().Be(404, unknown.ToString());
        await AssertDbUnchangedAsync(before);
    }
}
