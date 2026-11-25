using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>
/// IT_B1_095, 151, 172 — request song song trên connection DB độc lập (dữ liệu S0 được commit, tự dọn sau test).
/// </summary>
public sealed class B1ConcurrencyTests(ITestOutputHelper output) : B1ConcurrentTestBase(output)
{
    [Fact]
    public async Task IT_B1_095_Two_Concurrent_Updates_Same_RowVer_Exactly_One_Wins()
    {
        var token = await TokenAsync(S0.Adm);
        var lastAudit = await LastAuditIdAsync();
        var bodyA = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Một");
        var bodyB = await UpdateBodyAsync(S0.Wh.EmployeeId, fullName: "Kho Hai");

        var results = await Task.WhenAll(
            PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, bodyA),
            PutAsync(A + $"Employees/{S0.Wh.EmployeeId}", token, bodyB));

        var summary = string.Join(" | ", results.Select(r => r.ToString()));
        results.Count(r => r.Code == 200).Should().Be(1, summary);
        results.Count(r => r.Code == 409).Should().Be(1, summary);
        var winner = results[0].Code == 200 ? "Kho Một" : "Kho Hai";
        (await EmployeeAsync(S0.Wh.EmployeeId)).FullName.Should().Be(winner, "chỉ dữ liệu của request thắng được lưu");
        var rows = (await AuditRowsAfterAsync(lastAudit)).Where(a => a.TableName == nameof(Employee) && a.RecordId == S0.Wh.EmployeeId).ToList();
        rows.Should().HaveCount(1, "AuditLogs +1");
    }

    [Fact]
    public async Task IT_B1_151_Two_Concurrent_Leader_Appointments_Exactly_One_Wins_No_500()
    {
        var token = await TokenAsync(S0.Mgr);
        var url = M + $"Teams/{S0.TSew02.TeamId}/appoint";

        var results = await Task.WhenAll(
            PostAsync(url, token, new { EmployeeId = S0.W2.EmployeeId, Position = "Leader" }),
            PostAsync(url, token, new { EmployeeId = S0.W5.EmployeeId, Position = "Leader" }));

        var summary = string.Join(" | ", results.Select(r => r.ToString()));
        results.Should().OnlyContain(r => r.Code < 500, summary);
        results.Count(r => r.Code == 200).Should().Be(1, summary);
        results.Count(r => r.Code is 400 or 409).Should().Be(1, summary);
        foreach (var r in results)
            r.Body.Should().NotContainAny("RAISERROR", "THROW", "trigger", "Trigger");

        await using var db = NewDb();
        var leaders = await db.Employees.AsNoTracking()
            .CountAsync(e => e.TeamId == S0.TSew02.TeamId && e.Role.RoleCode == RoleCodes.TeamLeader);
        leaders.Should().Be(1);
    }

    [Fact]
    public async Task IT_B1_172_Two_Concurrent_Creates_Same_AttendanceCode_Exactly_One_Created_Other_409()
    {
        var token = await TokenAsync(S0.Adm);
        object Body(string code, string name) => new
        {
            EmployeeCode = code, FullName = name, Gender = "M", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0912345678",
            Role = "Electrician", TeamId = (int?)null, PayType = "Monthly", StartDate = Clock.Today, AttendanceCode = 5002,
        };

        var results = await Task.WhenAll(
            PostAsync(A + "Employees", token, Body("NV950", "Lê Văn Cường")),
            PostAsync(A + "Employees", token, Body("NV951", "Trần Thị Dung")));

        var summary = string.Join(" | ", results.Select(r => r.ToString()));
        results.Should().OnlyContain(r => r.Code < 500, summary);
        results.Count(r => r.Code is 200 or 201).Should().Be(1, summary);
        results.Count(r => r.Code == 409).Should().Be(1, summary);

        await using var db = NewDb();
        (await db.Employees.AsNoTracking().CountAsync(e => e.AttendanceCode == 5002)).Should().Be(1);
        var codes = new[] { "NV950", "NV951" };
        var employees = await db.Employees.AsNoTracking().Where(e => codes.Contains(e.EmployeeCode)).Select(e => e.EmployeeId).ToListAsync();
        employees.Should().HaveCount(1, "không có Employee mồ côi của request thua");
        (await db.Accounts.AsNoTracking().CountAsync(a => codes.Contains(a.Employee.EmployeeCode))).Should().Be(1);
    }
}
