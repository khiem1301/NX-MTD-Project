using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace System.Tests.NFR.Group3;

/// <summary>G3-06, G3-07: xác thực, token và Agent API. Ca phá trạng thái tài khoản dùng tài khoản tổ cắt riêng.</summary>
[Collection("NFR_G3")]
public sealed class G3AuthAgentTests : G3Base
{
    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromB64(string s) =>
        Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));

    private static string Sign(string header, string payload, string key) =>
        $"{header}.{payload}.{B64(new HMACSHA256(Encoding.UTF8.GetBytes(key)).ComputeHash(Encoding.ASCII.GetBytes($"{header}.{payload}")))}";

    private static Task<Resp> Me(string token) => Send(null, HttpMethod.Get, "common/auth/me", authorization: "Bearer " + token);

    private static Task<Resp> Refresh(string refreshToken) => Post(null, "common/auth/refresh", new { refreshToken });

    private static string Str(JsonNode n, string key) => n[key]!.GetValue<string>();

    [Fact]
    public async Task NFR_G3_027_LoginMeRefreshLogout()
    {
        var p = new Probe("NFR_G3_027");
        var login = await Post(null, "common/auth/login", new { username = "CNCAT2", password = Acc.Password });
        p.Status(login, "login", 200);
        var access = login.Data?["accessToken"]?.GetValue<string>() ?? "";
        var refresh = login.Data?["refreshToken"]?.GetValue<string>() ?? "";
        p.Check(access.Length > 0 && refresh.Length > 0, "login trả access + refresh token", login);
        var me = await Me(access);
        p.Status(me, "me", 200);
        p.Check(me.Text.Contains("CNCAT2"), "me đúng user", me);
        await Task.Delay(1100); // exp tính theo giây: tránh token mới trùng hệt token cũ
        var renewed = await Refresh(refresh);
        p.Status(renewed, "refresh", 200);
        var newAccess = renewed.Data?["accessToken"]?.GetValue<string>() ?? "";
        p.Check(newAccess.Length > 0 && newAccess != access, "refresh trả token mới", renewed);
        var logout = await Send(null, HttpMethod.Post, "common/auth/logout", authorization: "Bearer " + newAccess);
        p.Check(logout.Success, "logout 2xx", logout);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_028_ExpiredToken_ClockSkew()
    {
        var p = new Probe("NFR_G3_028");
        var key = G3Env.Required("NFR_JWT_KEY");
        var parts = (await Token(Acc.WorkerA)).Split('.');
        string Expired(int secondsAgo)
        {
            var claims = JsonNode.Parse(FromB64(parts[1]))!.AsObject();
            var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - secondsAgo;
            claims["exp"] = exp;
            if (claims.ContainsKey("nbf")) claims["nbf"] = exp - 600;
            if (claims.ContainsKey("iat")) claims["iat"] = exp - 600;
            return Sign(parts[0], B64(Encoding.UTF8.GetBytes(claims.ToJsonString())), key);
        }
        p.Status(await Me(Expired(30)), "exp + 30 s", 200);
        p.Status(await Me(Expired(61)), "exp + 61 s", 401);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_029_TamperedTokens_401()
    {
        var p = new Probe("NFR_G3_029");
        var token = await Token(Acc.WorkerA);
        var parts = token.Split('.');
        var mid = parts[2].Length / 2;
        var badSig = parts[2][..mid] + (parts[2][mid] == 'A' ? 'B' : 'A') + parts[2][(mid + 1)..];
        var payload = Encoding.UTF8.GetString(FromB64(parts[1]));
        var escalated = B64(Encoding.UTF8.GetBytes(payload.Replace("\"Worker\"", "\"Admin\"")));
        Assert.NotEqual(parts[1], escalated);
        var none = B64(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var variants = new (string Name, string Token)[]
        {
            ("sửa chữ ký", $"{parts[0]}.{parts[1]}.{badSig}"),
            ("đổi role giữ chữ ký", $"{parts[0]}.{escalated}.{parts[2]}"),
            ("alg none", $"{none}.{escalated}."),
            ("khóa khác", Sign(parts[0], escalated, new string('x', 64))),
        };
        foreach (var (name, forged) in variants)
        {
            p.Status(await Me(forged), $"me: {name}", 401);
            p.Status(await Send(null, HttpMethod.Get, "admin/employees", authorization: "Bearer " + forged), $"admin API: {name}", 401);
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_030_RefreshTokenRevocation()
    {
        var p = new Probe("NFR_G3_030");

        var a = await Login("CNCAT2");
        await Send(null, HttpMethod.Post, "common/auth/logout", authorization: "Bearer " + Str(a, "accessToken"));
        p.Status(await Refresh(Str(a, "refreshToken")), "(a) sau logout", 401);

        var lockedId = await EmployeeId("CNCAT6");
        var b = await Login("CNCAT6");
        RegisterCleanup("mở khóa CNCAT6", () => Post(Acc.Admin, $"admin/employees/{lockedId}/account/unlock"));
        var locked = await Post(Acc.Admin, $"admin/employees/{lockedId}/account/lock", new { reason = "NFR_AUTO_ revoke" });
        Assert.True(locked.Success, $"THIẾU SEED: khóa CNCAT6 → {locked.Status}");
        p.Status(await Refresh(Str(b, "refreshToken")), "(b) sau lock", 401);

        var resetId = await EmployeeId("CNCAT5");
        var c = await Login("CNCAT5");
        var reset = await Post(Acc.Admin, $"admin/employees/{resetId}/account/reset-password");
        Assert.True(reset.Success, $"THIẾU SEED: reset CNCAT5 → {reset.Status}");
        p.Status(await Refresh(Str(c, "refreshToken")), "(c) sau reset-password", 401);

        var d = await Login("CNCAT4");
        var next = "NfrChanged@456";
        var change = await Send(null, HttpMethod.Post, "common/profile/change-password",
            Json(new { currentPassword = Acc.Password, newPassword = next, confirmPassword = next }), "Bearer " + Str(d, "accessToken"));
        Assert.True(change.Success, $"THIẾU SEED: đổi mật khẩu CNCAT4 → {change.Status} {change.Text}");
        p.Status(await Refresh(Str(d, "refreshToken")), "(d) sau change-password", 401);

        var e = await Login("CNCAT2");
        var first = await Refresh(Str(e, "refreshToken"));
        Assert.True(first.Success, $"refresh lần 1 → {first.Status}");
        var again = await Refresh(Str(e, "refreshToken"));
        if (first.Data?["refreshToken"]?.GetValue<string>() is { } rotated && rotated != Str(e, "refreshToken"))
            p.Status(again, "(e) dùng lại refresh token lần 2", 401);
        else
            p.Note($"RỦI RO (e): server không xoay vòng refresh token, dùng lại lần 2 → HTTP {again.Status}");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_031_BruteForceIsThrottled()
    {
        var p = new Probe("NFR_G3_031");
        const string victim = "CNCAT3";
        await Login(victim);
        var watch = Stopwatch.StartNew();
        var statuses = new List<int>();
        for (var i = 0; i < 10; i++)
            statuses.Add((await Post(null, "common/auth/login", new { username = victim, password = $"Sai@{i}xyz" })).Status);
        // Sau 10 lần sai: lần kế tiếp dùng mật khẩu ĐÚNG phải bị chặn (429 hoặc tạm khóa)
        var correct = await Post(null, "common/auth/login", new { username = victim, password = Acc.Password });
        p.Check(statuses.Contains(429) || !correct.Success, "Sau ≤ 10 lần sai phải 429 hoặc tạm khóa; mật khẩu đúng vẫn đăng nhập được", correct);
        for (var i = 10; i < 100; i++)
            statuses.Add((await Post(null, "common/auth/login", new { username = victim, password = $"Sai@{i}xyz" })).Status);
        var seconds = watch.Elapsed.TotalSeconds;
        for (var i = 0; i < 100; i++)
            statuses.Add((await Post(null, "common/auth/login", new { username = $"NFR_AUTO_u{i}", password = "Sai@123456" })).Status);
        p.Check(statuses.All(s => s is > 0 and < 500), $"Không có 5xx/lỗi kết nối ({statuses.Count(s => s is 0 or >= 500)} lỗi)");
        p.Note($"100 lần sai cùng user trong {seconds:0.0} s; số 429 = {statuses.Count(s => s == 429)}/200");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_032_ManagerCannotControlAdminAccount()
    {
        var p = new Probe("NFR_G3_032");
        // Nếu lỗ hổng tồn tại thì mật khẩu / trạng thái Admin bị đổi: trả lại để các ca sau không Failed lây
        var original = Sql($"SELECT CONCAT(PasswordHash,'|',Status,'|',CAST(IsTemporaryPassword AS int)) FROM Account WHERE EmployeeId={Acc.AdminId}").Split('|');
        RegisterCleanup("khôi phục tài khoản Admin", () =>
        {
            Sql($"UPDATE Account SET PasswordHash='{original[0]}', Status='{original[1]}', IsTemporaryPassword={original[2]} WHERE EmployeeId={Acc.AdminId}");
            InvalidateToken(Acc.Admin);
            return Task.CompletedTask;
        });
        foreach (var (action, body) in new (string, object?)[] { ("reset-password", null), ("lock", new { reason = "NFR_AUTO_" }), ("unlock", null) })
            p.Status(await Post(Acc.Manager, $"manager/employees/{Acc.AdminId}/account/{action}", body), $"Manager {action} Admin", 403);
        var login = await Post(null, "common/auth/login", new { username = Acc.Admin, password = Acc.Password });
        p.Status(login, "Admin vẫn đăng nhập bằng mật khẩu cũ", 200);
        p.Done();
    }

    /// <summary>Chỉ dùng để dựng lại môi trường sau ca phá trạng thái, không dùng để Assert.</summary>
    private static string Sql(string query)
    {
        var db = G3Env.Required("NFR_DB");
        if (!db.Contains("NFR")) throw new InvalidOperationException("Chỉ cho phép thao tác trên database test NFR.");
        var psi = new ProcessStartInfo("sqlcmd") { RedirectStandardOutput = true };
        foreach (var arg in new[] { "-S", "localhost", "-E", "-C", "-I", "-b", "-h", "-1", "-W", "-d", db, "-Q", "SET NOCOUNT ON; " + query }) psi.ArgumentList.Add(arg);
        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? output : throw new InvalidOperationException($"sqlcmd lỗi: {output}");
    }

    [Fact]
    public async Task NFR_G3_033_LoginInjectionAndLongInput()
    {
        var p = new Probe("NFR_G3_033");
        foreach (var user in new[] { "' OR 1=1--", "admin'--" })
        {
            var r = await Post(null, "common/auth/login", new { username = user, password = "x' OR '1'='1" });
            p.Status(r, $"login {user}", 401, 400);
        }
        p.Status(await Post(null, "common/auth/login", new { username = new string('a', 10_000), password = "x" }), "username 10.000 ký tự", 400);
        p.Status(await Post(null, "common/auth/login", new { username = Acc.WorkerA, password = new string('a', 10_000) }), "password 10.000 ký tự", 400);
        p.Status(await Post(Acc.WorkerA, "common/profile/change-password", new { currentPassword = "SaiMatKhau@1", newPassword = "NfrMoi@12345", confirmPassword = "NfrMoi@12345" }), "đổi mật khẩu với mật khẩu cũ sai", 400);
        p.Done();
    }

    // ---------------------------------------------------------------- Agent API

    private static (string, string)[] Key(string? value = null) => new[] { ("X-Agent-Key", value ?? G3Env.Required("NFR_AGENT_KEY")) };

    private static Task<Resp> Agent(HttpMethod method, string path, object? body = null, (string, string)[]? headers = null) =>
        Send(null, method, "agent/attendance-sync/" + path, body is null ? null : Json(body), headers: headers ?? Key());

    private static object Punches(int count, long firstId, int code, DateTime start) => new
    {
        punches = Enumerable.Range(0, count).Select(i => new { sourceRecordId = firstId + i, attendanceCode = code, punchTime = start.AddMinutes(i % 1000).ToString("yyyy-MM-ddTHH:mm:ss") }).ToArray(),
        fileName = "NFR_AUTO_agent",
    };

    private static long NewSourceId() => 900_000_000L + Random.Shared.Next(1, 50_000_000);

    [Fact]
    public async Task NFR_G3_035_AgentValidKey_2xx()
    {
        var p = new Probe("NFR_G3_035");
        p.Check((await Agent(HttpMethod.Get, "cursor")).Success, "cursor 2xx");
        var punches = await Agent(HttpMethod.Post, "punches", Punches(100, NewSourceId(), 9901, DateTime.Today.AddDays(-120)));
        p.Check(punches.Success, "punches batch 100 2xx", punches);
        var emp = await Agent(HttpMethod.Post, "employees", new { employees = new[] { new { attendanceCode = 9901, fullName = "NFR_AUTO_agent", rjEmployeeCode = "NFRAUTO1" } } });
        p.Check(emp.Success, "employees 2xx", emp);
        var beat = await Agent(HttpMethod.Post, "heartbeat");
        p.Check(beat.Success, "heartbeat 2xx", beat);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_036_AgentMissingOrWrongKey_401()
    {
        var p = new Probe("NFR_G3_036");
        var key = G3Env.Required("NFR_AGENT_KEY");
        var before = (await Agent(HttpMethod.Get, "cursor")).Text;
        var jwt = await Token(Acc.Admin);
        var variants = new (string Name, (string, string)[] Headers)[]
        {
            ("thiếu key", Array.Empty<(string, string)>()),
            ("sai ký tự cuối", Key(key[..^1] + (key[^1] == 'a' ? 'b' : 'a'))),
            ("key rỗng", Key("")),
            ("chỉ JWT Admin", new[] { ("Authorization", "Bearer " + jwt) }),
        };
        foreach (var (name, headers) in variants)
        {
            p.Status(await Agent(HttpMethod.Get, "cursor", headers: headers), $"{name}: cursor", 401);
            p.Status(await Agent(HttpMethod.Post, "punches", Punches(1, 999_000_001, 9902, DateTime.Today.AddDays(-100)), headers), $"{name}: punches", 401);
            p.Status(await Agent(HttpMethod.Post, "employees", new { employees = Array.Empty<object>() }, headers), $"{name}: employees", 401);
            p.Status(await Agent(HttpMethod.Post, "heartbeat", headers: headers), $"{name}: heartbeat", 401);
        }
        p.Check(before == (await Agent(HttpMethod.Get, "cursor")).Text, "Cursor không đổi (không ghi punch)");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_037_AgentHugeBatches()
    {
        var p = new Probe("NFR_G3_037");
        using var ram = new RamMonitor();
        var ten = await Agent(HttpMethod.Post, "punches", Punches(10_000, 800_000_000L + Random.Shared.Next(1_000_000), 9903, DateTime.Today.AddDays(-150)));
        p.Check((ten.Success && ten.Ms < 30_000) || ten.Is(413, 400), $"10.000 punch: < 30 s hoặc 413/400 (đo {ten.Ms:0} ms)", ten);
        var hundred = await Agent(HttpMethod.Post, "punches", Punches(100_000, 700_000_000L, 9904, DateTime.Today.AddDays(-160)));
        p.Status(hundred, $"100.000 punch (đo {hundred.Ms:0} ms)", 413, 400);
        var delta = ram.PeakDeltaMb();
        p.Check(delta <= 500, $"RAM tăng {delta:0} MB (cần ≤ +500 MB)");
        p.Check(ram.Alive, "API không restart");
        p.Note($"10k={ten.Status}/{ten.Ms:0} ms; 100k={hundred.Status}/{hundred.Ms:0} ms; RAM đỉnh +{delta:0} MB");
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_038_AgentReplay_NoDuplicatePunches()
    {
        var p = new Probe("NFR_G3_038");
        const int code = 13; // CNCAT1
        var day = DateTime.Today.AddDays(-5);
        var body = Punches(2, NewSourceId(), code, day.AddHours(18).AddMinutes(11));
        var employee = await EmployeeId("CNCAT1");
        var query = $"accountant/attendance/timesheets?FromDate={day:yyyy-MM-dd}&ToDate={day:yyyy-MM-dd}&EmployeeId={employee}";
        var first = await Agent(HttpMethod.Post, "punches", body);
        Assert.True(first.Success, $"Lần gửi đầu phải thành công → {first.Status} {first.Text}");
        var afterFirst = (await Seed(Acc.Accountant, query)).ToJsonString();
        for (var i = 0; i < 10; i++)
        {
            var again = await Agent(HttpMethod.Post, "punches", body);
            p.Check(again.Success || again.Status == 409, $"replay lần {i + 1}: 2xx idempotent hoặc 409", again);
        }
        var afterReplay = (await Seed(Acc.Accountant, query)).ToJsonString();
        p.Check(afterFirst == afterReplay, "Timesheet không đổi sau 10 lần replay");
        p.Done();
    }
}
