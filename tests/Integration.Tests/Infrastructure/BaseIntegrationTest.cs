using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Domain.Entities;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;
using Integration.Tests.Seed;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// Mỗi test case: mở ambient transaction → seed Minimum Test Data → chạy → Rollback.
/// Không test nào phụ thuộc dữ liệu sót lại từ test trước.
/// </summary>
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected IntegrationTestFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    protected NxMtdDbContext Db { get; private set; } = null!;
    protected TestContext Ctx { get; private set; } = null!;
    protected FixedClock Clock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await InitializeCoreAsync();
        }
        catch
        {
            // xUnit không gọi DisposeAsync khi InitializeAsync ném lỗi: phải Rollback tại đây,
            // nếu không ambient transaction treo và chặn khóa của mọi test sau.
            await DisposeAsync();
            throw;
        }
    }

    private async Task InitializeCoreAsync()
    {
        Factory = new IntegrationTestFactory();
        Clock = Factory.Clock;

        // Mở connection + ambient transaction TRƯỚC khi host/HTTP client tạo DbContext.
        Factory.EnsureConnection();
        await Factory.BeginAmbientTransactionAsync();

        Client = Factory.CreateClient();

        Scope = Factory.Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
        var hasher = Scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var seeder = new TestDataSeeder(Db, hasher, Clock);
        Ctx = await SeedBaseAsync(seeder);
        await OnSeededAsync(seeder);
    }

    /// <summary>Mặc định seed toàn bộ Minimum Test Data; lớp con có thể chỉ seed phần nó cần.</summary>
    protected virtual Task<TestContext> SeedBaseAsync(TestDataSeeder seeder) => seeder.SeedAsync();

    /// <summary>Hook cho lớp con seed thêm dữ liệu nền riêng (vd. S0 của IT_B1) trong cùng ambient transaction.</summary>
    protected virtual Task OnSeededAsync(TestDataSeeder seeder) => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Scope?.Dispose();
        Client?.Dispose();

        if (Factory is not null)
        {
            await Factory.RollbackAmbientTransactionAsync();
            await Factory.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------ helper dùng chung cho các Batch

    /// <summary>Lớp con có seed S0 thì trả về S0Context để <see cref="SendAsAsync"/> tự lấy token.</summary>
    protected virtual S0Context? S0Source => null;

    private readonly Dictionary<string, string> _roleTokens = new();

    private Account ResolveAccount(string key)
    {
        var s0 = S0Source ?? throw new InvalidOperationException("Lớp test này chưa seed S0: không có token để gửi.");
        return key.ToLowerInvariant() switch
        {
            "admin" => s0.Adm, "manager" => s0.Mgr, "accountant" => s0.Acc,
            "warehouse" or "warehousekeeper" => s0.Wh, "teamleader" => s0.Tl1, "teamdeputy" => s0.Td1,
            "worker" => s0.W1, "qaqc" => s0.Tmp, "locked" => s0.Lck, "resigned" => s0.Rs,
            _ => s0.AllAccounts.FirstOrDefault(a => a.Username.Equals(key, StringComparison.OrdinalIgnoreCase))
                 ?? throw new ArgumentException($"Không có tài khoản S0 ứng với '{key}'"),
        };
    }

    /// <summary>
    /// Gửi request bằng token (login thật, có cache) của vai trò hoặc username S0; null / "anonymous" = không gửi token.
    /// Body là string thì gửi nguyên văn dạng JSON. Trả về (response, JSON root nếu body là JSON).
    /// </summary>
    protected async Task<(HttpResponseMessage Response, JsonElement? Json)> SendAsAsync(
        string? roleOrUserKey, HttpMethod method, string url, object? body = null)
    {
        string? token = null;
        if (!string.IsNullOrEmpty(roleOrUserKey) && !roleOrUserKey.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
        {
            var account = ResolveAccount(roleOrUserKey);
            if (!_roleTokens.TryGetValue(account.Username, out token))
            {
                var login = await Client.PostAsJsonAsync("/api/v1/common/Auth/login",
                    new { account.Username, Password = S0Context.Password });
                login.IsSuccessStatusCode.Should().BeTrue($"seed S0: login {account.Username} phải thành công");
                using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
                _roleTokens[account.Username] = token;
            }
        }

        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is string raw) req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null) req.Content = JsonContent.Create(body, body.GetType());

        var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonElement? json = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { json = JsonDocument.Parse(text).RootElement.Clone(); }
            catch (JsonException) { /* body không phải JSON (vd. 401 rỗng) */ }
        }
        return (res, json);
    }

    /// <summary>Kiểm mã HTTP, không 5xx, và body không chứa chuỗi nhạy cảm (RBAC/IDOR: tên người khác, SQL, constraint...).</summary>
    protected static async Task AssertErrorAsync(HttpResponseMessage res, int expectedStatus, string? forbiddenLeakText = null)
    {
        var body = await res.Content.ReadAsStringAsync();
        ((int)res.StatusCode).Should().Be(expectedStatus, $"body: {body}");
        ((int)res.StatusCode).Should().BeLessThan(500, "lỗi phải được xử lý êm, không 5xx");
        if (!string.IsNullOrEmpty(forbiddenLeakText))
            body.Should().NotContainEquivalentOf(forbiddenLeakText, "body lỗi không được lộ dữ liệu nhạy cảm");
    }

    private static async Task<string> RowVersHexAsync<T>(IQueryable<T> set) where T : class
    {
        var vers = await set.AsNoTracking().Select(x => EF.Property<byte[]>(x, "RowVer")).ToListAsync();
        return string.Join(",", vers.Select(Convert.ToHexString).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>Ảnh chụp COUNT các bảng chính + RowVer của các bảng có cột RowVer; so sánh bằng <see cref="DbSnapshot.AssertUnchangedAsync"/>.</summary>
    protected async Task<DbSnapshot> CaptureDbSnapshotAsync()
    {
        var map = new SortedDictionary<string, string>();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
            map["Accounts"] = (await db.Accounts.CountAsync()).ToString();
            map["Teams"] = (await db.Teams.CountAsync()).ToString();
            map["Customers"] = (await db.Customers.CountAsync()).ToString();
            map["Suppliers"] = (await db.Suppliers.CountAsync()).ToString();
            map["Materials"] = (await db.Materials.CountAsync()).ToString();
            map["Products"] = (await db.Products.CountAsync()).ToString();
            map["Vehicles"] = (await db.Vehicles.CountAsync()).ToString();
            map["AuditLogs"] = (await db.AuditLogs.CountAsync()).ToString();
        map["Employees"] = await RowVersHexAsync(db.Employees);
        map["CustomerOrders"] = await RowVersHexAsync(db.CustomerOrders);
        map["PurchaseOrders"] = await RowVersHexAsync(db.PurchaseOrders);
        map["MaterialReceipts"] = await RowVersHexAsync(db.MaterialReceipts);
        map["MaterialIssues"] = await RowVersHexAsync(db.MaterialIssues);
        map["DispatchNotes"] = await RowVersHexAsync(db.DispatchNotes);
        map["QcBatches"] = await RowVersHexAsync(db.QcBatches);
        map["DefectRecords"] = await RowVersHexAsync(db.DefectRecords);
        map["OutputRecords"] = await RowVersHexAsync(db.OutputRecords);
        map["Payrolls"] = await RowVersHexAsync(db.Payrolls);
        map["Requests"] = await RowVersHexAsync(db.Requests);
        }
        return new DbSnapshot(map, CaptureDbSnapshotAsync);
    }

    /// <summary>DbContext mới gắn cùng ambient transaction — dùng để đọc lại DB sau khi gọi API.</summary>
    protected NxMtdDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
    }
}

/// <summary>Ảnh chụp nhanh DB (COUNT + RowVer). <c>await snap.AssertUnchangedAsync()</c> = "DB không đổi".</summary>
public sealed class DbSnapshot(IReadOnlyDictionary<string, string> values, Func<Task<DbSnapshot>> recapture)
{
    public IReadOnlyDictionary<string, string> Values { get; } = values;

    public async Task AssertUnchangedAsync(string because = "")
    {
        var after = await recapture();
        foreach (var (table, before) in Values)
            after.Values[table].Should().Be(before, $"[DB] {table} không được thay đổi. {because}");
    }
}
