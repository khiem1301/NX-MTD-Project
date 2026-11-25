using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_081–091 — danh sách, lọc, phân trang và chi tiết nhân viên; ranh giới quyền Manager.</summary>
public sealed class B1EmployeeQueryTests(ITestOutputHelper output) : B1TestBase(output)
{
    private static int TotalItems(Api r) => B1Json.Int(r.Data, "totalItems") ?? -1;

    // ---------------------------------------------------------------- 081–087

    [Fact]
    public async Task IT_B1_081_List_Default_Status_Returns_Only_Active_Employees()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(A + "Employees?pageSize=100", token);

        r.Code.Should().Be(200, r.ToString());
        IdSet(r, "employeeId").Should().NotContain(S0.Rs.EmployeeId);
        // DTO dùng PascalCase (Active/Resigned) theo contract §2; DB lưu chữ HOA (kiểm ở TotalItems bên dưới).
        r.Items().Select(i => B1Json.Str(i, "status")).Should().OnlyContain(s => s == "Active");
        await using var db = NewDb();
        TotalItems(r).Should().Be(await db.Employees.AsNoTracking().CountAsync(e => e.Status == "ACTIVE"));
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_082_List_Status_Resigned_And_All_Match_Db_Counts()
    {
        var token = await TokenAsync(S0.Adm);
        await using var db = NewDb();
        var resignedIds = await db.Employees.AsNoTracking().Where(e => e.Status == "RESIGNED").Select(e => e.EmployeeId).ToListAsync();
        var total = await db.Employees.AsNoTracking().CountAsync();

        var resigned = await GetAsync(A + "Employees?Status=Resigned&pageSize=100", token);
        var all = await GetAsync(A + "Employees?Status=All&pageSize=100", token);

        resigned.Code.Should().Be(200, resigned.ToString());
        IdSet(resigned, "employeeId").Should().BeEquivalentTo(resignedIds);
        IdSet(resigned, "employeeId").Should().Contain(S0.Rs.EmployeeId);
        all.Code.Should().Be(200, all.ToString());
        TotalItems(all).Should().Be(total);
        var statuses = all.Items().Select(i => B1Json.Str(i, "status")).ToHashSet();
        statuses.Should().Contain(["Active", "Resigned"]);
    }

    [Fact]
    public async Task IT_B1_083_List_Status_Uppercase_Or_Unknown_Never_500()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var upper = await GetAsync(A + "Employees?Status=RESIGNED", token);
        var unknown = await GetAsync(A + "Employees?Status=Xyz", token);

