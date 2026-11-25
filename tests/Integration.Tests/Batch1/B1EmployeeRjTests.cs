using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_071–080 — tạo nhân viên: FK/validation, sinh Username, rollback, và import từ RJ snapshot.</summary>
public sealed class B1EmployeeRjTests(ITestOutputHelper output) : B1TestBase(output)
{
    private const string ThaiName = "Phạm Hồng Thái";

    private object Body(string code, string fullName, string role = "WarehouseKeeper", int? teamId = null) => new
    {
        EmployeeCode = code, FullName = fullName, Gender = "M", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0912345678",
        Role = role, TeamId = teamId, PayType = "Monthly", StartDate = Clock.Today, AttendanceCode = (int?)null,
    };

    private async Task<int> CountByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.EmployeeCode == code);
    }

    private async Task<int> CountByAttendanceAsync(int attendanceCode)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.AttendanceCode == attendanceCode);
    }

    // ---------------------------------------------------------------- 071–074

    [Fact]
    public async Task IT_B1_071_Create_With_Unknown_Or_Discontinued_Team_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var missingTeam = await NonExistingTeamIdAsync();
        var before = await SnapshotAsync();

        var unknown = await PostAsync(A + "Employees", token, Body("NV940", ThaiName, teamId: missingTeam));
        var old = await PostAsync(A + "Employees", token, Body("NV941", ThaiName, teamId: S0.TOld.TeamId));

        unknown.Code.Should().BeOneOf(400, 404);
        old.Code.Should().Be(400, old.ToString());
        (await CountByCodeAsync("NV940")).Should().Be(0);
        (await CountByCodeAsync("NV941")).Should().Be(0);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_072_Create_Missing_FullName_Missing_StartDate_Or_Bad_PayType_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();
        const string noName = """{"EmployeeCode":"NV942","Role":"WarehouseKeeper","PayType":"Monthly","StartDate":"2026-11-24"}""";
        const string noStart = """{"EmployeeCode":"NV943","FullName":"Phạm Hồng Thái","Role":"WarehouseKeeper","PayType":"Monthly"}""";
        const string badPay = """{"EmployeeCode":"NV944","FullName":"Phạm Hồng Thái","Role":"WarehouseKeeper","PayType":"Xyz","StartDate":"2026-11-24"}""";

        var r1 = await PostAsync(A + "Employees", token, noName);
        var r2 = await PostAsync(A + "Employees", token, noStart);
        var r3 = await PostAsync(A + "Employees", token, badPay);

        r1.Code.Should().Be(400, r1.ToString());
        r2.Code.Should().Be(400, r2.ToString());
        r3.Code.Should().Be(400, r3.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_073_Two_Employees_With_Same_FullName_Get_Different_Usernames()
    {
        var token = await TokenAsync(S0.Adm);

        var first = await PostAsync(A + "Employees", token, Body("NV910", ThaiName));
        var second = await PostAsync(A + "Employees", token, Body("NV911", ThaiName));

        ShouldBeCreated(first);
        ShouldBeCreated(second);
        var u1 = B1Json.Str(first.Data, "username");
        var u2 = B1Json.Str(second.Data, "username");
        u1.Should().NotBe(u2);
        await using var db = NewDb();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => a.Employee.EmployeeCode == "NV910" || a.Employee.EmployeeCode == "NV911").Select(a => a.Username).ToListAsync();
        accounts.Should().HaveCount(2);
        accounts.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task IT_B1_074_Username_Collision_Never_Returns_500_And_Leaves_No_Orphan_Employee()
    {
        var anRole = Ctx.Roles[RoleCodes.Worker].RoleId;
        var holder = new Employee
        {
            EmployeeCode = "S0-ANNV", FullName = "Holder An", RoleId = anRole, PayType = PayTypes.Monthly,
            StartDate = Clock.Today.AddYears(-1), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        };
        Db.Employees.Add(holder);
        await Db.SaveChangesAsync();
        Db.Accounts.Add(new Account
        {
            EmployeeId = holder.EmployeeId, Username = "AnNV", PasswordHash = S0.Adm.PasswordHash, IsTemporaryPassword = false,
            SecurityStamp = Guid.NewGuid(), Status = RecordStatus.Active, CreatedAt = Clock.Now,
        });
        await Db.SaveChangesAsync();
        var token = await TokenAsync(S0.Adm);

        var r = await PostAsync(A + "Employees", token, Body("NV920", "Nguyễn Văn An", role: "Electrician"));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 409);
        if (r.Code == 409)
        {
            (await CountByCodeAsync("NV920")).Should().Be(0, "transaction phải rollback, không có Employee mồ côi");
        }
        else
        {
            B1Json.Str(r.Data, "username").Should().NotBe("AnNV");
            await using var db = NewDb();
            (await db.Accounts.AsNoTracking().CountAsync(a => a.Employee.EmployeeCode == "NV920")).Should().Be(1);
        }

        await using var check = NewDb();
        (await check.Accounts.AsNoTracking().CountAsync(a => a.Username == "AnNV")).Should().Be(1);
    }

    // ---------------------------------------------------------------- 075–080 from-rj

    [Fact]
    public async Task IT_B1_075_Manager_Imports_Worker_From_Rj_Snapshot()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(M + "Employees/from-rj", token,
            new { AttendanceCode = 2001, TeamId = S0.TSew02.TeamId, PayType = "PieceRate", StartDate = Clock.Today });

        ShouldBeCreated(r);
        B1Json.Int(r.Data, "employeeId").Should().NotBeNull();
        B1Json.Str(r.Data, "username").Should().NotBeNullOrWhiteSpace();
        B1Json.Str(r.Data, "temporaryPassword").Should().NotBeNullOrWhiteSpace();
        r.CacheControl.Should().Contain("no-store");

        await using var db = NewDb();
        var emp = await db.Employees.AsNoTracking().Include(e => e.Role).SingleAsync(e => e.AttendanceCode == 2001);
        emp.FullName.Should().Be("Nguyễn Văn An");
        emp.Role.RoleCode.Should().Be(RoleCodes.Worker);
        emp.TeamId.Should().Be(S0.TSew02.TeamId);
        emp.Status.Should().Be("ACTIVE");
        var acc = await db.Accounts.AsNoTracking().SingleAsync(a => a.EmployeeId == emp.EmployeeId);
        acc.IsTemporaryPassword.Should().BeTrue();
        Note("IT_B1_075 RjEmployeeSnapshots không có cột 'đã nhập'; trạng thái nhập suy ra từ Employee.AttendanceCode.");
        await AssertSingleAuditAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B1_076_Import_From_Rj_With_Only_AttendanceCode_Uses_Defaults()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 2002 });

        ShouldBeCreated(r);
        await using var db = NewDb();
        var emp = await db.Employees.AsNoTracking().SingleAsync(e => e.AttendanceCode == 2002);
        emp.TeamId.Should().BeNull();
        Note($"IT_B1_076 mặc định: PayType={emp.PayType}, StartDate={emp.StartDate}");
    }

    [Fact]
    public async Task IT_B1_077_Import_From_Rj_Code_Not_In_Snapshot_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 999999 });

        r.Code.Should().BeOneOf(400, 404);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_078_Import_From_Rj_Already_Imported_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 1001 });

        r.Code.Should().Be(409, r.ToString());
        (await CountByAttendanceAsync(1001)).Should().Be(1);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_079_Import_From_Rj_Zero_Or_Negative_AttendanceCode_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var zero = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 0 });
        var negative = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = -1 });

        zero.Code.Should().BeOneOf(400, 404);
        negative.Code.Should().BeOneOf(400, 404);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_080_Import_From_Rj_Into_Discontinued_Team_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 2001, TeamId = S0.TOld.TeamId });

        r.Code.Should().Be(400, r.ToString());
        (await CountByAttendanceAsync(2001)).Should().Be(0, "snapshot 2001 vẫn chưa nhập");
        await AssertDbUnchangedAsync(before);
    }
}
