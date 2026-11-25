using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_208–212 — đồng bộ trạng thái và AuditLog (kiểm tra chéo).</summary>
public sealed class B1CrossCheckTests(ITestOutputHelper output) : B1TestBase(output)
{
    private async Task<int> ResignAsync(string token, int employeeId, DateOnly date) =>
        (await PostAsync(A + $"Employees/{employeeId}/resign", token,
            new { ResignDate = date, Reason = "Nghỉ theo nguyện vọng", RowVer = await RowVerAsync(employeeId) })).Code;

    [Fact]
    public async Task IT_B1_208_Account_And_Profile_Operations_Are_Audited_Without_Secrets()
    {
        var token = await TokenAsync(S0.Adm);
        var lastAudit = await LastAuditIdAsync();
        var id = S0.Wh.EmployeeId;

        var lck = await PostAsync(A + $"Employees/{id}/account/lock", token, new { Reason = "Kiểm tra chuỗi" });
        var unlock = await PostAsync(A + $"Employees/{id}/account/unlock", token);
        var reset = await PostAsync(A + $"Employees/{id}/account/reset-password", token);
        var put = await PutAsync(A + $"Employees/{id}", token, await UpdateBodyAsync(id, phone: "0911222333"));

        foreach (var r in new[] { lck, unlock, reset, put }) r.Code.Should().Be(200, r.ToString());
        var tempPassword = B1Json.Str(reset.Data, "temporaryPassword")!;

        var rows = await AuditRowsAfterAsync(lastAudit);
        rows.Should().HaveCount(4, "AuditLogs +4");
        rows.Should().OnlyContain(a =>
            (a.TableName == "Employee" && a.RecordId == id) || (a.TableName == "Account" && a.RecordId == S0.Wh.AccountId),
            "mọi dòng gắn với E_WH");
        var accountHash = (await AccountAsync(S0.Wh.AccountId)).PasswordHash;
        foreach (var a in rows)
        {
            var text = $"{a.OldValues}{a.NewValues}{a.Summary}{a.Reason}";
            text.Should().NotContain(tempPassword, "D29: không ghi mật khẩu tạm");
            text.Should().NotContain(accountHash, "không ghi PasswordHash");
        }

        var detail = await GetAsync(A + $"Employees/{id}", token);
        detail.Code.Should().Be(200, detail.ToString());
        var history = B1Json.Prop(detail.Data, "history")!.Value;
        history.GetArrayLength().Should().Be(4, "History[] gồm 4 mục mới");
        var stamps = history.EnumerateArray().Select(h => B1Json.Str(h, "changedAt")).Where(s => s is not null).Select(s => DateTime.Parse(s!)).ToList();
        (stamps.SequenceEqual(stamps.OrderBy(x => x)) || stamps.SequenceEqual(stamps.OrderByDescending(x => x)))
            .Should().BeTrue("theo đúng thứ tự thời gian");
    }

    [Fact]
    public async Task IT_B1_209_Team_Operations_Chain_Is_Recorded_In_Team_History()
    {
        var token = await TokenAsync(S0.Mgr);
        var lastAudit = await LastAuditIdAsync();
        var team = S0.TSew02.TeamId;

        var appoint = await PostAsync(M + $"Teams/{team}/appoint", token, new { EmployeeId = S0.W2.EmployeeId, Position = "Leader" });
        var dismiss = await PostAsync(M + $"Teams/{team}/dismiss", token, new { EmployeeId = S0.W2.EmployeeId });
        var transfer = await PostAsync(M + $"Teams/{team}/transfer-in", token,
            new { EmployeeIds = new[] { S0.W3.EmployeeId }, EffectiveDate = Clock.Today, Reason = "Điều chuyển" });

        foreach (var r in new[] { appoint, dismiss, transfer }) r.Code.Should().Be(200, r.ToString());
        (await RoleCodeOfAsync(S0.W2.EmployeeId)).Should().Be(RoleCodes.Worker);

        var teamRows = (await AuditRowsAfterAsync(lastAudit)).Where(a => a.TableName == "Team" && a.RecordId == team).ToList();
        teamRows.Should().HaveCount(3, "AuditLogs +3 gắn với tổ T_SEW02");
        var detail = await GetAsync(M + $"Teams/{team}", token);
        detail.Code.Should().Be(200, detail.ToString());
        B1Json.Prop(detail.Data, "history")!.Value.GetArrayLength().Should().Be(3, "History gồm đủ 3 sự kiện");
    }

