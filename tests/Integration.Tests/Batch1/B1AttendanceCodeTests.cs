using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_165–173 (trừ 172 ở B1ConcurrencyTests) — Filtered Unique Index UX_Employee_AttendanceCode.</summary>
public sealed class B1AttendanceCodeTests(ITestOutputHelper output) : B1TestBase(output)
{
    private object Body(string code, string name, object? attendanceCode, string role = "Electrician") => new
    {
        EmployeeCode = code, FullName = name, Gender = "M", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0912345678",
        Role = role, TeamId = (int?)null, PayType = "Monthly", StartDate = Clock.Today, AttendanceCode = attendanceCode,
    };

    private async Task<int> CountAsync(string code)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.EmployeeCode == code);
    }

    private async Task<int> CountAttendanceAsync(int attendanceCode)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.AttendanceCode == attendanceCode);
    }

    private async Task<int> CountNullAttendanceAsync()
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.AttendanceCode == null);
    }

    [Fact]
    public async Task IT_B1_165_Many_Employees_With_Null_AttendanceCode_Are_Allowed()
    {
        var token = await TokenAsync(S0.Adm);
        var nullsBefore = await CountNullAttendanceAsync();

        var first = await PostAsync(A + "Employees", token, Body("NV930", "Lê Văn Cường", null));
        var second = await PostAsync(A + "Employees", token,
            """{"EmployeeCode":"NV931","FullName":"Trần Thị Dung","Role":"Electrician","PayType":"Monthly","StartDate":"2026-11-24"}""");

        ShouldBeCreated(first);
        ShouldBeCreated(second);
        (await CountNullAttendanceAsync()).Should().Be(nullsBefore + 2);
    }

    [Fact]
    public async Task IT_B1_166_Create_With_Existing_AttendanceCode_Returns_409_And_Rolls_Back()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV932", "Lê Văn Cường", 1001));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        (await CountAttendanceAsync(1001)).Should().Be(1);
        (await CountAsync("NV932")).Should().Be(0);
        await using var db = NewDb();
        (await db.Accounts.AsNoTracking().CountAsync(a => a.Employee.EmployeeCode == "NV932")).Should().Be(0, "không có Account mồ côi");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_167_Second_Create_With_Same_New_AttendanceCode_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);

        var first = await PostAsync(A + "Employees", token, Body("NV933", "Lê Văn Cường", 5001));
        ShouldBeCreated(first);
        var before = await SnapshotAsync();
        var second = await PostAsync(A + "Employees", token, Body("NV934", "Trần Thị Dung", 5001));

        second.ShouldNotBeServerError();
        second.Code.Should().Be(409, second.ToString());
        (await CountAttendanceAsync(5001)).Should().Be(1);
        await using var db = NewDb();
        (await db.Employees.AsNoTracking().SingleAsync(e => e.AttendanceCode == 5001)).EmployeeCode.Should().Be("NV933");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_168_Update_To_AttendanceCode_Of_Another_Employee_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var body = await UpdateBodyAsync(S0.Wh.EmployeeId, keepAttendance: false, attendanceCode: 1002);
        var before = await SnapshotAsync();
        var rowVer = await RowVerAsync(S0.Wh.EmployeeId);

        var r = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, body);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        var wh = await EmployeeAsync(S0.Wh.EmployeeId);
        wh.AttendanceCode.Should().BeNull();
        wh.RowVer.Should().Equal(rowVer);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_169_Setting_AttendanceCode_To_Null_Is_Valid_Alongside_Other_Nulls()
    {
        var token = await TokenAsync(S0.Adm);
        var body = await UpdateBodyAsync(S0.W1.EmployeeId, keepAttendance: false, attendanceCode: null);

        var r = await PutAsync(A + $"Employees/{S0.W1.EmployeeId}", token, body);

        r.Code.Should().Be(200, r.ToString());
        (await EmployeeAsync(S0.W1.EmployeeId)).AttendanceCode.Should().BeNull();
        (await EmployeeAsync(S0.W3.EmployeeId)).AttendanceCode.Should().BeNull();
    }

    [Fact]
    public async Task IT_B1_170_AttendanceCode_Of_Resigned_Employee_Is_Still_Reserved()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees", token, Body("NV935", "Lê Văn Cường", 1009));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_171_AttendanceCode_Integer_Boundaries()
    {
        var token = await TokenAsync(S0.Adm);
        using var scope = new AssertionScope(); // đủ 4 mốc, không để mốc đầu che mốc sau

        var max = await PostAsync(A + "Employees", token, Body("NV936", "Lê Văn Cường", int.MaxValue));
        var zero = await PostAsync(A + "Employees", token, Body("NV937", "Trần Thị Dung", 0));
        var negative = await PostAsync(A + "Employees", token, Body("NV938", "Phạm Văn Em", -1));
        var overflow = await PostAsync(A + "Employees", token, Body("NV939", "Hoàng Thị Giang", 2147483648L));

        foreach (var r in new[] { max, zero, negative, overflow }) r.ShouldNotBeServerError();
        max.Code.Should().BeOneOf(new[] { 200, 201 }, max.ToString());
        zero.Code.Should().BeOneOf(200, 201, 400);
        negative.Code.Should().BeOneOf(200, 201, 400);
        overflow.Code.Should().Be(400, overflow.ToString());

        (await CountAsync("NV936")).Should().Be(max.Code is 200 or 201 ? 1 : 0);
        (await CountAsync("NV937")).Should().Be(zero.Code is 200 or 201 ? 1 : 0);
        (await CountAsync("NV938")).Should().Be(negative.Code is 200 or 201 ? 1 : 0);
        (await CountAsync("NV939")).Should().Be(0);
        Note($"IT_B1_171 max={max.Code}, 0={zero.Code}, -1={negative.Code}, overflow={overflow.Code}");
    }

    [Fact]
    public async Task IT_B1_173_Import_From_Rj_Code_Already_Taken_By_Manual_Employee_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var manual = await PostAsync(A + "Employees", token, Body("NV940", "Lê Văn Cường", 2002));
        ShouldBeCreated(manual);
        var before = await SnapshotAsync();

        var r = await PostAsync(A + "Employees/from-rj", token, new { AttendanceCode = 2002 });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        (await CountAttendanceAsync(2002)).Should().Be(1);
        await AssertDbUnchangedAsync(before, "không Employee/Account mới");
    }
}
