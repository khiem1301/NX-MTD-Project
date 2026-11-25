using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>
/// Nền cho ca đồng thời / fault-injection của IT_B2: KHÔNG dùng ambient transaction. Dữ liệu S0 (Batch 1 + Batch 2) được COMMIT để các
/// request song song dùng connection độc lập; <see cref="DisposeAsync"/> xóa mọi dòng phát sinh (id lớn hơn mốc trước test) và khôi phục
/// các khóa UNIQUE của dữ liệu có sẵn đã bị đổi tạm.
/// </summary>
public abstract class B2ConcurrentTestBase : IAsyncLifetime
{
    protected const string A = "/api/v1/admin/";
    protected const string M = "/api/v1/manager/";
    protected const string C = "/api/v1/common/";
    protected const string AC = "/api/v1/accountant/";
    protected const string WK = "/api/v1/warehouse-keeper/";

    /// <summary>Bảng gốc (con trước, cha sau). PK lấy động từ sys.indexes; bảng con khác được dọn theo FK tới các bảng này.</summary>
    private static readonly string[] BaseRootTables =
    [
        "AuditLog", "StockBalance", "OrderMaterial", "OrderLine", "ProductMarker", "ProductMarkerFabric", "ProductComponentMaterial",
        "ProductComponent", "ProductVariant", "ProductColor", "ProductImage", "VehicleTrip", "PurchaseOrder", "CustomerOrder", "Product",
        "Material", "Supplier", "Customer", "Vehicle", "Account", "Employee", "Team", "GradeLevel",
    ];

    /// <summary>Batch sau khai báo thêm bảng gốc (con trước, cha sau) được đặt trước <see cref="BaseRootTables"/>.</summary>
    protected virtual string[] ExtraRootTables => [];