    [Fact]
    public async Task IT_B1_210_Failed_Operations_Leave_AuditLog_Untouched()
    {
        var token = await TokenAsync(S0.Adm);
        var mgr = await TokenAsync(S0.Mgr);

        var staleBody = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Mới");
        var first = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, staleBody);
        first.Code.Should().Be(200, first.ToString());
        var lastAudit = await LastAuditIdAsync();

        var stale = await PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Cũ", rowVer: (byte[])staleBody["RowVer"]!));
        var appoint = await PostAsync(M + $"Teams/{S0.TSew01.TeamId}/appoint", mgr, new { EmployeeId = S0.W1.EmployeeId, Position = "Leader" });
        var partial = await PostAsync(M + $"Teams/{S0.TEmpty.TeamId}/add-members", mgr,
            new { EmployeeIds = new[] { S0.W3.EmployeeId, S0.W1.EmployeeId }, Reason = "Một phần không hợp lệ" });

        stale.Code.Should().Be(409, stale.ToString());
        appoint.Code.Should().BeOneOf(400, 409);
        partial.Code.Should().BeOneOf(400, 409);
        (await LastAuditIdAsync()).Should().Be(lastAudit, "log rollback cùng thao tác chính");
    }

    [Fact]
    public async Task IT_B1_211_All_Persisted_Status_Values_Are_Uppercase()
    {
        var token = await TokenAsync(S0.Adm);
        (await PostAsync(A + $"Employees/{S0.Wh.EmployeeId}/account/lock", token, new { Reason = "Khóa" })).Code.Should().Be(200);
        (await ResignAsync(token, S0.W2.EmployeeId, Clock.Today)).Should().Be(200);
        (await PostAsync(M + $"Teams/{S0.TEmpty.TeamId}/discontinue", await TokenAsync(S0.Mgr))).Code.Should().Be(200);
        (await PutAsync(A + "grade-codes/B2", token, new { Status = "Inactive" })).Code.Should().Be(200);

        await using var db = NewDb();
        var values = new Dictionary<string, List<string>>
        {
            ["Employee"] = await db.Employees.AsNoTracking().Select(e => e.Status).Distinct().ToListAsync(),
            ["Account"] = await db.Accounts.AsNoTracking().Select(a => a.Status).Distinct().ToListAsync(),
            ["Team"] = await db.Teams.AsNoTracking().Select(t => t.Status).Distinct().ToListAsync(),
            ["GradeLevelCode"] = await db.GradeLevelCodes.AsNoTracking().Select(g => g.Status).Distinct().ToListAsync(),
        };

        foreach (var (table, list) in values)
        {
            Note($"{table}: {string.Join(", ", list)}");
            list.Should().OnlyContain(v => v == v.ToUpperInvariant(), $"{table}.Status phải VIẾT HOA");
            list.Should().NotContain(["Active", "Resigned", "Inactive"], table);
        }

        values["Employee"].Should().Contain("RESIGNED");
        values["Account"].Should().Contain("LOCKED");
        values["Team"].Should().Contain("DISCONTINUED");
        values["GradeLevelCode"].Should().Contain("INACTIVE");
    }

    [Fact]
    public async Task IT_B1_212_Resigned_Employee_Is_Consistent_Between_Db_And_Dto()
    {
        var token = await TokenAsync(S0.Adm);
        S0.Wh.Employee.StartDate = new DateOnly(2024, 1, 1);
        await Db.SaveChangesAsync();
        (await ResignAsync(token, S0.Wh.EmployeeId, Clock.Today)).Should().Be(200);

        var list = await GetAsync(A + "Employees?Status=Resigned&pageSize=100", token);
        var detail = await GetAsync(A + $"Employees/{S0.Wh.EmployeeId}", token);

        list.Code.Should().Be(200, list.ToString());
        IdSet(list, "employeeId").Should().Contain(S0.Wh.EmployeeId);
        detail.Code.Should().Be(200, detail.ToString());
        var dbStatus = (await EmployeeAsync(S0.Wh.EmployeeId)).Status;
        dbStatus.Should().Be("RESIGNED");
        B1Json.Str(detail.Data, "status").Should().Be("Resigned", "DTO dùng PascalCase");
        B1Json.Str(detail.Data, "status")!.ToUpperInvariant().Should().Be(dbStatus, "chuyển đổi DB ↔ DTO nhất quán");
        var inList = list.Items().Single(i => B1Json.Int(i, "employeeId") == S0.Wh.EmployeeId);
        B1Json.Str(inList, "status")!.ToUpperInvariant().Should().Be(dbStatus);
    }
}
