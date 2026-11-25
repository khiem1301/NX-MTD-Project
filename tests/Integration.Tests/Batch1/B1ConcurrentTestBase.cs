using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>
/// Nền cho ca đồng thời (IT_B1_095, 151, 172): KHÔNG dùng ambient transaction. Dữ liệu S0 được COMMIT để các request song song
/// dùng connection độc lập; <see cref="DisposeAsync"/> xóa mọi thứ phát sinh (dòng có id lớn hơn mốc trước test + dữ liệu S0 mượn).
/// </summary>
public abstract class B1ConcurrentTestBase : IAsyncLifetime
{
    protected const string A = "/api/v1/admin/";
    protected const string M = "/api/v1/manager/";
    protected const string C = "/api/v1/common/";

    private ConcurrentTestFactory _factory = null!;
    private NxMtdDbContext _seedDb = null!;
    private readonly Dictionary<int, string> _tokens = new();
    private Marks _marks = null!;
    private List<string> _createdRoles = [];

    protected ITestOutputHelper Out { get; }
    protected HttpClient Client { get; private set; } = null!;
    protected S0Context S0 { get; private set; } = null!;
    protected FixedClock Clock => FixedClock.Default;

    protected B1ConcurrentTestBase(ITestOutputHelper output) => Out = output;

    private sealed record Marks(
        long Audit, int Employee, int Account, int Team, int GradeLevel,
        HashSet<string> ExistingGradeCodes, Dictionary<int, string> ExistingSnapshots);

    public async Task InitializeAsync()
    {
        _factory = new ConcurrentTestFactory();
        Client = _factory.CreateClient();
        _marks = await CaptureMarksAsync();

        _seedDb = _factory.NewContext();
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var seeder = new TestDataSeeder(_seedDb, hasher, FixedClock.Default);
        _createdRoles = await seeder.SeedRolesForConcurrentAsync();
        S0 = await seeder.SeedS0Async();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        try
        {
            await CleanupAsync();
        }
        finally
        {
            _seedDb?.Dispose();
            if (_factory is not null) await _factory.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------ dọn dẹp

    private async Task<Marks> CaptureMarksAsync()
    {
        await using var db = _factory.NewContext();
        var codes = await db.GradeLevelCodes.AsNoTracking().Select(g => g.GradeCode).ToListAsync();
        var snaps = await db.RjEmployeeSnapshots.AsNoTracking().Where(r => new[] { 1001, 2001, 2002 }.Contains(r.AttendanceCode))
            .ToDictionaryAsync(r => r.AttendanceCode, r => r.FullName);
        return new Marks(
            await db.AuditLogs.AsNoTracking().Select(a => (long?)a.AuditLogId).MaxAsync() ?? 0,
            await db.Employees.AsNoTracking().Select(e => (int?)e.EmployeeId).MaxAsync() ?? 0,
            await db.Accounts.AsNoTracking().Select(a => (int?)a.AccountId).MaxAsync() ?? 0,
            await db.Teams.AsNoTracking().Select(t => (int?)t.TeamId).MaxAsync() ?? 0,
            await db.GradeLevels.AsNoTracking().Select(g => (int?)g.GradeLevelId).MaxAsync() ?? 0,
            codes.ToHashSet(StringComparer.OrdinalIgnoreCase), snaps);
    }

    private async Task CleanupAsync()
    {
        await using var conn = new SqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        async Task Exec(string sql, params (string, object)[] ps)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec("DELETE FROM dbo.AuditLog WHERE AuditLogId > @m", ("@m", _marks.Audit));

        // Mọi bảng con có FK tới Employee / Account / Team và có dòng mới (id cha > mốc): xóa trước, nhiều lượt vì FK chéo.
        var children = new List<(string Table, string Col, string Parent)>();
        await using (var q = conn.CreateCommand())
        {
            q.CommandText = """
                SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id), pc.name, OBJECT_NAME(fk.referenced_object_id)
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                WHERE OBJECT_NAME(fk.referenced_object_id) IN ('Employee','Account','Team')
                  AND OBJECT_NAME(fk.parent_object_id) NOT IN ('Employee','Account','Team')
                """;
            await using var rd = await q.ExecuteReaderAsync();
            while (await rd.ReadAsync()) children.Add((rd.GetString(0), rd.GetString(1), rd.GetString(2)));
        }

        long MarkOf(string parent) => parent switch { "Employee" => _marks.Employee, "Account" => _marks.Account, _ => _marks.Team };
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var (table, col, parent) in children)
            {
                try { await Exec($"DELETE FROM {table} WHERE [{col}] > @m", ("@m", MarkOf(parent))); }
                catch (SqlException) { /* FK chéo giữa các bảng con: thử lại ở lượt sau */ }
            }
        }

        await Exec("DELETE FROM dbo.Account WHERE AccountId > @a OR EmployeeId > @e", ("@a", _marks.Account), ("@e", _marks.Employee));
        await Exec("DELETE FROM dbo.Employee WHERE EmployeeId > @e", ("@e", _marks.Employee));
        await Exec("DELETE FROM dbo.Team WHERE TeamId > @t", ("@t", _marks.Team));
        await Exec("DELETE FROM dbo.GradeLevel WHERE GradeLevelId > @g", ("@g", _marks.GradeLevel));

        foreach (var code in new[] { "B1", "B2", "B3", "B4", "B9" }.Where(c => !_marks.ExistingGradeCodes.Contains(c)))
            await Exec("DELETE FROM dbo.GradeLevelCode WHERE GradeCode = @c", ("@c", code));

        foreach (var code in new[] { 1001, 2001, 2002 })
        {
            if (_marks.ExistingSnapshots.TryGetValue(code, out var name))
                await Exec("UPDATE dbo.RjEmployeeSnapshot SET FullName = @n WHERE AttendanceCode = @c", ("@n", name), ("@c", code));
            else
                await Exec("DELETE FROM dbo.RjEmployeeSnapshot WHERE AttendanceCode = @c", ("@c", code));
        }

        foreach (var role in _createdRoles)
            await Exec("DELETE FROM dbo.Role WHERE RoleCode = @r", ("@r", role));
    }

