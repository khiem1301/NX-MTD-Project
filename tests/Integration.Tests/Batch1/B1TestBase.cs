using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>Kết quả một lời gọi HTTP: status, body thô và envelope JSON (success/data/message hoặc errorCode).</summary>
public sealed class Api
{
    public HttpStatusCode Status { get; init; }
    public int Code => (int)Status;
    public string Body { get; init; } = "";
    public JsonElement Root { get; init; }
    public bool IsJson { get; init; }
    public string CacheControl { get; init; } = "";

    public JsonElement Data => B1Json.Prop(Root, "data") ?? default;
    public string? Message => B1Json.Prop(Root, "message") is { ValueKind: JsonValueKind.String } m ? m.GetString() : null;
    public string? ErrorCode => B1Json.Prop(Root, "errorCode") is { ValueKind: JsonValueKind.String } m ? m.GetString() : null;

    /// <summary>Danh sách phần tử của data (data là mảng, hoặc data.items).</summary>
    public List<JsonElement> Items()
    {
        var d = Data;
        if (d.ValueKind == JsonValueKind.Array) return d.EnumerateArray().ToList();
        if (d.ValueKind == JsonValueKind.Object && B1Json.Prop(d, "items") is { ValueKind: JsonValueKind.Array } arr)
            return arr.EnumerateArray().ToList();
        return [];
    }

    public void ShouldNotBeServerError() =>
        Code.Should().BeLessThan(500, $"mọi lỗi phải xử lý êm, không được 5xx. Body: {Body}");

    public override string ToString() => $"{Code} {Body}";
}

public static class B1Json
{
    public static JsonElement? Prop(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    public static int? Int(JsonElement el, string name) =>
        Prop(el, name) is { ValueKind: JsonValueKind.Number } v ? v.GetInt32() : null;

    public static string? Str(JsonElement el, string name) =>
        Prop(el, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static bool? Bool(JsonElement el, string name) =>
        Prop(el, name) is { ValueKind: JsonValueKind.True or JsonValueKind.False } v ? v.GetBoolean() : null;

    public static decimal? Dec(JsonElement el, string name) =>
        Prop(el, name) is { ValueKind: JsonValueKind.Number } v ? v.GetDecimal() : null;
}

/// <summary>
/// Nền chung cho IT_B1: seed S0 sau dữ liệu tối thiểu, helper gọi API có token, snapshot DB để assert "DB không đổi".
/// </summary>
public abstract class B1TestBase : BaseIntegrationTest
{
    protected const string A = "/api/v1/admin/";
    protected const string M = "/api/v1/manager/";
    protected const string C = "/api/v1/common/";

    protected static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<int, string> _tokens = new();
    protected ITestOutputHelper Out { get; }
    protected S0Context S0 { get; private set; } = null!;
    protected override S0Context? S0Source => S0;

    protected B1TestBase(ITestOutputHelper output) => Out = output;

    protected override Task<TestContext> SeedBaseAsync(TestDataSeeder seeder) => seeder.SeedBatch1BaseAsync();

    protected override async Task OnSeededAsync(TestDataSeeder seeder) => S0 = await seeder.SeedS0Async();

    /// <summary>Ghi lại hành vi thực tế của các ca đánh dấu [?].</summary>
    protected void Note(string text) => Out.WriteLine("[?] " + text);

    // ------------------------------------------------------------------ HTTP

    protected Task<Api> GetAsync(string url, string? token = null) => SendAsync(HttpMethod.Get, url, token, null);
    protected Task<Api> PostAsync(string url, string? token = null, object? body = null) => SendAsync(HttpMethod.Post, url, token, body);
    protected Task<Api> PutAsync(string url, string? token = null, object? body = null) => SendAsync(HttpMethod.Put, url, token, body);

    protected async Task<Api> SendAsync(HttpMethod method, string url, string? token, object? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is string raw)
            req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null)
            req.Content = JsonContent.Create(body, body.GetType());

        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();

        JsonElement root = default;
        var isJson = false;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { root = JsonDocument.Parse(text).RootElement.Clone(); isJson = true; }
            catch (JsonException) { /* body không phải JSON (vd. 401 rỗng) */ }
        }

