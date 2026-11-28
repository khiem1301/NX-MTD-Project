using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace System.Tests.NFR.Group3;

/// <summary>Kết quả một lời gọi API.</summary>
public sealed record Resp(int Status, string Text, byte[] Bytes, double Ms, HttpResponseHeaders? Headers)
{
    public JsonNode? Json
    {
        get { try { return JsonNode.Parse(Text); } catch (JsonException) { return null; } }
    }

    /// <summary>Trường data của envelope ApiResponse.</summary>
    public JsonNode? Data => Json?["data"];

    public bool Is(params int[] codes) => codes.Contains(Status);
    public bool Success => Status is >= 200 and < 300;
}

/// <summary>
/// Gom mọi phép kiểm của một ca rồi mới Assert một lần, để Note liệt kê đủ điểm vi phạm.
/// Mỗi phép kiểm là một mẫu đo: ok_count / fail_count = số phép kiểm đạt / vi phạm.
/// </summary>
public sealed class Probe
{
    private readonly string _id;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<NfrSample> _samples = new();
    private readonly List<string> _violations = new();
    private readonly List<string> _notes = new();

    public Probe(string caseId)
    {
        _id = caseId;
        Nfr.ResetMetric(caseId);
        var detail = DetailPath();
        if (File.Exists(detail)) File.Delete(detail);
    }

    private string DetailPath() => Path.Combine(Nfr.ResultsDir, "soft", $"{_id}.txt");

    public bool Check(bool ok, string what, Resp? r = null)
    {
        _samples.Add(new NfrSample(r?.Ms ?? 0, ok, r?.Status ?? 0));
        if (!ok) _violations.Add(r is null ? what : $"{what} → {r.Status}");
        return ok;
    }

    /// <summary>Kỳ vọng HTTP status nằm trong danh sách.</summary>
    public bool Status(Resp r, string what, params int[] expected) =>
        Check(r.Is(expected), $"{what} (cần {string.Join("/", expected)})", r);

    public void Note(string note) => _notes.Add(note);

    public void Done()
    {
        var extra = "Phép kiểm đạt/vi phạm tính ở ok/fail; p50/p95 là latency của request được kiểm.";
        if (_notes.Count > 0) extra += " " + string.Join("; ", _notes);
        Nfr.WriteMetric(_id, 1, _watch.Elapsed, _samples, extra);
        if (_violations.Count > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DetailPath())!);
            File.WriteAllLines(DetailPath(), _violations);
        }
        Assert.True(_samples.Count > 0, $"{_id}: không có phép kiểm nào được thực hiện.");
        Assert.True(_violations.Count == 0, $"{_violations.Count}/{_samples.Count} vi phạm: {string.Join(" | ", _violations.Take(6))}");
    }
}

/// <summary>Tài khoản bộ seed SQL/seed (tên đăng nhập = mã nhân viên). Mật khẩu lấy từ biến môi trường NFR_PASSWORD.</summary>
public static class Acc
{
    public const string Admin = "ADMIN", Manager = "QUANDOC", Accountant = "KETOAN", Keeper = "THUKHO",
        Leader = "TTMAY", Deputy = "TPMAY", Leader2 = "TTMAY2", CutLeader = "TTCAT",
        Qa = "QAQC1", Qa2 = "QAQC2", Electrician = "CODIEN", WorkerA = "CNMAY1", WorkerB = "CNMAY9";

    public const int AdminId = 1, QaId = 5, WorkerAId = 18, WorkerBId = 28;

    public static readonly (string Role, string User)[] Roles =
    {
        ("Admin", Admin), ("Manager", Manager), ("Accountant", Accountant), ("WarehouseKeeper", Keeper),
        ("TeamLeader", Leader), ("TeamDeputy", Deputy), ("QaQc", Qa), ("Worker", WorkerA), ("Electrician", Electrician),
    };

    public static string Password => G3Env.Required("NFR_PASSWORD");
}

public static class G3Env
{
    public static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : throw new InvalidOperationException($"THIẾU MÔI TRƯỜNG: chưa đặt biến {name}.");

    /// <summary>Gọi thẳng API (không qua proxy của FE) để mã lỗi và header đúng là của API.</summary>
    public static Uri ApiUrl
    {
        get
        {
            var uri = new Uri(Environment.GetEnvironmentVariable("NFR_API_URL") ?? Nfr.BaseUrl);
            if (!uri.IsLoopback) throw new InvalidOperationException($"Từ chối chạy NFR lên '{uri.Host}': không phải localhost.");
            return uri;
        }
    }
}

[CollectionDefinition("NFR_G3", DisableParallelization = true)]
public sealed class G3Collection;

