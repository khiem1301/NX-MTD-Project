using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace System.Tests.NFR;

/// <summary>Chỉ số đo của một ca NFR. Tên trường JSON được export_it_report.py đọc, không đổi.</summary>
public sealed record NfrMetric(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("vu")] int Vu,
    [property: JsonPropertyName("duration_sec")] double DurationSec,
    [property: JsonPropertyName("rps")] double Rps,
    [property: JsonPropertyName("p50_ms")] double P50Ms,
    [property: JsonPropertyName("p95_ms")] double P95Ms,
    [property: JsonPropertyName("max_ms")] double MaxMs,
    [property: JsonPropertyName("ok_count")] int OkCount,
    [property: JsonPropertyName("fail_count")] int FailCount,
    [property: JsonPropertyName("extra_note")] string ExtraNote)
{
    /// <summary>Tỷ lệ lỗi 0..1. Không có request nào thì coi là lỗi toàn bộ để Assert không Passed rỗng.</summary>
    [JsonIgnore]
    public double ErrorRate => OkCount + FailCount == 0 ? 1 : (double)FailCount / (OkCount + FailCount);
}

/// <summary>Kết quả một request tự đo (dùng cho ca Task.WhenAll).</summary>
public readonly record struct NfrSample(double LatencyMs, bool Ok, int StatusCode);

/// <summary>Cấu hình dùng chung với BaseSystemTest, chốt chặn môi trường và ghi file chỉ số.</summary>
public static class Nfr
{
    public const string DataPrefix = "NFR_AUTO_";
    private static readonly Regex CaseIdRe = new(@"^NFR_G\d+_\d{3}$");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string BaseUrl => BaseSystemTest.BaseUrl;

    public static string ResultsDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "docs"))) dir = dir.Parent;
            return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "tests", "docs", "ST_RESULTS");
        }
    }

    public static string MetricPath(string caseId)
    {
        if (!CaseIdRe.IsMatch(caseId)) throw new ArgumentException($"Mã ca NFR không hợp lệ: '{caseId}' (cần NFR_G<N>_xxx).");
        return Path.Combine(ResultsDir, "nfr_metrics", $"{caseId}.json");
    }

    /// <summary>
    /// NFR gồm tải và thử bảo mật nên mặc định chỉ chạy trên máy local (ST_NFR_TARGETS.md mục 6).
    /// Máy test riêng: đặt NFR_ALLOWED_HOST đúng bằng host của ST_BASE_URL. Không dùng cho môi trường đang triển khai.
    /// </summary>
    public static void EnsureSafeTarget()
    {
        var uri = new Uri(BaseUrl);
        var allowed = Environment.GetEnvironmentVariable("NFR_ALLOWED_HOST");
        if (uri.IsLoopback || string.Equals(uri.Host, allowed, StringComparison.OrdinalIgnoreCase)) return;
        throw new InvalidOperationException(
            $"Từ chối chạy NFR lên '{uri.Host}': không phải localhost. Đặt ST_BASE_URL về localhost, " +
            "hoặc đặt NFR_ALLOWED_HOST nếu đây là máy test riêng.");
    }

    /// <summary>Xóa chỉ số của lần chạy trước để ca lỗi giữa chừng không mang số cũ vào báo cáo.</summary>
    public static void ResetMetric(string caseId)
    {
        var path = MetricPath(caseId);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Ghi chỉ số. Gọi TRƯỚC các Assert ngưỡng để ca Failed vẫn có số thực đo.</summary>
    public static NfrMetric WriteMetric(NfrMetric metric)
    {
        var path = MetricPath(metric.CaseId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(metric, JsonOpts));
        return metric;
    }

    /// <summary>Tổng hợp các request tự đo; percentile tính trên mọi request, kể cả request lỗi.</summary>
    public static NfrMetric WriteMetric(string caseId, int vu, TimeSpan duration, IReadOnlyCollection<NfrSample> samples, string extraNote = "")
    {
        var sorted = samples.Select(s => s.LatencyMs).Order().ToArray();
        double Percentile(double p) => sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];
        var ok = samples.Count(s => s.Ok);
        var sec = duration.TotalSeconds;
        return WriteMetric(new NfrMetric(caseId, vu, Math.Round(sec, 2), sec > 0 ? Math.Round(samples.Count / sec, 2) : 0,
            Math.Round(Percentile(0.50), 1), Math.Round(Percentile(0.95), 1), Math.Round(sorted.LastOrDefault(), 1),
            ok, samples.Count - ok, extraNote));
    }

    /// <summary>Chuyển thống kê NBomber. Latency lấy từ request OK; nếu không có request OK thì lấy từ request lỗi.</summary>
    public static NfrMetric WriteMetric(string caseId, int vu, ScenarioStats stats, string extraNote = "")
    {
        var ok = stats.Ok.Request.Count;
        var fail = stats.Fail.Request.Count;
        var latency = ok > 0 ? stats.Ok.Latency : stats.Fail.Latency;
        return WriteMetric(new NfrMetric(caseId, vu, Math.Round(stats.Duration.TotalSeconds, 2),
            Math.Round(stats.Ok.Request.RPS + stats.Fail.Request.RPS, 2),
            latency.Percent50, latency.Percent95, latency.MaxMs, ok, fail, extraNote));
    }
}