        return new Api
        {
            Status = res.StatusCode,
            Body = text,
            Root = root,
            IsJson = isJson,
            CacheControl = res.Headers.CacheControl?.ToString() ?? "",
        };
    }

    protected Task<Api> LoginAsync(string username, string password) =>
        PostAsync(C + "Auth/login", null, new { Username = username, Password = password });

    /// <summary>TK_&lt;alias&gt;: access token lấy bằng login thật.</summary>
    protected async Task<string> TokenAsync(Account account)
    {
        if (_tokens.TryGetValue(account.AccountId, out var cached)) return cached;
        var res = await LoginAsync(account.Username, S0Context.Password);
        res.Code.Should().Be(200, $"seed S0: login {account.Username} phải thành công. {res}");
        var token = B1Json.Str(res.Data, "accessToken")!;
        _tokens[account.AccountId] = token;
        return token;
    }

    // ------------------------------------------------------------------ JWT giả

    private static (string Key, string Issuer, string Audience) JwtSettings()
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Integration.json"), optional: false)
            .Build();
        return (cfg["Jwt:Key"]!, cfg["Jwt:Issuer"]!, cfg["Jwt:Audience"]!);
    }

    private async Task<string> RoleOfAsync(Account account)
    {
        await using var db = NewDb();
        var code = await db.Employees.AsNoTracking()
            .Where(e => e.EmployeeId == account.EmployeeId).Select(e => e.Role.RoleCode).SingleAsync();
        return Roles.FromRoleCode(code);
    }

    /// <summary>JWT ký đúng khóa của hệ thống, claim giống TokenService; dùng để dựng token hết hạn / lệch hạn.</summary>
    protected async Task<string> CraftJwtAsync(Account account, string tokenType, DateTime notBeforeUtc, DateTime expiresUtc)
    {
        var (key, issuer, audience) = JwtSettings();
        var claims = new List<Claim> { new("sub", account.AccountId.ToString()), new("stamp", account.SecurityStamp.ToString()), new("tokenType", tokenType) };
        if (tokenType == "access")
        {
            claims.Add(new Claim("employeeId", account.EmployeeId.ToString()));
            claims.Add(new Claim("username", account.Username));
            claims.Add(new Claim("fullName", account.Employee.FullName));
            claims.Add(new Claim("role", await RoleOfAsync(account)));
        }

        var token = new JwtSecurityToken(issuer, audience, claims, notBeforeUtc, expiresUtc,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>TK_FORGED: lấy token thật rồi sửa claim role trong payload, giữ nguyên header + chữ ký cũ ⇒ sai chữ ký.</summary>
    protected static string ForgeRole(string validToken, string newRole)
    {
        var parts = validToken.Split('.');
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlEncoder.Decode(parts[1]))!;
        var edited = payload.ToDictionary(k => k.Key, k => (object)k.Value);
        edited["role"] = newRole;
        parts[1] = Base64UrlEncoder.Encode(JsonSerializer.Serialize(edited));
        return string.Join('.', parts);
    }

    // ------------------------------------------------------------------ DB

    protected NxMtdDbContext NewDb() => CreateDbContext();

    /// <summary>Ảnh chụp mọi cột của Employee, Account, Team, GradeLevel, GradeLevelCode + id AuditLogs (gồm cả RowVer).</summary>
    protected async Task<string> SnapshotAsync()
    {
        await using var db = NewDb();
        var employees = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeId).Select(e => new
        {
            e.EmployeeId, e.EmployeeCode, e.FullName, e.Gender, e.DateOfBirth, e.Phone, e.AvatarUrl, e.RoleId, e.TeamId,
            e.PayType, e.CurrentGradeCode, e.AttendanceCode, e.StartDate, e.ResignDate, e.Status, e.UpdatedAt, e.RowVer,
        }).ToListAsync();
        var accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.AccountId).Select(a => new
        {
            a.AccountId, a.EmployeeId, a.Username, a.PasswordHash, a.IsTemporaryPassword, a.SecurityStamp, a.Status, a.LockedAt, a.LockReason,
        }).ToListAsync();
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.TeamId).Select(t => new { t.TeamId, t.TeamCode, t.TeamType, t.Status }).ToListAsync();
        var levels = await db.GradeLevels.AsNoTracking().OrderBy(g => g.GradeLevelId)
            .Select(g => new { g.GradeLevelId, g.GradeCode, g.MonthlyAmount, g.EffectiveDate }).ToListAsync();
        var codes = await db.GradeLevelCodes.AsNoTracking().OrderBy(g => g.GradeCode).Select(g => new { g.GradeCode, g.Status }).ToListAsync();
        var audits = await db.AuditLogs.AsNoTracking().OrderBy(a => a.AuditLogId).Select(a => a.AuditLogId).ToListAsync();
        return JsonSerializer.Serialize(new { employees, accounts, teams, levels, codes, audits });
    }

    protected async Task AssertDbUnchangedAsync(string before, string because = "")
    {
        var after = await SnapshotAsync();
        (after == before).Should().BeTrue($"[DB] không được thay đổi. {because}");
    }

    protected async Task<Employee> EmployeeAsync(int employeeId)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().Include(e => e.Role).SingleAsync(e => e.EmployeeId == employeeId);
    }

    protected async Task<Account> AccountAsync(int accountId)
    {
        await using var db = NewDb();
        return await db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == accountId);
    }

    protected async Task<int> AuditCountAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().CountAsync();
    }

    protected async Task<List<AuditLog>> AuditRowsAfterAsync(long lastKnownId)
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Where(a => a.AuditLogId > lastKnownId).OrderBy(a => a.AuditLogId).ToListAsync();
    }

    protected async Task<long> LastAuditIdAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Select(a => (long?)a.AuditLogId).MaxAsync() ?? 0;
    }

    /// <summary>Id chắc chắn không tồn tại, tính động từ dữ liệu (không hardcode).</summary>
    protected async Task<int> NonExistingEmployeeIdAsync()
    {
        await using var db = NewDb();
        return (await db.Employees.AsNoTracking().MaxAsync(e => e.EmployeeId)) + 1_000_000;
    }

    // ------------------------------------------------------------------ helper dùng cho IT_B1_071+

    protected Task<Api> DeleteAsync(string url, string? token = null) => SendAsync(HttpMethod.Delete, url, token, null);

    /// <summary>Mục 8: mọi lỗi phải êm — không 5xx, không lộ lỗi SQL / trigger trong body.</summary>
    protected static void AssertGraceful(Api r)
    {
        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny("RAISERROR", "THROW", "trigger", "Trigger", "SqlException");
    }

    /// <summary>Bất biến mục 8: mỗi tổ có tối đa 1 TEAM_LEADER và 1 TEAM_DEPUTY.</summary>
    protected async Task AssertLeadershipInvariantAsync()
    {
        await using var db = NewDb();
        var groups = await db.Employees.AsNoTracking()
            .Where(e => e.TeamId != null && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .GroupBy(e => new { e.TeamId, e.Role.RoleCode }).Select(g => new { g.Key.TeamId, g.Key.RoleCode, N = g.Count() }).ToListAsync();
        groups.Where(g => g.N > 1).Should().BeEmpty("mỗi tổ tối đa 1 Leader và 1 Deputy");
    }

    protected async Task<string> RoleCodeOfAsync(int employeeId) => (await EmployeeAsync(employeeId)).Role.RoleCode;

    protected async Task<int> CountRoleInTeamAsync(int teamId, string roleCode)
    {
        await using var db = NewDb();
        return await db.Employees.AsNoTracking().CountAsync(e => e.TeamId == teamId && e.Role.RoleCode == roleCode);
    }

    protected async Task<byte[]> RowVerAsync(int employeeId) => (await EmployeeAsync(employeeId)).RowVer;

    protected static string PayTypeName(string dbPayType) => dbPayType switch
    {
        PayTypes.PieceRate => "PieceRate",
        PayTypes.Daily => "Daily",
        _ => "Monthly",
    };

    /// <summary>Body PUT Employees/{id} lấy từ dòng hiện tại trong DB, ghi đè các trường truyền vào.</summary>
    protected async Task<Dictionary<string, object?>> UpdateBodyAsync(int employeeId, string? fullName = null, string? phone = null,
        string? role = null, int? teamId = null, bool keepTeam = true, int? attendanceCode = null, bool keepAttendance = true,
        byte[]? rowVer = null)
    {
        var e = await EmployeeAsync(employeeId);
        return new Dictionary<string, object?>
        {
            ["FullName"] = fullName ?? e.FullName,
            ["Gender"] = e.Gender,
            ["DateOfBirth"] = e.DateOfBirth,
            ["Phone"] = phone ?? e.Phone,
            ["Role"] = role ?? Roles.FromRoleCode(e.Role.RoleCode),
            ["TeamId"] = keepTeam ? e.TeamId : teamId,
            ["PayType"] = PayTypeName(e.PayType),
            ["StartDate"] = e.StartDate,
            ["AttendanceCode"] = keepAttendance ? e.AttendanceCode : attendanceCode,
            ["RowVer"] = rowVer ?? e.RowVer,
        };
    }

    protected async Task<Team> TeamAsync(int teamId)
    {
        await using var db = NewDb();
        return await db.Teams.AsNoTracking().SingleAsync(t => t.TeamId == teamId);
    }

    protected async Task<int> NonExistingTeamIdAsync()
    {
        await using var db = NewDb();
        return (await db.Teams.AsNoTracking().MaxAsync(t => t.TeamId)) + 1_000_000;
    }

    protected static HashSet<int> IdSet(Api r, string name) =>
        r.Items().Select(i => B1Json.Int(i, name)).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();

    protected static HashSet<string> StrSet(Api r, string name) =>
        r.Items().Select(i => B1Json.Str(i, name)).Where(v => v is not null).Select(v => v!).ToHashSet();

    /// <summary>Spec ghi "201" cho tạo mới; ToResponse của hệ thống trả 200 (đã thống nhất ở lượt 1: chấp nhận 200/201).</summary>
    protected static void ShouldBeCreated(Api r) => r.Code.Should().BeOneOf(new[] { 200, 201 }, r.ToString());

    protected async Task AssertSingleAuditAsync(long lastId)
    {
        var rows = await AuditRowsAfterAsync(lastId);
        rows.Should().HaveCount(1, "AuditLogs +1");
        Note($"AuditLog: Table={rows[0].TableName}, Action={rows[0].Action}, RecordId={rows[0].RecordId}");
    }

    protected static DateOnly PastDay(DateOnly today) => today.AddDays(-1);

    protected static DateTime VnNow() => DateTime.UtcNow.AddHours(7);

    /// <summary>Chuỗi tiếng Việt có dấu đúng <paramref name="length"/> ký tự, không có khoảng trắng ở hai đầu (service Trim).</summary>
    protected static string LongVietnamese(int length)
    {
        var s = string.Concat(Enumerable.Repeat("Vi phạm nội quy ", length / 16 + 1))[..length].TrimEnd();
        return s.PadRight(length, 'ạ');
    }

    /// <summary>"AuditLogs +1" gắn với đối tượng Employee bị tác động: đúng 1 dòng Employee#id (log phía Team ghi nhận riêng).</summary>
    protected async Task AssertSingleEmployeeAuditAsync(long lastId, int employeeId)
    {
        var rows = await AuditRowsAfterAsync(lastId);
        var mine = rows.Where(a => a.TableName == nameof(Employee) && a.RecordId == employeeId).ToList();
        mine.Should().HaveCount(1, "AuditLogs +1 cho Employee bị tác động");
        Note("Audit rows: " + string.Join("; ", rows.Select(a => $"{a.TableName}#{a.RecordId} {a.Action}")));
    }

    protected static DateOnly FutureDay(DateOnly clockToday)
    {
        var real = DateOnly.FromDateTime(DateTime.Today);
        return (clockToday > real ? clockToday : real).AddDays(1);
    }

    /// <summary>Tên người có dấu, đúng <paramref name="length"/> ký tự, chỉ chữ cái và dấu cách đơn (qua PersonNameRule).</summary>
    protected static string VietnameseName(int length)
    {
        var sb = new StringBuilder();
        while (sb.Length < length + 6) sb.Append("Việt ");
        var s = sb.ToString(0, length).TrimEnd();
        while (s.Length < length) s += 'ệ';
        return s;
    }
}
