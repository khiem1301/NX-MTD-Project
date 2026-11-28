using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace System.Tests.NFR.Group3;

/// <summary>G3-06 (UI), G3-09 (XSS), G3-10 (guard phía Frontend).</summary>
[Collection("NFR_G3")]
public sealed class G3UiTests : NfrBrowserTest
{
    private sealed class Watch
    {
        public List<string> Console { get; } = new();
        public List<string> BadResponses { get; } = new();
        public List<string> Requests { get; } = new();
        public List<string> Dialogs { get; } = new();
    }

    private static Watch Observe(IPage page)
    {
        var w = new Watch();
        page.Console += (_, m) => { if (m.Type == "error") w.Console.Add(m.Text); };
        page.PageError += (_, e) => w.Console.Add(e);
        page.Response += (_, r) =>
        {
            if (!r.Url.Contains("/api/")) return;
            var line = $"{r.Status} {new Uri(r.Url).AbsolutePath}";
            w.Requests.Add(line);
            if (r.Status >= 400) w.BadResponses.Add(line);
        };
        page.Dialog += async (_, d) => { w.Dialogs.Add(d.Message); await d.DismissAsync(); };
        return w;
    }

    private async Task<IPage> OpenAs(string role)
    {
        var page = await NewPageAsRoleAsync(role);
        await page.SetViewportSizeAsync(1920, 1080);
        return page;
    }

    private static async Task<bool> Settle(IPage page, string path)
    {
        try
        {
            await GotoAndWaitAsync(page, path);
            return true;
        }
        catch (TimeoutException) { return false; }
    }

    private static async Task<string> BodyText(IPage page) => await page.Locator("body").InnerTextAsync();

    private async Task Shot(IPage page, string caseId)
    {
        var dir = Path.Combine(Nfr.ResultsDir, "screenshots");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{caseId}.fail.png");
        _evidence = (file, Path.Combine(dir, $"{caseId}.png"));
        if (!File.Exists(file)) await page.ScreenshotAsync(new() { Path = file, FullPage = true });
    }

    private static void ClearShot(string caseId)
    {
        var file = Path.Combine(Nfr.ResultsDir, "screenshots", $"{caseId}.png");
        foreach (var f in new[] { file, Path.ChangeExtension(file, ".fail.png") })
            if (File.Exists(f)) File.Delete(f);
    }

    private (string From, string To)? _evidence;