    // ------------------------------------------------------------------ HTTP / DB helper

    protected NxMtdDbContext NewDb() => _factory.NewContext();

    protected async Task<Api> SendAsync(HttpMethod method, string url, string? token, object? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is string raw) req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null) req.Content = JsonContent.Create(body, body.GetType());

        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonElement root = default;
        var isJson = false;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { root = JsonDocument.Parse(text).RootElement.Clone(); isJson = true; }
            catch (JsonException) { }
        }

        return new Api { Status = res.StatusCode, Body = text, Root = root, IsJson = isJson, CacheControl = res.Headers.CacheControl?.ToString() ?? "" };
    }

    protected Task<Api> PostAsync(string url, string? token, object? body = null) => SendAsync(HttpMethod.Post, url, token, body);
    protected Task<Api> PutAsync(string url, string? token, object? body = null) => SendAsync(HttpMethod.Put, url, token, body);

    protected async Task<string> TokenAsync(Account account)
    {
        if (_tokens.TryGetValue(account.AccountId, out var cached)) return cached;
        var res = await SendAsync(HttpMethod.Post, C + "Auth/login", null, new { Username = account.Username, Password = S0Context.Password });
        res.Code.Should().Be(200, res.ToString());
        return _tokens[account.AccountId] = B1Json.Str(res.Data, "accessToken")!;
    }

    protected async Task<Employee> EmployeeAsync(int employeeId)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().Include(e => e.Role).SingleAsync(e => e.EmployeeId == employeeId);
    }

    protected async Task<long> LastAuditIdAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Select(a => (long?)a.AuditLogId).MaxAsync() ?? 0;
    }

    protected async Task<List<AuditLog>> AuditRowsAfterAsync(long lastId)
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Where(a => a.AuditLogId > lastId).OrderBy(a => a.AuditLogId).ToListAsync();
    }

    protected async Task<Dictionary<string, object?>> UpdateBodyAsync(int employeeId, string? fullName = null)
    {
        var e = await EmployeeAsync(employeeId);
        return new Dictionary<string, object?>
        {
            ["FullName"] = fullName ?? e.FullName, ["Gender"] = e.Gender, ["DateOfBirth"] = e.DateOfBirth, ["Phone"] = e.Phone,
            ["Role"] = NX_MTD.Domain.Constants.Roles.FromRoleCode(e.Role.RoleCode), ["TeamId"] = e.TeamId,
            ["PayType"] = e.PayType switch { "PIECE_RATE" => "PieceRate", "DAILY" => "Daily", _ => "Monthly" },
            ["StartDate"] = e.StartDate, ["AttendanceCode"] = e.AttendanceCode, ["RowVer"] = e.RowVer,
        };
    }
}