/// <summary>
/// Nền cho ca NFR gọi API (Load / Race / Security). Dùng chung ST_BASE_URL và tài khoản của BaseSystemTest.
/// Mỗi ca đăng ký việc dọn dữ liệu bằng RegisterCleanup / RegisterSweep; việc dọn chạy sau ca, kể cả khi Failed.
/// </summary>
public abstract class BaseNfrTest : IAsyncLifetime
{
    private const string LoginPath = "/api/v1/common/auth/login";

    private sealed record Session(string AccessToken, DateTimeOffset ExpiresAt);

    private static readonly ConcurrentDictionary<string, Session> Sessions = new();
    private static readonly SemaphoreSlim LoginLock = new(1, 1);
    private static readonly HttpClient AuthClient = new() { BaseAddress = new Uri(Nfr.BaseUrl) };

    private readonly Stack<(string What, Func<Task> Action)> _cleanups = new();
    private readonly List<HttpClient> _clients = new();

    protected BaseNfrTest() => Nfr.EnsureSafeTarget();

    // ---------------------------------------------------------------- token / client

    /// <summary>Access token theo tài khoản, dùng lại giữa các ca và đăng nhập lại khi còn dưới 2 phút (soak test).</summary>
    public static async Task<string> GetTokenAsync(string username, string password)
    {
        bool Fresh(Session? s) => s is not null && s.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(2);
        if (Sessions.TryGetValue(username, out var cached) && Fresh(cached)) return cached.AccessToken;
        await LoginLock.WaitAsync();
        try
        {
            if (Sessions.TryGetValue(username, out cached) && Fresh(cached)) return cached.AccessToken;
            using var res = await AuthClient.PostAsJsonAsync(LoginPath, new { username, password });
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Đăng nhập API '{username}' lỗi HTTP {(int)res.StatusCode}.");
            var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            var session = new Session(data.GetProperty("accessToken").GetString()!, data.GetProperty("accessExpiresAt").GetDateTimeOffset());
            Sessions[username] = session;
            return session.AccessToken;
        }
        finally { LoginLock.Release(); }
    }

    public static Task<string> GetRoleTokenAsync(string role)
    {
        var (user, pass) = BaseSystemTest.GetAccount(role);
        return GetTokenAsync(user, pass);
    }

    /// <summary>Bỏ token đã cache (sau ca logout / khóa tài khoản / reset mật khẩu).</summary>
    public static void InvalidateToken(string username) => Sessions.TryRemove(username, out _);