/// <summary>Nền cho ca Security gọi API.</summary>
public abstract class G3Base : BaseNfrTest
{
    protected static readonly HttpClient Api = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        BaseAddress = G3Env.ApiUrl,
        Timeout = TimeSpan.FromSeconds(90),
    };

    protected static Task<string> Token(string user) => GetTokenAsync(user, Acc.Password);

    protected static string Url(string path) => path.StartsWith('/') ? path : "/api/v1/" + path;

    protected static HttpContent Json(object body) =>
        new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    protected static HttpContent Raw(string body, string contentType)
    {
        var c = new StringContent(body, Encoding.UTF8);
        c.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return c;
    }

    protected static MultipartFormDataContent Multipart(HttpContent file, string fileName, string contentType, params (string, string)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (k, v) in fields) form.Add(new StringContent(v), k);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    protected const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    protected static byte[] Xlsx(string[] headers, params object[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>PNG 1x1 hợp lệ, đệm thêm byte 0 cho đủ kích thước yêu cầu.</summary>
    protected static byte[] Png(int size = 0)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        if (size > png.Length) Array.Resize(ref png, size);
        return png;
    }

    /// <summary>Gửi request. user = null là không đăng nhập. authorization ghi đè header Authorization (token giả, rỗng).</summary>
    protected static async Task<Resp> Send(string? user, HttpMethod method, string path, HttpContent? content = null,
        string? authorization = null, (string, string)[]? headers = null, bool expectContinue = false)
    {
        using var req = new HttpRequestMessage(method, Url(path)) { Content = content };
        if (authorization is not null) req.Headers.TryAddWithoutValidation("Authorization", authorization);
        else if (user is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token(user));
        foreach (var (k, v) in headers ?? Array.Empty<(string, string)>()) req.Headers.TryAddWithoutValidation(k, v);
        if (expectContinue) req.Headers.ExpectContinue = true;
        var watch = Stopwatch.StartNew();
        try
        {
            using var res = await Api.SendAsync(req);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            return new Resp((int)res.StatusCode, Encoding.UTF8.GetString(bytes), bytes, watch.Elapsed.TotalMilliseconds, res.Headers);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return new Resp(0, e.GetType().Name + ": " + e.Message, Array.Empty<byte>(), watch.Elapsed.TotalMilliseconds, null);
        }
    }

    protected static Task<Resp> Get(string? user, string path) => Send(user, HttpMethod.Get, path);
    protected static Task<Resp> Post(string? user, string path, object? body = null) => Send(user, HttpMethod.Post, path, body is null ? null : Json(body));
    protected static Task<Resp> Put(string? user, string path, object body) => Send(user, HttpMethod.Put, path, Json(body));
    protected static Task<Resp> Delete(string? user, string path) => Send(user, HttpMethod.Delete, path);
    protected static Task<Resp> Patch(string? user, string path, object body) => Send(user, HttpMethod.Patch, path, Json(body));

    /// <summary>Đọc dữ liệu nền; lỗi ở đây là thiếu dữ liệu mồi / môi trường, không phải lỗi bảo mật.</summary>
    protected static async Task<JsonNode> Seed(string user, string path)
    {
        var r = await Get(user, path);
        return r.Success && r.Data is { } d ? d : throw new InvalidOperationException($"THIẾU SEED: {user} GET {path} → {r.Status}");
    }

    protected static JsonArray Items(JsonNode data) => data as JsonArray ?? data["items"]!.AsArray();

    protected static int Total(JsonNode data) =>
        data is JsonArray a ? a.Count : (data["totalItems"] ?? data["totalCount"] ?? data["total"])?.GetValue<int>() ?? Items(data).Count;

    protected static async Task<JsonNode> Login(string user, string? password = null)
    {
        var r = await Post(null, "common/auth/login", new { username = user, password = password ?? Acc.Password });
        return r.Success && r.Data is { } d ? d : throw new InvalidOperationException($"THIẾU SEED: đăng nhập {user} → {r.Status}");
    }

    protected static async Task<int> EmployeeId(string code)
    {
        var data = await Seed(Acc.Admin, $"admin/employees?Keyword={code}&PageSize=100");
        return Items(data).First(e => e!["employeeCode"]!.GetValue<string>() == code)!["employeeId"]!.GetValue<int>();
    }

    protected static async Task<int> CreateOwnRequest(string user, string title)
    {
        var r = await Post(user, "common/requests", new { requestType = "Other", title, content = "Dữ liệu kiểm thử bảo mật", handlerRole = "Admin" });
        var id = r.Data is JsonValue v ? v.GetValue<int>() : r.Data?["requestId"]?.GetValue<int>();
        return r.Success && id is { } i ? i : throw new InvalidOperationException($"THIẾU SEED: {user} tạo đơn → {r.Status} {r.Text}");
    }

    protected static string Month(int back = 0) => DateTime.Today.AddMonths(-back).ToString("yyyy-MM");
}

/// <summary>Route lấy từ OpenAPI (Swagger bật ở môi trường Development) của đúng bản code đang test.</summary>
public static class OpenApi
{
    public sealed record Route(string Method, string Template, bool Multipart)
    {
        public string Concrete => Template
            .Replace("{periodMonth}", Month).Replace("{kind}", "spec").Replace("{date}", DateTime.Today.ToString("yyyy-MM-dd"))
            .Pipe(p => Text.RegularExpressions.Regex.Replace(p, @"\{[^}]+\}", "1"));
        private static string Month => DateTime.Today.AddMonths(-2).ToString("yyyy-MM");
    }

    private static string Pipe(this string s, Func<string, string> f) => f(s);

    public static IReadOnlyList<Route> Routes()
    {
        var doc = JsonNode.Parse(File.ReadAllText(G3Env.Required("NFR_OPENAPI_FILE")))!;
        return doc["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Select(op => new Route(op.Key.ToUpperInvariant(), p.Key,
                op.Value!["requestBody"]?["content"]?["multipart/form-data"] is not null)))
            .ToList();
    }
}