        upper.Code.Should().BeOneOf(200, 400);
        Note($"IT_B1_083 Status=RESIGNED => {upper.Code}");
        if (upper.Code == 200)
            upper.Items().Select(i => B1Json.Str(i, "status")).Should().OnlyContain(s => s == "Resigned");
        unknown.Code.Should().Be(400, unknown.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_084_List_Filter_By_Team_And_Role_Returns_Only_Matching_Worker()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(A + $"Employees?TeamId={S0.TSew01.TeamId}&Role=Worker", token);

        r.Code.Should().Be(200, r.ToString());
        IdSet(r, "employeeId").Should().BeEquivalentTo(new[] { S0.W1.EmployeeId });
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_085_Manager_List_Hides_Admin_And_Accountant()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await GetAsync(M + "Employees?Status=All&pageSize=100", token);

        r.Code.Should().Be(200, r.ToString());
        var ids = IdSet(r, "employeeId");
        ids.Should().NotContain([S0.Adm.EmployeeId, S0.Adm2.EmployeeId, S0.Acc.EmployeeId]);
        Note($"IT_B1_085 Manager tự xuất hiện trong danh sách: {ids.Contains(S0.Mgr.EmployeeId)}");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_086_Manager_Filter_By_Admin_Or_Accountant_Role_Leaks_Nothing()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        foreach (var role in new[] { "Admin", "Accountant" })
        {
            var r = await GetAsync(M + $"Employees?Role={role}&Status=All", token);
            r.Code.Should().BeOneOf(200, 403);
            if (r.Code == 200) r.Items().Should().BeEmpty(role);
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_087_List_Paging_Boundaries_Never_500()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var beyond = await GetAsync(A + "Employees?page=9999&pageSize=20", token);
        beyond.Code.Should().Be(200, beyond.ToString());
        beyond.Items().Should().BeEmpty();

        foreach (var size in new[] { "0", "-1", "100000" })
        {
            var r = await GetAsync(A + $"Employees?page=1&pageSize={size}", token);
            r.Code.Should().BeOneOf(200, 400);
            if (r.Code == 200)
            {
                r.Items().Count.Should().BeLessThanOrEqualTo(100, $"pageSize={size} phải bị kẹp về giới hạn");
                Note($"IT_B1_087 pageSize={size} => 200, pageSize thực tế {B1Json.Int(r.Data, "pageSize")}");
            }
        }

        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 088–091 detail

    [Fact]
    public async Task IT_B1_088_Admin_Detail_Includes_Account_RowVer_And_History_Matching_Db()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(A + $"Employees/{S0.W1.EmployeeId}", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "employeeId").Should().Be(S0.W1.EmployeeId);
        var account = B1Json.Prop(r.Data, "account");
        account.Should().NotBeNull();
        account!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        B1Json.Str(account.Value, "username").Should().Be("wk01");
        B1Json.Str(account.Value, "status").Should().Be("Active");
        var emp = await EmployeeAsync(S0.W1.EmployeeId);
        B1Json.Str(r.Data, "rowVer").Should().Be(Convert.ToBase64String(emp.RowVer));
        B1Json.Str(r.Data, "fullName").Should().Be(emp.FullName);
        B1Json.Str(r.Data, "status")!.ToUpperInvariant().Should().Be(emp.Status, "DTO PascalCase, DB chữ HOA");

        var history = B1Json.Prop(r.Data, "history");
        history.Should().NotBeNull();
        await using var db = NewDb();
        var auditCount = await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.TableName == nameof(NX_MTD.Domain.Entities.Employee) && a.RecordId == S0.W1.EmployeeId);
        history!.Value.GetArrayLength().Should().Be(auditCount);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_089_Manager_Detail_Hides_Account()
    {
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        var r = await GetAsync(M + $"Employees/{S0.W1.EmployeeId}", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "employeeId").Should().Be(S0.W1.EmployeeId);
        var account = B1Json.Prop(r.Data, "account");
        (account is null || account.Value.ValueKind == System.Text.Json.JsonValueKind.Null).Should().BeTrue($"Manager nhận Account = null. Body: {r.Body}");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_090_Manager_Cannot_Read_Admin_Or_Accountant_Detail_No_Data_Leak()
    {
        S0.Adm2.Employee.Phone = "0900000002";
        S0.Acc.Employee.Phone = "0900000003";
        await Db.SaveChangesAsync();
        var token = await TokenAsync(S0.Mgr);
        var before = await SnapshotAsync();

        foreach (var target in new[] { S0.Adm2, S0.Acc })
        {
            var r = await GetAsync(M + $"Employees/{target.EmployeeId}", token);
            r.Code.Should().BeOneOf(403, 404);
            r.Body.Should().NotContain(target.Employee.FullName);
            r.Body.Should().NotContain(target.Employee.Phone!);
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_091_Detail_Unknown_Id_Or_NonInteger_Route_Returns_404()
    {
        var token = await TokenAsync(S0.Adm);
        var missing = await NonExistingEmployeeIdAsync();
        var before = await SnapshotAsync();

        var unknown = await GetAsync(A + $"Employees/{missing}", token);
        var nonInt = await GetAsync(A + "Employees/abc", token);

        unknown.Code.Should().Be(404, unknown.ToString());
        nonInt.Code.Should().Be(404, nonInt.ToString());
        await AssertDbUnchangedAsync(before);
    }
}
