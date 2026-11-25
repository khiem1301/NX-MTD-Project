using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_174–185 — bất thường hợp đồng: Email không có cột lưu; PUT GradeCodes chỉ có Status.</summary>
public sealed class B1ContractAnomalyTests(ITestOutputHelper output) : B1TestBase(output)
{
    private object Body(string code, string name, object? email, bool withEmail = true)
    {
        var d = new Dictionary<string, object?>
        {
            ["EmployeeCode"] = code, ["FullName"] = name, ["Gender"] = "M", ["DateOfBirth"] = new DateOnly(1990, 1, 1),
            ["Phone"] = "0912345678", ["Role"] = "WarehouseKeeper", ["TeamId"] = null, ["PayType"] = "Monthly",
            ["StartDate"] = Clock.Today, ["AttendanceCode"] = null,
        };
        if (withEmail) d["Email"] = email;
        return d;
    }

    private async Task<List<string>> EmailLikeColumnsAsync()
    {
        return await Db.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME IN ('Employee','Account') AND COLUMN_NAME LIKE '%mail%'")
            .ToListAsync();
    }

    private async Task<List<string>> ColumnsOfAsync(string table) =>
        await Db.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0} ORDER BY ORDINAL_POSITION", table).ToListAsync();

    private async Task<int> CountByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.EmployeeCode == code);
    }

    // ---------------------------------------------------------------- 174–177 Email

    [Fact]
    public async Task IT_B1_174_Create_With_Valid_Email_Succeeds_But_Email_Is_Not_Persisted()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PostAsync(A + "Employees", token, Body("NV940", "Phạm Hồng Thái", "thai.ph@nxmtd.vn"));

        ShouldBeCreated(r);
        Note("IT_B1_174 response có trả lại Email: " + r.Body.Contains("thai.ph@nxmtd.vn"));
        await using var db = NewDb();
        (await db.Employees.AsNoTracking().CountAsync(e => e.EmployeeCode == "NV940")).Should().Be(1);
        (await db.Accounts.AsNoTracking().CountAsync(a => a.Employee.EmployeeCode == "NV940")).Should().Be(1);
        (await EmailLikeColumnsAsync()).Should().BeEmpty("không có cột Email nào nhận giá trị");
        Note("FINDING IT_B1_174: dữ liệu Email bị mất (không có cột lưu trữ).");
    }

    [Fact]
    public async Task IT_B1_175_Create_With_Malformed_Email_Is_Consistent_Never_500()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV941", "Phạm Hồng Thái", "abc"));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400);
        Note($"IT_B1_175 Email 'abc' => {r.Code}");
        if (r.Code == 400)
        {
            (await CountByCodeAsync("NV941")).Should().Be(0);
            await AssertDbUnchangedAsync(before);
        }
        else
        {
            (await CountByCodeAsync("NV941")).Should().Be(1);
        }
    }

    [Fact]
    public async Task IT_B1_176_Email_Empty_Very_Long_And_Wrong_Type()
    {
        var token = await TokenAsync(S0.Adm);
        var longEmail = new string('a', 990) + "@nxmtd.vn";
        var before = await SnapshotAsync();

        var wrongType = await PostAsync(A + "Employees", token,
            """{"EmployeeCode":"NV944","FullName":"Phạm Hồng Thái","Role":"WarehouseKeeper","PayType":"Monthly","StartDate":"2026-11-24","Email":123}""");

        wrongType.Code.Should().Be(400, wrongType.ToString());
        (await CountByCodeAsync("NV944")).Should().Be(0);
        await AssertDbUnchangedAsync(before);

        var empty = await PostAsync(A + "Employees", token, Body("NV942", "Lê Văn Cường", ""));
        var tooLong = await PostAsync(A + "Employees", token, Body("NV943", "Trần Thị Dung", longEmail));

        empty.ShouldNotBeServerError();
        tooLong.ShouldNotBeServerError();
        empty.Code.Should().BeOneOf(200, 201, 400);
        tooLong.Code.Should().BeOneOf(200, 201, 400);
        (await CountByCodeAsync("NV942")).Should().Be(empty.Code is 200 or 201 ? 1 : 0);
        (await CountByCodeAsync("NV943")).Should().Be(tooLong.Code is 200 or 201 ? 1 : 0);
    }

    [Fact]
    public async Task IT_B1_177_Create_Without_Email_Behaves_Identically_To_Create_With_Email()
    {
        var token = await TokenAsync(S0.Adm);

        var withEmail = await PostAsync(A + "Employees", token, Body("NV945", "Lê Văn Cường", "cuong@nxmtd.vn"));
        var without = await PostAsync(A + "Employees", token, Body("NV946", "Trần Thị Dung", null, withEmail: false));

        ShouldBeCreated(withEmail);
        ShouldBeCreated(without);
        withEmail.Code.Should().Be(without.Code);
        var keysA = withEmail.Data.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        var keysB = without.Data.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        keysB.Should().Equal(keysA, "cấu trúc response giống nhau");

        var a = await EmployeeAsync(B1Json.Int(withEmail.Data, "employeeId")!.Value);
        var b = await EmployeeAsync(B1Json.Int(without.Data, "employeeId")!.Value);
        (a.Gender, a.RoleId, a.PayType, a.Status, a.TeamId, a.AttendanceCode, a.StartDate, a.Phone, a.DateOfBirth)
            .Should().Be((b.Gender, b.RoleId, b.PayType, b.Status, b.TeamId, b.AttendanceCode, b.StartDate, b.Phone, b.DateOfBirth));
        var accA = await AccountOfAsync(a.EmployeeId);
        var accB = await AccountOfAsync(b.EmployeeId);
        (accA.Status, accA.IsTemporaryPassword).Should().Be((accB.Status, accB.IsTemporaryPassword));
    }

    private async Task<NX_MTD.Domain.Entities.Account> AccountOfAsync(int employeeId)
    {
        await using var db = NewDb();
        return await db.Accounts.AsNoTracking().SingleAsync(a => a.EmployeeId == employeeId);
    }

    // ---------------------------------------------------------------- 178–185 PUT grade-codes

    private string CodeUrl(string code) => A + $"grade-codes/{code}";

    private async Task<string?> GradeStatusAsync(string code)
    {
        await using var db = NewDb();
        return await db.GradeLevelCodes.AsNoTracking().Where(g => g.GradeCode == code).Select(g => g.Status).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task IT_B1_178_Deactivate_Grade_Code_Stores_Uppercase_And_Hides_From_Options()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PutAsync(CodeUrl("B2"), token, new { Status = "Inactive" });

        r.Code.Should().Be(200, r.ToString());
        (await GradeStatusAsync("B2")).Should().Be(GradeCodes.Inactive);
        var options = await GetAsync(C + "grade-code-options", await TokenAsync(S0.Acc));
        StrSet(options, "gradeCode").Should().NotContain("B2");
        var all = await GetAsync(A + "grade-codes", token);
        StrSet(all, "gradeCode").Should().Contain("B2");
    }

    [Fact]
    public async Task IT_B1_179_Extra_Fields_Are_Ignored_Only_Status_Column_Exists()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PutAsync(CodeUrl("B2"), token, new { Status = "Active", Name = "Bậc 2 mới", SortOrder = 5 });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        var columns = await ColumnsOfAsync("GradeLevelCode");
        columns.Should().BeEquivalentTo(new[] { "GradeCode", "Status", "CreatedAt" }, "không có cột tên/thứ tự");
        Note("FINDING IT_B1_179: không có cách đổi tên/thứ tự bậc thợ, trái với comment của GradeCodesController.");
    }

    [Fact]
    public async Task IT_B1_180_Update_Grade_Code_Without_Status_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(CodeUrl("B2"), token, new { Name = "X" });

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_181_Grade_Code_Cannot_Be_Renamed_Via_Body()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PutAsync(CodeUrl("B2"), token, new { GradeCode = "B5", Status = "Active" });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        (await GradeStatusAsync("B5")).Should().BeNull();
        (await GradeStatusAsync("B2")).Should().NotBeNull();
    }

    [Fact]
    public async Task IT_B1_182_Invalid_Status_Values_Return_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var deleted = await PutAsync(CodeUrl("B2"), token, new { Status = "Deleted" });
        var empty = await PutAsync(CodeUrl("B2"), token, new { Status = "" });

        deleted.Code.Should().Be(400, deleted.ToString());
        empty.Code.Should().Be(400, empty.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_183_Update_Unknown_Grade_Code_Returns_404_Without_Upsert()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(CodeUrl("ZZ"), token, new { Status = "Inactive" });

        r.Code.Should().Be(404, r.ToString());
        (await GradeStatusAsync("ZZ")).Should().BeNull();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_184_Deactivate_Grade_Code_In_Use_Keeps_Employee_Grade()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PutAsync(CodeUrl("B1"), token, new { Status = "Inactive" });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 409);
        Note($"IT_B1_184 ngừng bậc đang dùng => {r.Code}");
        (await EmployeeAsync(S0.W1.EmployeeId)).CurrentGradeCode.Should().Be("B1");
    }

    [Fact]
    public async Task IT_B1_185_Lowercase_Route_Does_Not_Create_Duplicate_Code()
    {
        var token = await TokenAsync(S0.Adm);
        await using var before = NewDb();
        var countBefore = await before.GradeLevelCodes.AsNoTracking().CountAsync();

        var r = await PutAsync(CodeUrl("b2"), token, new { Status = "Active" });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 404);
        await using var after = NewDb();
        (await after.GradeLevelCodes.AsNoTracking().CountAsync()).Should().Be(countBefore);
        (await after.GradeLevelCodes.AsNoTracking().CountAsync(g => g.GradeCode == "B2")).Should().Be(1);
    }
}