    private sealed class RoleTokenHandler(string role) : DelegatingHandler(new SocketsHttpHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetRoleTokenAsync(role));
            return await base.SendAsync(request, ct);
        }
    }

    private HttpClient Track(HttpClient client, TimeSpan? timeout)
    {
        client.BaseAddress = new Uri(Nfr.BaseUrl);
        client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
        _clients.Add(client);
        return client;
    }

    /// <summary>HttpClient tự gắn Bearer token còn hạn của Role.</summary>
    protected HttpClient CreateClient(string role, TimeSpan? timeout = null) => Track(new HttpClient(new RoleTokenHandler(role)), timeout);

    /// <summary>HttpClient không token (ca thiếu token / token giả / X-Agent-Key).</summary>
    protected HttpClient CreateAnonymousClient(TimeSpan? timeout = null) => Track(new HttpClient(), timeout);

    // ---------------------------------------------------------------- đo tải / đồng thời

    /// <summary>Gửi một request và đo thời gian. isOk mặc định: HTTP 2xx. Lỗi mạng / timeout trả StatusCode = 0.</summary>
    protected static async Task<NfrSample> MeasureAsync(Func<Task<HttpResponseMessage>> send, Func<HttpResponseMessage, bool>? isOk = null)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var res = await send();
            return new NfrSample(watch.Elapsed.TotalMilliseconds, isOk?.Invoke(res) ?? res.IsSuccessStatusCode, (int)res.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new NfrSample(watch.Elapsed.TotalMilliseconds, false, 0);
        }
    }

    /// <summary>Bắn N request cùng xuất phát qua một cổng chung rồi chờ bằng Task.WhenAll.</summary>
    protected static async Task<T[]> FireConcurrentlyAsync<T>(int count, Func<int, Task<T>> action)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            return await action(i);
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Chạy các scenario NBomber, ghi chỉ số của scenario đầu tiên (scenario cần Assert) rồi trả toàn bộ thống kê.
    /// Có tải nền thì đọc thêm scenario khác từ NodeStats và đưa số vào extra_note.
    /// </summary>
    protected static (NfrMetric Metric, NodeStats Stats) RunLoad(string caseId, int vu, string extraNote, params ScenarioProps[] scenarios)
    {
        Nfr.ResetMetric(caseId);
        var stats = NBomberRunner.RegisterScenarios(scenarios).WithoutReports().Run();
        var primary = stats.ScenarioStats.Single(s => s.ScenarioName == scenarios[0].ScenarioName);
        return (Nfr.WriteMetric(caseId, vu, primary, extraNote), stats);
    }

    // ---------------------------------------------------------------- dữ liệu test và dọn dẹp

    /// <summary>Tên / mã dữ liệu test, luôn bắt đầu bằng NFR_AUTO_. Tự rút ngắn tag nếu trường bị giới hạn độ dài.</summary>
    protected static string NewDataName(string tag = "") =>
        $"{Nfr.DataPrefix}{(tag.Length > 0 ? tag + "_" : "")}{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Đăng ký việc dọn; chạy theo thứ tự ngược lúc đăng ký (con trước, cha sau).</summary>
    protected void RegisterCleanup(string what, Func<Task> action) => _cleanups.Push((what, action));

    /// <summary>Đăng ký quét rác sau ca (dành cho tải sinh nhiều bản ghi, không giữ được từng ID).</summary>
    protected void RegisterSweep(string role, string listPath, string nameField, Func<HttpClient, JsonElement, Task<HttpResponseMessage>> remove) =>
        RegisterCleanup($"sweep {listPath}", () => SweepAsync(role, listPath, nameField, remove));

    /// <summary>
    /// Tìm qua API danh sách công khai (search=NFR_AUTO_) rồi xóa / hủy từng bản ghi bằng hàm remove.
    /// Chỉ đụng bản ghi có nameField bắt đầu bằng NFR_AUTO_, phòng API bỏ qua tham số search.
    /// </summary>
    protected async Task SweepAsync(string role, string listPath, string nameField, Func<HttpClient, JsonElement, Task<HttpResponseMessage>> remove)
    {
        var client = CreateClient(role);
        var found = new List<JsonElement>();
        var sep = listPath.Contains('?') ? '&' : '?';
        // Gom hết trước khi xóa để phân trang không bị lệch
        for (var page = 1; page <= 1000; page++)
        {
            var json = await client.GetFromJsonAsync<JsonElement>($"{listPath}{sep}search={Nfr.DataPrefix}&page={page}&pageSize=100");
            var items = json.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
            found.AddRange(items.Where(i => i.TryGetProperty(nameField, out var n) && n.ValueKind == JsonValueKind.String
                                            && n.GetString()!.StartsWith(Nfr.DataPrefix, StringComparison.Ordinal)));
            if (items.Length < 100) break;
        }

        var failed = 0;
        foreach (var item in found)
        {
            using var res = await remove(client, item);
            if (!res.IsSuccessStatusCode) failed++;
        }
        if (failed > 0) throw new InvalidOperationException($"{failed}/{found.Count} bản ghi chưa dọn được.");
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Lỗi dọn dẹp không đổi kết quả ca đo; ghi vào nfr_cleanup_leftovers.log để dọn tay.</summary>
    public virtual async Task DisposeAsync()
    {
        while (_cleanups.TryPop(out var item))
        {
            try { await item.Action(); }
            catch (Exception e)
            {
                var line = $"{DateTime.Now:s} {GetType().Name} | {item.What} | {e.Message}";
                Console.Error.WriteLine(line);
                Directory.CreateDirectory(Nfr.ResultsDir);
                File.AppendAllText(Path.Combine(Nfr.ResultsDir, "nfr_cleanup_leftovers.log"), line + Environment.NewLine);
            }
        }
        foreach (var c in _clients) c.Dispose();
    }
}

/// <summary>Nền cho ca NFR cần trình duyệt (Group4): phiên theo Role và ảnh lỗi của BaseSystemTest, thêm chốt môi trường.</summary>
public abstract class NfrBrowserTest : BaseSystemTest
{
    protected NfrBrowserTest() => Nfr.EnsureSafeTarget();
}