    /// <summary>Ảnh chụp đúng lúc phát hiện lỗi phải thắng ảnh chụp lúc đóng test của BaseSystemTest.</summary>
    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_evidence is { } e && File.Exists(e.From)) File.Move(e.From, e.To, overwrite: true);
    }

    [Fact]
    public async Task NFR_G3_034_LoginErrorsAreGeneric()
    {
        CaseId = "NFR_G3_034";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        var page = await NewAnonymousPageAsync();
        var w = Observe(page);
        await page.GotoAsync("/login");
        var messages = new List<string>();
        foreach (var user in new[] { "CNCAT1", "NFR_AUTO_khongtontai", "CNCAT1", "NFR_AUTO_khongco", "CNCAT1" })
        {
            await page.GotoAsync("/login");
            var done = page.WaitForResponseAsync(r => r.Url.Contains("/common/auth/login"));
            await FillLoginAsync(page, user, "SaiMatKhau@999");
            await done;
            var alert = page.Locator(".ant-message-error, .ant-alert-error, .ant-form-item-explain-error, .ant-notification-notice-error").First;
            await Assertions.Expect(alert).ToBeVisibleAsync();
            messages.Add((await alert.InnerTextAsync()).Trim());
        }
        p.Check(messages.Distinct().Count() == 1, $"Thông báo phải giống nhau cho sai username / sai mật khẩu: {string.Join(" / ", messages.Distinct())}");
        p.Check(!Regex.IsMatch(messages[0], "(tên đăng nhập|tài khoản) (không tồn tại|không đúng)$|mật khẩu (không đúng|sai)$", RegexOptions.IgnoreCase), $"Thông báo tiết lộ trường sai: {messages[0]}");
        var leaked = w.Console.Count(c => Regex.IsMatch(c, @"eyJ[A-Za-z0-9_-]{10,}\.|accessToken|refreshToken|SaiMatKhau"));
        p.Check(leaked == 0, $"{leaked} lỗi Console chứa token / mật khẩu");
        p.Note($"Thông báo hiển thị: \"{messages[0]}\"");
        if (messages.Distinct().Count() != 1 || leaked > 0) await Shot(page, CaseId);
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_045_StoredXss()
    {
        CaseId = "NFR_G3_045";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        var payloads = new[] { "<script>alert(1)</script>", "<img src=x onerror=alert(1)>", "\"><svg onload=alert(1)>" };
        var api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = Nfr.BaseUrl });
        async Task<APIRequestContextOptions> Auth(string user, object body) => new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + await BaseNfrTest.GetTokenAsync(user, Acc.Password) },
            DataObject = body,
        };

        foreach (var payload in payloads)
        {
            var code = $"NFRXSS{Guid.NewGuid().ToString("N")[..6]}";
            var material = await api.PostAsync("/api/v1/admin/materials", await Auth(Acc.Admin, new { materialCode = code, materialName = $"NFR_AUTO_{payload}", materialType = "TRIM", unit = "cái", color = "Đen" }));
            var title = $"NFR_AUTO_{payload}";
            var request = await api.PostAsync("/api/v1/common/requests", await Auth(Acc.WorkerA, new { requestType = "Other", title, content = payload, handlerRole = "Admin" }));
            p.Check(material.Status is < 500 && request.Status is < 500, $"lưu payload: vật tư {material.Status}, đơn {request.Status}");
        }

        async Task Visit(string role, string path, string label, bool search)
        {
            var page = await OpenAs(role);
            var w = Observe(page);
            await Settle(page, path);
            if (search)
            {
                var box = page.GetByPlaceholder(new Regex("Tìm", RegexOptions.IgnoreCase)).First;
                await box.FillAsync("NFR_AUTO_");
                await box.PressAsync("Enter");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            var shown = page.GetByText("alert(1)", new() { Exact = false }).First;
            var visible = true;
            try { await Assertions.Expect(shown).ToBeVisibleAsync(); }
            catch (PlaywrightException) { visible = false; }
            p.Check(w.Dialogs.Count == 0, $"{label}: xuất hiện {w.Dialogs.Count} dialog alert");
            p.Check(visible, $"{label}: chuỗi phải hiển thị dạng text");
            p.Check(w.Console.Count == 0, $"{label}: {w.Console.Count} lỗi Console ({w.Console.FirstOrDefault()})");
            if (w.Dialogs.Count > 0 || !visible || w.Console.Count > 0) await Shot(page, CaseId);
        }

        await Visit("Admin", "/warehouse/materials", "tên NVL", true);
        await Visit("Worker", "/requests", "tiêu đề đơn", false);
        p.Note("Kiểm tên NVL và tiêu đề đơn; ghi chú đơn hàng, lý do từ chối, tên đính kèm chưa kiểm trên UI");
        p.Done();
    }

    private static readonly Dictionary<string, string[]> RoleRoutes = new()
    {
        ["Admin"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/admin/orders", "/admin/orders/new", "/admin/products", "/admin/customers", "/admin/employees", "/teams", "/hr/grades", "/admin/audit-logs", "/admin/vehicles", "/admin/meal-settings", "/admin/piece-rates", "/operations", "/order-line-teams", "/manager/output-records", "/manager/defect-records", "/manager/qc-batches", "/kpi/targets", "/suppliers", "/purchase-orders", "/admin/revenue", "/admin/break-even", "/admin/cost-summary", "/attendance", "/payrolls/approval", "/cutting/work-orders", "/warehouse/materials", "/warehouse/material-receipts", "/warehouse/short-deliveries", "/warehouse/inventories", "/warehouse/stocktakes", "/warehouse/dispatch-notes", "/my/payslips" },
        ["Manager"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/manager/orders", "/manager/employees", "/teams", "/hr/grades", "/kpi/targets", "/order-line-teams", "/manager/output-records", "/manager/defect-records", "/manager/qc-batches", "/purchase-orders", "/attendance", "/my/attendance", "/my/meals", "/my/payslips", "/cutting/work-orders", "/warehouse/materials", "/warehouse/material-receipts", "/warehouse/short-deliveries", "/warehouse/inventories", "/warehouse/stocktakes", "/warehouse/dispatch-notes" },
        ["Accountant"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/accounting/products", "/operations", "/attendance", "/payrolls", "/purchase-payments", "/accounting/order-payments", "/accounting/vehicle-log", "/monthly-costs", "/revenue", "/accountant/accounting-reports", "/meals/summary", "/warehouse/dispatch-notes", "/my/attendance", "/my/meals", "/my/payslips" },
        ["WarehouseKeeper"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/warehouse/materials", "/warehouse/incoming-materials", "/warehouse/material-receipts", "/warehouse/material-issues", "/warehouse/material-returns", "/warehouse/short-deliveries", "/warehouse/inventories", "/warehouse/stocktakes", "/warehouse/dispatch-notes", "/warehouse/fg-handovers", "/cutting/work-orders", "/cutting/handovers", "/my/attendance", "/my/meals", "/my/payslips" },
        ["TeamLeader"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/team/attendance", "/team/meals", "/operation-assignments", "/output-records", "/defect-records", "/warehouse/material-issues", "/warehouse/piece-movements", "/warehouse/piece-holdings", "/hr/grades", "/my/attendance", "/my/payslips" },
        ["TeamDeputy"] = new[] { "/", "/profile", "/notifications", "/team/attendance", "/team/meals", "/operation-assignments", "/output-records", "/defect-records", "/warehouse/material-issues", "/warehouse/piece-movements", "/warehouse/piece-holdings", "/hr/grades", "/my/attendance", "/my/payslips" },
        ["QaQc"] = new[] { "/", "/profile", "/notifications", "/requests", "/qc-batches", "/warehouse/fg-handovers", "/my/attendance", "/my/meals", "/my/payslips" },
        ["Worker"] = new[] { "/", "/profile", "/notifications", "/requests", "/my/attendance", "/my/meals", "/my/payslips", "/my/estimated-pay", "/my-piece-movements", "/my-defects" },
        ["Electrician"] = new[] { "/", "/profile", "/notifications", "/requests", "/requests/inbox", "/my/attendance", "/my/meals", "/my/payslips" },
    };

    [Fact]
    public async Task NFR_G3_050_EachRoleOpensOwnRoutes()
    {
        CaseId = "NFR_G3_050";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        foreach (var (role, routes) in RoleRoutes)
        {
            var page = await OpenAs(role);
            var w = Observe(page);
            foreach (var route in routes)
            {
                w.Console.Clear();
                w.BadResponses.Clear();
                var loaded = await Settle(page, route);
                var onRoute = PathOf(page) == route;
                var rendered = (await BodyText(page)).Trim().Length > 0;
                var problems = new List<string>();
                if (!loaded) problems.Add("không tải xong");
                if (!onRoute) problems.Add($"bị chuyển sang {PathOf(page)}");
                if (!rendered) problems.Add("trang trắng");
                if (w.BadResponses.Count > 0) problems.Add(string.Join(", ", w.BadResponses.Distinct().Take(2)));
                if (w.Console.Count > 0) problems.Add($"{w.Console.Count} lỗi Console: {w.Console[0][..Math.Min(80, w.Console[0].Length)]}");
                p.Check(problems.Count == 0, $"{role} {route}: {string.Join("; ", problems)}");
                if (problems.Count > 0) await Shot(page, CaseId);
            }
            await page.CloseAsync();
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_051_WorkerDirectUrlIsBlocked()
    {
        CaseId = "NFR_G3_051";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        var page = await OpenAs("Worker");
        var w = Observe(page);
        foreach (var route in new[] { "/admin/orders", "/payrolls/approval", "/admin/audit-logs", "/admin/employees", "/payrolls", "/revenue", "/admin/break-even", "/accounting/order-payments", "/purchase-payments" })
        {
            w.Requests.Clear();
            await Settle(page, route);
            var at = PathOf(page);
            p.Check(at is "/" or "/403", $"{route}: phải về trang 403/trang chủ, đang ở {at}");
            var leaked = w.Requests.Where(r => r.StartsWith("200") && (r.Contains("/api/v1/admin/") || r.Contains("/api/v1/accountant/"))).ToList();
            p.Check(leaked.Count == 0, $"{route}: có response 200 {string.Join(", ", leaked.Take(2))}");
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_052_ProfileAndPayslips_OwnDataOnly()
    {
        CaseId = "NFR_G3_052";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        foreach (var (role, user) in Acc.Roles)
        {
            var page = await OpenAs(role);
            var w = Observe(page);
            await Settle(page, "/profile");
            var profile = await BodyText(page);
            var inputs = await page.EvaluateAsync<string>("() => Array.from(document.querySelectorAll('input')).map(i => i.value).join(' ')");
            p.Check(Regex.IsMatch(profile + " " + inputs, $@"(?<![A-Z0-9]){user}(?![0-9])"), $"{role} /profile phải hiện mã {user} của chính mình");
            await Settle(page, "/my/payslips");
            var ok = PathOf(page) == "/my/payslips" && w.BadResponses.Count == 0 && w.Console.Count == 0;
            p.Check(ok, $"{role} /my/payslips: ở {PathOf(page)}; {string.Join(", ", w.BadResponses.Take(2))}; Console {w.Console.Count} ({w.Console.FirstOrDefault()})");
            if (!ok) await Shot(page, CaseId);
            await page.CloseAsync();
        }
        p.Done();
    }

    [Fact]
    public async Task NFR_G3_053_GuardsRedirectToLogin()
    {
        CaseId = "NFR_G3_053";
        ClearShot(CaseId);
        var p = new Probe(CaseId);
        var anonymous = await NewAnonymousPageAsync();
        foreach (var route in RoleRoutes.Values.SelectMany(r => r).Distinct())
        {
            await Settle(anonymous, route);
            var at = PathOf(anonymous);
            p.Check(at == "/login", $"chưa đăng nhập {route}: đang ở {at}");
        }

        // Đăng nhập riêng cho ca này (không đụng phiên đã cache), xem dữ liệu rồi xóa token giữa phiên
        var page = await NewAnonymousPageAsync();
        await LoginUiAsync(page, Acc.Admin, Acc.Password);
        await Settle(page, "/admin/employees");
        await Assertions.Expect(page.GetByText("CNCAT1", new() { Exact = true }).First).ToBeVisibleAsync();
        await page.EvaluateAsync("() => { localStorage.removeItem('nxmtd.accessToken'); localStorage.removeItem('nxmtd.refreshToken'); }");
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "Tổ", Exact = false }).Filter(new() { HasTextRegex = new Regex("^Tổ$") }).ClickAsync();
        var redirected = true;
        try { await page.WaitForURLAsync(u => u.Contains("/login"), new() { Timeout = 10_000 }); }
        catch (TimeoutException) { redirected = false; }
        p.Check(redirected, $"xóa token rồi chuyển trang: phải về /login, đang ở {PathOf(page)}");
        var stale = await page.GetByText("CNCAT1", new() { Exact = true }).CountAsync();
        p.Check(stale == 0, "không còn hiển thị dữ liệu của phiên trước");
        if (!redirected || stale > 0) await Shot(page, CaseId);
        p.Done();
    }
}
