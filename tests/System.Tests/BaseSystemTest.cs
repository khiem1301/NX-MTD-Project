using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace System.Tests;

/// <summary>Nền System Test E2E: cấu hình, cache phiên theo Role, helper chuẩn, chụp ảnh khi Failed.</summary>
public abstract class BaseSystemTest : PageTest
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.SystemTest.json", optional: false)
        .AddJsonFile("appsettings.SystemTest.local.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static readonly string BaseUrl =
        (Environment.GetEnvironmentVariable("ST_BASE_URL") ?? Config["BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    private static readonly string AuthDir = Path.Combine(AppContext.BaseDirectory, ".auth");
    private static readonly SemaphoreSlim LoginLock = new(1, 1);

    /// <summary>Mã ca (ST_M1_001...) để đặt tên ảnh lỗi; lấy từ tên hàm test nếu không gán thủ công.</summary>
    protected string CaseId { get; set; } = "";

    // xUnit v2 không lộ kết quả test cho Dispose: ghi nhận lỗi Assert/Playwright bằng FirstChanceException (theo luồng async của ca).
    private sealed class FailureFlag { public volatile bool Failed; }
    private static readonly AsyncLocal<FailureFlag?> Flag = new();
    private static bool _hooked;

    protected BaseSystemTest()
    {
        var flag = new FailureFlag();
        Flag.Value = flag;
        if (_hooked) return;
        _hooked = true;
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            var n = e.Exception.GetType().Name;
            if (e.Exception is PlaywrightException || e.Exception is TimeoutException || e.Exception.Message.StartsWith("SAI: ") || n.Contains("Assert") || n.EndsWith("XunitException") || n.EndsWith("EqualException") || n.EndsWith("TrueException"))
            {
                var f = Flag.Value;
                if (f is not null) f.Failed = true;
            }
        };
    }

    private string MethodName => GetType().GetMethods().FirstOrDefault(m => m.GetCustomAttributes(typeof(FactAttribute), true).Length > 0)?.Name ?? "unknown";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = BaseUrl,
        IgnoreHTTPSErrors = true,
        Locale = "vi-VN",
        ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
    };

    public static (string Username, string Password) GetAccount(string role)
    {
        var sec = Config.GetSection($"Accounts:{role}");
        var u = sec["Username"]; var p = sec["Password"];
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            throw new InvalidOperationException($"Thiếu tài khoản Role '{role}' trong appsettings.SystemTest.json.");
        return (u, p);
    }

    public static void InvalidateRole(string role)
    {
        try { File.Delete(StatePath(role)); } catch { }
    }

    private static string StatePath(string role) => Path.Combine(AuthDir, $"{role}.json");

    /// <summary>Đăng nhập một lần cho mỗi Role, lưu StorageState vào .auth/ để dùng lại.</summary>
    private async Task EnsureStorageStateAsync(string role)
    {
        var path = StatePath(role);
        if (File.Exists(path)) return;
        await LoginLock.WaitAsync();
        try
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(AuthDir);
            var (user, pass) = GetAccount(role);
            await using var ctx = await Browser.NewContextAsync(ContextOptions());
            var page = await ctx.NewPageAsync();
            await page.GotoAsync("/login");
            await page.GetByLabel("Tên đăng nhập").FillAsync(user);
            await page.GetByLabel("Mật khẩu").FillAsync(pass);
            await page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" }).ClickAsync();
            await page.WaitForURLAsync(u => !u.Contains("/login"));
            await ctx.StorageStateAsync(new() { Path = path });
        }
        finally { LoginLock.Release(); }
    }

    /// <summary>Mở trang mới đã đăng nhập sẵn bằng phiên Role đã cache.</summary>
    public async Task<IPage> NewPageAsRoleAsync(string role)
    {
        await EnsureStorageStateAsync(role);
        var opts = ContextOptions();
        opts.StorageStatePath = StatePath(role);
        var ctx = await Browser.NewContextAsync(opts);
        var page = await ctx.NewPageAsync();
        _pages.Add(page);
        return page;
    }

    private readonly List<IPage> _pages = new();

    public async Task<IPage> NewAnonymousPageAsync()
    {
        var ctx = await Browser.NewContextAsync(ContextOptions());
        var page = await ctx.NewPageAsync();
        _pages.Add(page);
        return page;
    }

    public static async Task FillLoginAsync(IPage page, string user, string pass)
    {
        await page.GetByLabel("Tên đăng nhập").FillAsync(user);
        await page.GetByLabel("Mật khẩu").FillAsync(pass);
        await page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" }).ClickAsync();
    }

    public static async Task LoginUiAsync(IPage page, string user, string pass)
    {
        await page.GotoAsync("/login");
        await FillLoginAsync(page, user, pass);
        await page.WaitForURLAsync(u => !u.Contains("/login"));
    }

    // Kiểm hết các điểm của một ca rồi mới báo Failed, để Note ghi đủ mọi điểm lệch đặc tả
    private readonly List<string> _soft = new();

    protected async Task SoftAsync(string what, Func<Task> check)
    {
        try { await check(); }
        catch (Exception) { _soft.Add(what); }
    }

    protected void SoftVerify()
    {
        if (_soft.Count == 0) return;
        // parse_trx cắt thông báo ở 200 ký tự: ghi đủ danh sách điểm sai ra file cạnh ảnh lỗi
        var dir = Path.Combine(ScreenshotDir(), "..", "soft");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, $"{CaseId}.txt"), _soft);
        throw new InvalidOperationException("SAI: " + string.Join(" | ", _soft));
    }

    public static string PathOf(IPage page) => new Uri(page.Url).AbsolutePath;

    public static async Task GotoAndWaitAsync(IPage page, string relativeUrl)
    {
        await page.GotoAsync(relativeUrl.StartsWith('/') ? relativeUrl : "/" + relativeUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Chờ toast (antd message/notification) chứa nội dung kỳ vọng.</summary>
    public static async Task AssertToastAsync(IPage page, string expectedText)
    {
        var toast = page.Locator(".ant-message-notice, .ant-notification-notice")
            .Filter(new() { HasTextRegex = new Regex(Regex.Escape(expectedText), RegexOptions.IgnoreCase) });
        await Assertions.Expect(toast.First).ToBeVisibleAsync();
    }

    /// <summary>Chụp ảnh các trang đang mở khi test lỗi, rồi đóng context.</summary>
    public override async Task DisposeAsync()
    {
        var failed = Flag.Value?.Failed == true;
        if (failed)
        {
            var id = !string.IsNullOrEmpty(CaseId) ? CaseId : MethodName;
            var m = Regex.Match(id, @"ST_M\d+_\d+");
            if (m.Success) id = m.Value;
            var dir = ScreenshotDir();
            Directory.CreateDirectory(dir);
            var all = _pages.Concat(new[] { Page }).Where(p => p is not null && !p.IsClosed).ToList();
            for (var i = 0; i < all.Count; i++)
            {
                try
                {
                    var file = Path.Combine(dir, i == 0 ? $"{id}.png" : $"{id}_{i}.png");
                    await all[i].ScreenshotAsync(new() { Path = file, FullPage = true });
                }
                catch { /* trang đã đóng: bỏ qua */ }
            }
        }
        foreach (var p in _pages) { try { await p.Context.CloseAsync(); } catch { } }
        await base.DisposeAsync();
    }

    private static string ScreenshotDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "docs"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "tests", "docs", "ST_RESULTS", "screenshots");
    }
}
