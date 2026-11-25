using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_060–070 — POST Employees (tạo nhân viên văn phòng): quyền theo vai trò, UNIQUE, biên độ dài.</summary>
public sealed class B1EmployeeCreateTests(ITestOutputHelper output) : B1TestBase(output)
{
    private const string ThaiName = "Phạm Hồng Thái";

    private object Body(string code, string fullName, string role = "WarehouseKeeper", string? gender = "M", int? teamId = null) => new
    {
        EmployeeCode = code,
        FullName = fullName,
        Gender = gender,
        DateOfBirth = new DateOnly(1990, 1, 1),
        Phone = "0912345678",
        Role = role,
        TeamId = teamId,
        PayType = "Monthly",
        StartDate = Clock.Today,
        AttendanceCode = (int?)null,
    };

    private async Task<int> CountEmployeesAsync(string code)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.EmployeeCode == code);
    }

    private async Task<int> CountAccountsOfCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.Accounts.AsNoTracking().CountAsync(a => a.Employee.EmployeeCode == code);
    }

    private async Task AssertCreatedAsync(Api r, string code, string fullName, string roleCode)
    {
        r.Code.Should().BeOneOf(200, 201);
        var employeeId = B1Json.Int(r.Data, "employeeId");
        employeeId.Should().NotBeNull();
        B1Json.Str(r.Data, "username").Should().NotBeNullOrWhiteSpace();
        var tempPassword = B1Json.Str(r.Data, "temporaryPassword");
        tempPassword.Should().NotBeNullOrWhiteSpace();

        await using var db = NewDb();
        var emp = await db.Employees.AsNoTracking().Include(e => e.Role).SingleAsync(e => e.EmployeeCode == code);
        emp.EmployeeId.Should().Be(employeeId!.Value);
        emp.Status.Should().Be("ACTIVE");
        emp.Role.RoleCode.Should().Be(roleCode);
        emp.FullName.Should().Be(fullName);
        var acc = await db.Accounts.AsNoTracking().SingleAsync(a => a.EmployeeId == emp.EmployeeId);
        acc.Username.Should().Be(B1Json.Str(r.Data, "username"));
        acc.IsTemporaryPassword.Should().BeTrue();
        acc.Status.Should().Be("ACTIVE");
        acc.PasswordHash.Should().NotBe(tempPassword);
        acc.SecurityStamp.Should().NotBe(Guid.Empty);
    }

    // ---------------------------------------------------------------- 060–061

    [Fact]
    public async Task IT_B1_060_Admin_Creates_WarehouseKeeper_With_Generated_Username_And_NoStore()
    {
        var token = await TokenAsync(S0.Adm);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV900", ThaiName));

        await AssertCreatedAsync(r, "NV900", ThaiName, RoleCodes.WarehouseKeeper);
        B1Json.Str(r.Data, "username").Should().Be("ThaiPH");
        r.CacheControl.Should().Contain("no-store");
        var audits = await AuditRowsAfterAsync(lastAudit);
        audits.Should().HaveCount(1, "AuditLogs +1");
        Note($"IT_B1_060 AuditLog: Table={audits[0].TableName}, Action={audits[0].Action}, RecordId={audits[0].RecordId}");
    }

    [Fact]
    public async Task IT_B1_061_Manager_Creates_QaQc_Employee_With_Account()
    {
        var token = await TokenAsync(S0.Mgr);

        var r = await PostAsync(M + "Employees", token, Body("NV901", ThaiName, role: "QaQc"));

        // Spec: "201"; cùng endpoint IT_B1_060 chấp nhận 201/200 (ToResponse trả 200), AssertCreatedAsync kiểm 200/201.
        await AssertCreatedAsync(r, "NV901", ThaiName, RoleCodes.QaQc);
    }

    // ---------------------------------------------------------------- 062–065

    [Fact]
    public async Task IT_B1_062_Create_Worker_Role_Returns_400_Nothing_Created()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV902", ThaiName, role: "Worker"));

        r.Code.Should().Be(400, r.ToString());
        (await CountEmployeesAsync("NV902")).Should().Be(0);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_063_Create_TeamLeader_Or_TeamDeputy_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var leader = await PostAsync(A + "Employees", token, Body("NV903", ThaiName, role: "TeamLeader"));
        var deputy = await PostAsync(A + "Employees", token, Body("NV904", ThaiName, role: "TeamDeputy"));

        leader.Code.Should().Be(400, leader.ToString());
        deputy.Code.Should().Be(400, deputy.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_064_Create_With_Unknown_Or_DbStyle_Role_Returns_400_Not_500()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var unknown = await PostAsync(A + "Employees", token, Body("NV905", ThaiName, role: "Superuser"));
        var dbStyle = await PostAsync(A + "Employees", token, Body("NV906", ThaiName, role: "WAREHOUSE_KEEPER"));

        unknown.Code.Should().Be(400, unknown.ToString());
        dbStyle.Code.Should().Be(400, dbStyle.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_065_Manager_Cannot_Create_Admin_Accountant_Or_Manager()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        foreach (var (role, code) in new[] { ("Admin", "NV907"), ("Accountant", "NV908"), ("Manager", "NV909") })
        {
            var r = await PostAsync(M + "Employees", token, Body(code, ThaiName, role: role));
            r.Code.Should().BeOneOf(400, 403);
            (await CountEmployeesAsync(code)).Should().Be(0, role);
        }

        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 066–067 UNIQUE

    [Fact]
    public async Task IT_B1_066_Duplicate_EmployeeCode_Returns_409_No_Orphan_Account()
    {
        var token = await TokenAsync(S0.Adm);
        var first = await PostAsync(A + "Employees", token, Body("NV900", ThaiName));
        first.Code.Should().BeOneOf(200, 201);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV900", "Lê Văn Cường"));

        r.Code.Should().Be(409, r.ToString());
        (await CountEmployeesAsync("NV900")).Should().Be(1);
        (await CountAccountsOfCodeAsync("NV900")).Should().Be(1);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_067_Duplicate_EmployeeCode_Differing_Only_By_Case_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var first = await PostAsync(A + "Employees", token, Body("NV900", ThaiName));
        first.Code.Should().BeOneOf(200, 201);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("nv900", "Lê Văn Cường"));

        r.Code.Should().Be(409, r.ToString());
        (await CountEmployeesAsync("NV900")).Should().Be(1);
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 068–070 biên

    [Fact]
    public async Task IT_B1_068_EmployeeCode_Length_20_Accepted_21_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var code20 = "NV" + new string('1', 18);
        var code21 = "NV" + new string('2', 19);
        var before = await SnapshotAsync();

        var tooLong = await PostAsync(A + "Employees", token, Body(code21, ThaiName));

        tooLong.Code.Should().Be(400, tooLong.ToString());
        (await CountEmployeesAsync(code21)).Should().Be(0);
        await AssertDbUnchangedAsync(before);

        var ok = await PostAsync(A + "Employees", token, Body(code20, ThaiName));

        await AssertCreatedAsync(ok, code20, ThaiName, RoleCodes.WarehouseKeeper);
        (await CountEmployeesAsync(code20)).Should().Be(1);
    }

    [Fact]
    public async Task IT_B1_069_FullName_Length_100_Accepted_With_Diacritics_101_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var name100 = VietnameseName(100);
        var name101 = VietnameseName(101);
        name100.Length.Should().Be(100);
        name101.Length.Should().Be(101);
        var before = await SnapshotAsync();

        var tooLong = await PostAsync(A + "Employees", token, Body("NV910", name101));

        tooLong.Code.Should().Be(400, tooLong.ToString());
        (await CountEmployeesAsync("NV910")).Should().Be(0);
        await AssertDbUnchangedAsync(before);

        var ok = await PostAsync(A + "Employees", token, Body("NV911", name100));

        await AssertCreatedAsync(ok, "NV911", name100, RoleCodes.WarehouseKeeper);
        (await EmployeeByCodeAsync("NV911")).FullName.Should().Contain("ệ").And.NotContain("?");
    }

    [Fact]
    public async Task IT_B1_070_Gender_Single_Char_Accepted_Two_Chars_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var bad = await PostAsync(A + "Employees", token, Body("NV912", ThaiName, gender: "MA"));

        bad.Code.Should().Be(400, bad.ToString());
        (await CountEmployeesAsync("NV912")).Should().Be(0);
        await AssertDbUnchangedAsync(before);

        var ok = await PostAsync(A + "Employees", token, Body("NV913", ThaiName, gender: "M"));

        await AssertCreatedAsync(ok, "NV913", ThaiName, RoleCodes.WarehouseKeeper);
        (await EmployeeByCodeAsync("NV913")).Gender.Should().Be("M");
    }

    private async Task<NX_MTD.Domain.Entities.Employee> EmployeeByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == code);
    }
}