    /// <summary>Seed thêm dữ liệu nền riêng của batch (đã commit) sau S0 + B2.</summary>
    protected virtual Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb) => Task.CompletedTask;

    private string[] RootTables => [.. ExtraRootTables, .. BaseRootTables];

    private ConcurrentTestFactory _factory = null!;
    private NxMtdDbContext _seedDb = null!;
    private TestDataSeeder _seeder = null!;
    private readonly Dictionary<int, string> _tokens = new();
    private readonly Dictionary<string, long> _marks = new();
    private readonly Dictionary<string, string> _pk = new();
    private HashSet<string> _existingGradeCodes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, string> _existingSnapshots = new();
    private List<string> _createdRoles = [];

    protected ITestOutputHelper Out { get; }
    protected HttpClient Client { get; private set; } = null!;
    protected S0Context S0 { get; private set; } = null!;
    protected B2Context B { get; private set; } = null!;
    protected FixedClock Clock => FixedClock.Default;

    protected B2ConcurrentTestBase(ITestOutputHelper output) => Out = output;

    public async Task InitializeAsync()
    {
        _factory = new ConcurrentTestFactory();
        Client = _factory.CreateClient();
        try
        {
            await CaptureMarksAsync();
            _seedDb = _factory.NewContext();
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            _seeder = new TestDataSeeder(_seedDb, hasher, FixedClock.Default);
            _createdRoles = await _seeder.SeedRolesForConcurrentAsync();
            S0 = await _seeder.SeedS0Async();
            B = await _seeder.SeedB2Async(withStock: false);
            await OnSeededAsync(_seeder, _seedDb);
        }
        catch
        {
            // xUnit không gọi DisposeAsync khi InitializeAsync lỗi: dọn tại đây để không để rác trong Test DB.
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        try
        {
            if (_factory is not null) await CleanupAsync();
        }
        finally
        {
            _seedDb?.Dispose();
            if (_factory is not null) await _factory.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------ dọn dẹp

    private async Task CaptureMarksAsync()
    {
        await using var conn = new SqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        foreach (var table in RootTables)
        {
            await using var pkCmd = conn.CreateCommand();
            pkCmd.CommandText = """
                SELECT c.name FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.is_primary_key = 1 AND i.object_id = OBJECT_ID(@t)
                """;
            pkCmd.Parameters.AddWithValue("@t", "dbo." + table);
            var columns = new List<string>();
            await using (var rd = await pkCmd.ExecuteReaderAsync())
                while (await rd.ReadAsync()) columns.Add(rd.GetString(0));
            if (columns.Count != 1) throw new InvalidOperationException($"Bảng dbo.{table} phải có khóa chính 1 cột, thực tế: [{string.Join(",", columns)}].");

            _pk[table] = columns[0];
            await using var maxCmd = conn.CreateCommand();
            maxCmd.CommandText = $"SELECT ISNULL(MAX(CAST([{columns[0]}] AS bigint)), 0) FROM dbo.[{table}]";
            _marks[table] = (long)(await maxCmd.ExecuteScalarAsync())!;
        }

        await using var db = _factory.NewContext();
        _existingGradeCodes = (await db.GradeLevelCodes.AsNoTracking().Select(g => g.GradeCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _existingSnapshots = await db.RjEmployeeSnapshots.AsNoTracking().Where(r => new[] { 1001, 2001, 2002 }.Contains(r.AttendanceCode))
            .ToDictionaryAsync(r => r.AttendanceCode, r => r.FullName);
    }

    private async Task CleanupAsync()
    {
        await using var conn = new SqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await BeforeCleanupAsync(conn);
        try { await CleanupCoreAsync(conn); }
        finally { await AfterCleanupAsync(conn); }
    }

    /// <summary>Batch sau có thể tạm tắt trigger chặn DELETE trước khi dọn (và bật lại ở <see cref="AfterCleanupAsync"/>).</summary>
    protected virtual Task BeforeCleanupAsync(SqlConnection conn) => Task.CompletedTask;

    protected virtual Task AfterCleanupAsync(SqlConnection conn) => Task.CompletedTask;

    private async Task CleanupCoreAsync(SqlConnection conn)
    {
        async Task<bool> TryExec(string sql, params (string, object)[] ps)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = 60;
                foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

        // Bảng con có FK tới các bảng gốc nhưng không nằm trong RootTables (vd. ProductMarkerRatio, ProductMarkerComponent, StockTransaction, RefreshToken).
        var children = new List<(string Table, string Col, string Parent)>();
        await using (var q = conn.CreateCommand())
        {
            var list = string.Join(",", RootTables.Select(t => $"'{t}'"));
            q.CommandText = $"""
                SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id), pc.name, OBJECT_NAME(fk.referenced_object_id)
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                WHERE OBJECT_NAME(fk.referenced_object_id) IN ({list}) AND OBJECT_NAME(fk.parent_object_id) NOT IN ({list})
                """;
            await using var rd = await q.ExecuteReaderAsync();
            while (await rd.ReadAsync()) children.Add((rd.GetString(0), rd.GetString(1), rd.GetString(2)));
        }

        var failures = new List<string>();
        for (var pass = 0; pass < 8; pass++)
        {
            failures.Clear();
            foreach (var (table, col, parent) in children)
                // Chỉ xóa khi thực sự có dòng mới: bảng sổ chỉ-thêm (StockTransaction) có trigger chặn mọi DELETE kể cả khi không có dòng nào.
                if (!await TryExec($"IF EXISTS (SELECT 1 FROM {table} WHERE [{col}] > @m) DELETE FROM {table} WHERE [{col}] > @m", ("@m", _marks[parent])))
                    failures.Add($"{table}.{col}");
            foreach (var table in RootTables)
                if (!await TryExec($"IF EXISTS (SELECT 1 FROM dbo.[{table}] WHERE [{_pk[table]}] > @m) DELETE FROM dbo.[{table}] WHERE [{_pk[table]}] > @m", ("@m", _marks[table])))
                    failures.Add(table);
            if (failures.Count == 0) break;
        }

        // Khôi phục khóa UNIQUE của dữ liệu có sẵn đã đổi tạm khi seed S0.
        if (_seeder is not null)
            foreach (var (table, idColumn, id, column, original) in _seeder.MovedKeys)
                await TryExec($"UPDATE dbo.[{table}] SET [{column}] = @v WHERE [{idColumn}] = @id", ("@v", (object?)original ?? DBNull.Value), ("@id", id));

        foreach (var code in new[] { "B1", "B2", "B3", "B4", "B9" }.Where(c => !_existingGradeCodes.Contains(c)))
            await TryExec("DELETE FROM dbo.GradeLevelCode WHERE GradeCode = @c", ("@c", code));
        foreach (var code in new[] { 1001, 2001, 2002 })
        {
            if (_existingSnapshots.TryGetValue(code, out var name))
                await TryExec("UPDATE dbo.RjEmployeeSnapshot SET FullName = @n WHERE AttendanceCode = @c", ("@n", name), ("@c", code));
            else
                await TryExec("DELETE FROM dbo.RjEmployeeSnapshot WHERE AttendanceCode = @c", ("@c", code));
        }

        foreach (var role in _createdRoles) await TryExec("DELETE FROM dbo.Role WHERE RoleCode = @r", ("@r", role));
        failures.Should().BeEmpty("dọn dẹp Test DB phải sạch: " + string.Join(", ", failures));
    }

    // ------------------------------------------------------------------ HTTP / DB helper

    protected NxMtdDbContext NewDb() => _factory.NewContext();

    protected string ConnectionString => _factory.ConnectionString;

    protected async Task<Api> SendAsync(HttpMethod method, string url, string? token, object? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is string raw) req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null) req.Content = JsonContent.Create(body, body.GetType());
        return await ReadAsync(await Client.SendAsync(req));
    }

    protected async Task<Api> UploadAsync(string url, string token, byte[] content, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await ReadAsync(await Client.SendAsync(req));
    }

    private static async Task<Api> ReadAsync(HttpResponseMessage res)
    {
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync();
            JsonElement root = default;
            var isJson = false;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { root = JsonDocument.Parse(text).RootElement.Clone(); isJson = true; }
                catch (JsonException) { /* body không phải JSON */ }
            }

            return new Api { Status = res.StatusCode, Body = text, Root = root, IsJson = isJson, CacheControl = res.Headers.CacheControl?.ToString() ?? "" };
        }
    }

    protected Task<Api> GetAsync(string url, string? token) => SendAsync(HttpMethod.Get, url, token, null);
    protected Task<Api> PostAsync(string url, string? token, object? body = null) => SendAsync(HttpMethod.Post, url, token, body);
    protected Task<Api> PutAsync(string url, string? token, object? body = null) => SendAsync(HttpMethod.Put, url, token, body);

    protected async Task<string> TokenAsync(Account account)
    {
        if (_tokens.TryGetValue(account.AccountId, out var cached)) return cached;
        var res = await SendAsync(HttpMethod.Post, C + "Auth/login", null, new { account.Username, Password = S0Context.Password });
        res.Code.Should().Be(200, res.ToString());
        return _tokens[account.AccountId] = B1Json.Str(res.Data, "accessToken")!;
    }

    protected Task<string> TkAdm() => TokenAsync(S0.Adm);
    protected Task<string> TkAcc() => TokenAsync(S0.Acc);
    protected Task<string> TkWk() => TokenAsync(S0.Wh);

    protected async Task<long> LastAuditIdAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Select(a => (long?)a.AuditLogId).MaxAsync() ?? 0;
    }

    /// <summary>Chạy hai request cùng lúc: cả hai được tạo sẵn rồi mới thả cùng một tín hiệu bắt đầu để tăng khả năng đua nhau.</summary>
    protected static async Task<Api[]> RaceAsync(params Func<Task<Api>>[] calls)
    {
        var gate = new TaskCompletionSource();
        var tasks = calls.Select(call => Task.Run(async () =>
        {
            await gate.Task;
            return await call();
        })).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    protected static string Summary(IEnumerable<Api> results) => string.Join(" | ", results.Select(r => r.ToString()));

    protected static bool IsSuccess(Api r) => r.Code is 200 or 201;

    protected static void AssertRaceNoServerError(Api[] results)
    {
        results.Should().OnlyContain(r => r.Code < 500, Summary(results));
        foreach (var r in results)
            r.Body.Should().NotContainAny("UQ_", "SqlException", "Cannot insert duplicate", "deadlock", "Deadlock", "StackTrace");
    }

    protected static object CustomerBody(string name, string phone, string email) =>
        new { CustomerName = name, Phone = phone, Email = email, DefaultAddress = "Địa chỉ IT B2" };

    /// <summary>Ngẫu nhiên theo lần chạy để không trùng với dữ liệu còn sót trong Test DB.</summary>
    protected static string NextPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);
    protected static string NextEmail() => $"race{Guid.NewGuid():N}"[..20] + "@it-b2.vn";

    private static readonly byte[] TinyJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

    /// <summary>Upload một ảnh hợp lệ (kích thước khác nhau ⇒ khóa khác nhau) qua P7, trả về khóa ảnh.</summary>
    protected async Task<string> UploadKeyAsync(string token, int size)
    {
        var rest = TinyJpeg[2..];
        var padding = size - 2 - rest.Length;
        using var ms = new MemoryStream();
        ms.Write(TinyJpeg, 0, 2);
        while (padding >= 4)
        {
            var len = Math.Min(padding - 2, 65_535);
            ms.WriteByte(0xFF); ms.WriteByte(0xFE);
            ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)(len & 0xFF));
            ms.Write(new byte[len - 2]);
            padding -= 2 + len;
        }

        ms.Write(rest);
        if (padding > 0) ms.Write(new byte[padding]);
        var res = await UploadAsync(A + "products/sample-image", token, ms.ToArray(), "ảnh.jpg", "image/jpeg");
        res.Code.Should().Be(200, $"seed: upload ảnh mẫu phải thành công. {res}");
        var key = res.Data.ValueKind == JsonValueKind.String ? res.Data.GetString() : B1Json.Str(res.Data, "key") ?? B1Json.Str(res.Data, "imageKey");
        key.Should().NotBeNullOrWhiteSpace();
        return key!;
    }

    protected async Task<Product?> ProductByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductCode == code);
    }
}
