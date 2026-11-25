using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_092–105 — sửa nhân viên (khóa lạc quan RowVer, IDOR) và nghỉ việc.</summary>
public sealed class B1EmployeeUpdateResignTests(ITestOutputHelper output) : B1TestBase(output)
{
    private async Task SetWhStartDateAsync(DateOnly date)
    {
        S0.Wh.Employee.StartDate = date;
        await Db.SaveChangesAsync();
    }

    private async Task<object> ResignBodyAsync(int employeeId, DateOnly date, string reason = "Nghỉ theo nguyện vọng", byte[]? rowVer = null) =>
        new { ResignDate = date, Reason = reason, RowVer = rowVer ?? await RowVerAsync(employeeId) };

    // ---------------------------------------------------------------- 092–098 update

    [Fact]
    public async Task IT_B1_092_Admin_Updates_Employee_With_Valid_RowVer()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await EmployeeAsync(S0.Wh.EmployeeId);
        var lastAudit = await LastAuditIdAsync();
        var body = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới", phone: "0987654321");

        var r = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, body);

        r.Code.Should().Be(200, r.ToString());
        var after = await EmployeeAsync(S0.Wh.EmployeeId);
        after.FullName.Should().Be("Kho Mới");
        after.Phone.Should().Be("0987654321");
        after.RowVer.Should().NotEqual(before.RowVer);
        after.UpdatedAt.Should().NotBeNull();
        await AssertSingleAuditAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B1_093_Update_With_Stale_RowVer_Returns_409_And_Changes_Nothing()
    {
        var token = await TokenAsync(S0.Adm);
        var staleBody = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới", phone: "0987654321");
        var first = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, staleBody);
        first.Code.Should().Be(200, first.ToString());
        var afterFirst = await SnapshotAsync();
        var staleAgain = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Cũ", rowVer: (byte[])staleBody["RowVer"]!);

        var r = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, staleAgain);

        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(afterFirst, "không đổi và AuditLogs không thêm dòng");
    }

    [Fact]
    public async Task IT_B1_094_Update_Missing_Or_Malformed_RowVer_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();
        var noRowVer = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới");
        noRowVer.Remove("RowVer");
        var badRowVer = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới");
        badRowVer["RowVer"] = "không-phải-base64";

        var r1 = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, noRowVer);
        var r2 = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, badRowVer);

        r1.Code.Should().Be(400, r1.ToString());
        r2.Code.Should().Be(400, r2.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // IT_B1_095 (đồng thời) nằm ở B1ConcurrencyTests: cần connection DB độc lập, không dùng ambient transaction.

    [Fact]
    public async Task IT_B1_096_Manager_Cannot_Update_Accountant()
    {
        var token = await TokenAsync(S0.Mgr);
        var body = await UpdateBodyAsync(S0.Acc.EmployeeId, fullName: "Ke Toan Hack");
        var before = await SnapshotAsync();

        var r = await PutAsync(M + $"Employees/{S0.Acc.EmployeeId}", token, body);

        r.Code.Should().BeOneOf(403, 404);
        r.Body.Should().NotContain(S0.Acc.Employee.FullName);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_097_Manager_Cannot_Assign_Admin_Accountant_Or_Manager_Role()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        foreach (var role in new[] { "Admin", "Accountant", "Manager" })
        {
            var body = await UpdateBodyAsync(S0.Wh.EmployeeId, role: role);
            var r = await PutAsync(M + $"Employees/{S0.Wh.EmployeeId}", token, body);
            r.Code.Should().BeOneOf(400, 403);
            (await EmployeeAsync(S0.Wh.EmployeeId)).Role.RoleCode.Should().Be(RoleCodes.WarehouseKeeper, role);
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_098_Update_Phone_Of_16_Characters_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var body = await UpdateBodyAsync(S0.Wh.EmployeeId, phone: "0123456789012345");
        var before = await SnapshotAsync();

        var r = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, body);

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 099–105 resign

    [Fact]
    public async Task IT_B1_099_Admin_Resigns_Employee_Status_Uppercase_And_Account_Deactivated()
    {
        await SetWhStartDateAsync(new DateOnly(2024, 1, 1));
        var token = await TokenAsync(S0.Adm);
        var before = await EmployeeAsync(S0.Wh.EmployeeId);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/resign", token, await ResignBodyAsync(S0.Wh.EmployeeId, Clock.Today));

        r.Code.Should().Be(200, r.ToString());
        var after = await EmployeeAsync(S0.Wh.EmployeeId);
        after.Status.Should().Be("RESIGNED");
        after.ResignDate.Should().Be(Clock.Today);
        after.RowVer.Should().NotEqual(before.RowVer);
        var account = await AccountAsync(S0.Wh.AccountId);
        // [?] Spec cho phép ghi lại hành vi thực tế của Account.Status; ràng buộc nhất quán: người đã nghỉ không đăng nhập được.
        Note($"IT_B1_099 Account.Status sau nghỉ việc = {account.Status}");
        var loginAfterResign = await LoginAsync("wh01", Seed.S0Context.Password);
        loginAfterResign.Code.Should().BeOneOf(401, 403);
        await AssertSingleAuditAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B1_100_Resigned_Employee_Cannot_Login_And_Appears_In_Resigned_List()
    {
        await SetWhStartDateAsync(new DateOnly(2024, 1, 1));
        var token = await TokenAsync(S0.Adm);
        var resign = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/resign", token, await ResignBodyAsync(S0.Wh.EmployeeId, Clock.Today));
        resign.Code.Should().Be(200, resign.ToString());

        var login = await LoginAsync("wh01", Seed.S0Context.Password);
        var list = await GetAsync(A + "Employees?Status=Resigned&pageSize=100", token);

        login.Code.Should().BeOneOf(401, 403);
        login.Body.Should().NotContain("accessToken");
        list.Code.Should().Be(200, list.ToString());
        IdSet(list, "employeeId").Should().Contain(S0.Wh.EmployeeId);
        (await EmployeeAsync(S0.Wh.EmployeeId)).Status.Should().Be("RESIGNED");
    }

    [Fact]
    public async Task IT_B1_101_Resign_Already_Resigned_Employee_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();
        var resignDateBefore = (await EmployeeAsync(S0.Rs.EmployeeId)).ResignDate;

        var r = await PostAsync(A + $"Employees/{S0.Rs.EmployeeId}/resign", token, await ResignBodyAsync(S0.Rs.EmployeeId, Clock.Today));

        r.Code.Should().BeOneOf(400, 409);
        (await EmployeeAsync(S0.Rs.EmployeeId)).ResignDate.Should().Be(resignDateBefore);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_102_Resign_Date_Boundary_Around_StartDate()
    {
        await SetWhStartDateAsync(new DateOnly(2024, 1, 1));
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var early = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/resign", token,
            await ResignBodyAsync(S0.Wh.EmployeeId, new DateOnly(2023, 12, 31)));

        early.Code.Should().Be(400, early.ToString());
        await AssertDbUnchangedAsync(before);

        var same = await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/resign", token,
            await ResignBodyAsync(S0.Wh.EmployeeId, new DateOnly(2024, 1, 1)));

        same.Code.Should().Be(200, same.ToString());
        (await EmployeeAsync(S0.Wh.EmployeeId)).ResignDate.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public async Task IT_B1_103_Resign_Empty_Reason_Missing_Date_And_Stale_RowVer()
    {
        var token = await TokenAsync(S0.Adm);
        var url = A + $"Employees/{S0.Wh.EmployeeId}/resign";
        var before = await SnapshotAsync();
        var rowVerB64 = Convert.ToBase64String(await RowVerAsync(S0.Wh.EmployeeId));

        var emptyReason = await PostAsync(url, token, await ResignBodyAsync(S0.Wh.EmployeeId, Clock.Today, reason: ""));
        var missingDate = await PostAsync(url, token, $$"""{"Reason":"Nghỉ việc","RowVer":"{{rowVerB64}}"}""");

        emptyReason.Code.Should().Be(400, emptyReason.ToString());
        missingDate.Code.Should().Be(400, missingDate.ToString());
        await AssertDbUnchangedAsync(before);

        var staleRowVer = await RowVerAsync(S0.Wh.EmployeeId);
        var edit = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới"));
        edit.Code.Should().Be(200, edit.ToString());
        var afterEdit = await SnapshotAsync();

        var stale = await PostAsync(url, token, await ResignBodyAsync(S0.Wh.EmployeeId, Clock.Today, rowVer: staleRowVer));

        stale.Code.Should().Be(409, stale.ToString());
        await AssertDbUnchangedAsync(afterEdit);
    }

    [Fact]
    public async Task IT_B1_104_Manager_Cannot_Resign_Admin()
    {
        var token = await TokenAsync(S0.Mgr);
        var body = await ResignBodyAsync(S0.Adm2.EmployeeId, Clock.Today);
        var before = await SnapshotAsync();

        var r = await PostAsync(M + $"Employees/{S0.Adm2.EmployeeId}/resign", token, body);

        r.Code.Should().BeOneOf(403, 404);
        (await EmployeeAsync(S0.Adm2.EmployeeId)).Status.Should().Be("ACTIVE");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_105_Admin_Cannot_Resign_Self()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + $"Employees/{S0.Adm.EmployeeId}/resign", token, await ResignBodyAsync(S0.Adm.EmployeeId, Clock.Today));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(400, r.ToString());
        var after = await EmployeeAsync(S0.Adm.EmployeeId);
        Note($"IT_B1_105 trạng thái thực tế: Employee.Status={after.Status}, Account.Status={(await AccountAsync(S0.Adm.AccountId)).Status}");
        await AssertDbUnchangedAsync(before);
    }
}
