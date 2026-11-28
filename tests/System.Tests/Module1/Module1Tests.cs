using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;

namespace System.Tests.Module1;

public class Module1Tests : BaseSystemTest, IClassFixture<CleanupFixture>
{
    private static readonly SemaphoreSlim SharedLock = new(1, 1);
    private static TempAccount? _shared;

    private async Task<TempAccount> NewActiveAccountAsync()
    {
        var admin = await NewPageAsRoleAsync("Admin");
        var acc = await AccountKit.CreateAsync(admin);
        var page = await NewAnonymousPageAsync();
        await AccountKit.ActivateAsync(page, acc);
        return acc with { TempPassword = AccountKit.KnownPassword };
    }

    private async Task<TempAccount> SharedAccountAsync()
    {
        await SharedLock.WaitAsync();
        try { return _shared ??= await NewActiveAccountAsync(); }
        finally { SharedLock.Release(); }
    }

    private async Task<IPage> LoggedInAsync(TempAccount acc)
    {
        var page = await NewAnonymousPageAsync();
        await LoginUiAsync(page, acc.Username, acc.TempPassword);
        return page;
    }

    private static ILocator Home(IPage page) => page.GetByLabel("Tìm chức năng");

    [Fact]
    public async Task ST_M1_001()
    {
        CaseId = "ST_M1_001";
        var (u, p) = GetAccount("Admin");
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        var login = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, u, p),
            r => r.Url.Contains("/common/auth/login"));
        login.Status.Should().Be(200);
        await page.WaitForURLAsync(url => !url.Contains("/login"));
        PathOf(page).Should().Be("/");
        await Expect(Home(page)).ToBeVisibleAsync();
        await Expect(page.Locator("button.app-user")).ToBeVisibleAsync();
        var me = await page.RunAndWaitForResponseAsync(() => page.ReloadAsync(), r => r.Url.Contains("/common/auth/me"));
        me.Status.Should().Be(200);
        PathOf(page).Should().Be("/");
        await Expect(Home(page)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_002()
    {
        CaseId = "ST_M1_002";
        var (u, p) = GetAccount("Admin");
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/profile");
        await page.WaitForURLAsync(url => url.Contains("/login"));
        var prof = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, u, p),
            r => r.Url.Contains("/common/profile") && r.Request.Method == "GET");
        prof.Status.Should().Be(200);
        PathOf(page).Should().Be("/profile");
    }

    [Theory]
    [InlineData("ST_M1_003", "Admin", "admin")]
    [InlineData("ST_M1_003", "Manager", "manager")]
    [InlineData("ST_M1_003", "Accountant", "accountant")]
    [InlineData("ST_M1_003", "WarehouseKeeper", "warehouse-keeper")]
    [InlineData("ST_M1_003", "TeamLeader", "team-leader")]
    [InlineData("ST_M1_003", "TeamDeputy", "team-leader")]
    [InlineData("ST_M1_003", "QaQc", "qa-qc")]
    [InlineData("ST_M1_003", "Electrician", "electrician")]
    [InlineData("ST_M1_003", "Worker", "worker")]
    public async Task ST_M1_003(string caseId, string role, string rolePath)
    {
        CaseId = caseId;
        var (u, p) = GetAccount(role);
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        var home = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, u, p),
            r => r.Url.Contains($"/api/v1/{rolePath}/home"));
        home.Status.Should().Be(200);
        await Expect(Home(page)).ToBeVisibleAsync();
        await Expect(page.GetByText("Không tải được dữ liệu.")).ToHaveCountAsync(0);
        await page.Locator("button.app-user").ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/login"));
        InvalidateRole(role);
    }

    [Fact]
    public async Task ST_M1_004()
    {
        CaseId = "ST_M1_004";
        var acc = await SharedAccountAsync();
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        var resp = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, acc.Username, "Sai@matkhau1"),
            r => r.Url.Contains("/common/auth/login"));
        resp.Status.Should().BeInRange(400, 499);
        await Expect(page.Locator(".ant-alert-error")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" })).ToBeEnabledAsync();
        PathOf(page).Should().Be("/login");
        await page.ReloadAsync();
        PathOf(page).Should().Be("/login");
        await Expect(page.GetByLabel("Tên đăng nhập")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_005()
    {
        CaseId = "ST_M1_005";
        var page = await NewAnonymousPageAsync();
        var posts = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/common/auth/login") && r.Method == "POST") posts++; };
        await page.GotoAsync("/login");
        await page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" }).ClickAsync();
        await Expect(page.GetByText("Vui lòng nhập tên đăng nhập.")).ToBeVisibleAsync();
        await Expect(page.GetByText("Vui lòng nhập mật khẩu.")).ToBeVisibleAsync();
        await page.GetByLabel("Tên đăng nhập").FillAsync("   ");
        await page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" }).ClickAsync();
        await Expect(page.GetByText("Vui lòng nhập tên đăng nhập.")).ToBeVisibleAsync();
        posts.Should().Be(0);
    }

    [Fact]
    public async Task ST_M1_006()
    {
        CaseId = "ST_M1_006";
        var acc = await SharedAccountAsync();
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        string? body = null;
        page.Request += (_, r) => { if (r.Url.Contains("/common/auth/login") && r.Method == "POST") body = r.PostData; };
        await FillLoginAsync(page, $"  {acc.Username}  ", acc.TempPassword);
        await page.WaitForURLAsync(url => !url.Contains("/login"));
        await Expect(Home(page)).ToBeVisibleAsync();
        body.Should().NotBeNull();
        body!.Should().Contain($"\"username\":\"{acc.Username}\"");
    }

    [Fact]
    public async Task ST_M1_007()
    {
        CaseId = "ST_M1_007";
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        var resp = await page.RunAndWaitForResponseAsync(
            () => FillLoginAsync(page, new string('u', 300), new string('p', 51)),
            r => r.Url.Contains("/common/auth/login"));
        resp.Status.Should().BeInRange(400, 499);
        await Expect(page.Locator(".ant-alert-error")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Đăng nhập" })).ToBeEnabledAsync();
        PathOf(page).Should().Be("/login");
    }

    [Fact]
    public async Task ST_M1_008()
    {
        CaseId = "ST_M1_008";
        var acc = await NewActiveAccountAsync();
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        for (var i = 0; i < 6; i++)
        {
            var r = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, acc.Username, $"Sai@matkhau{i}"),
                x => x.Url.Contains("/common/auth/login"));
            r.Status.Should().BeInRange(400, 499);
            await Expect(page.Locator(".ant-alert-error")).ToBeVisibleAsync();
        }
        var last = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, acc.Username, acc.TempPassword),
            x => x.Url.Contains("/common/auth/login"));
        if (last.Status == 200)
        {
            await page.WaitForURLAsync(url => !url.Contains("/login"));
        }
        else
        {
            last.Status.Should().BeInRange(400, 499);
            await Expect(page.Locator(".ant-alert-error")).ToBeVisibleAsync();
            PathOf(page).Should().Be("/login");
        }
    }

    [Fact]
    public async Task ST_M1_009()
    {
        CaseId = "ST_M1_009";
        var page = await NewPageAsRoleAsync("Admin");
        await GotoAndWaitAsync(page, "/");
        await page.GotoAsync("/login");
        await page.WaitForURLAsync(url => !url.Contains("/login"));
        PathOf(page).Should().Be("/");
        await Expect(page.GetByLabel("Tên đăng nhập")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("ST_M1_010", "/admin/employees")]
    [InlineData("ST_M1_010", "/payrolls/approval")]
    [InlineData("ST_M1_010", "/admin/audit-logs")]
    public async Task ST_M1_010(string caseId, string url)
    {
        CaseId = caseId;
        var page = await NewPageAsRoleAsync("Worker");
        await GotoAndWaitAsync(page, "/");
        var adminCalls = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/api/v1/admin/")) adminCalls++; };
        await page.GotoAsync(url);
        await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        PathOf(page).Should().Be("/");
        adminCalls.Should().Be(0);
        var token = await page.EvaluateAsync<string>("localStorage.getItem('nxmtd.accessToken')");
        var api = await page.Context.APIRequest.GetAsync($"{BaseUrl}/api/v1/admin/employees",
            new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } });
        api.Status.Should().Be(403);
    }

    [Theory]
    [InlineData("ST_M1_011", "/operation-assignments")]
    [InlineData("ST_M1_011", "/output-records")]
    public Task ST_M1_011(string caseId, string url)
    {
        CaseId = caseId;
        throw new InvalidOperationException(
            $"Không thực hiện được: danh sách tài khoản không có TeamLeader thuộc tổ cắt để mở {url}");
    }

    [Fact]
    public async Task ST_M1_012()
    {
        CaseId = "ST_M1_012";
        var page = await NewPageAsRoleAsync("Admin");
        await GotoAndWaitAsync(page, "/abc/xyz");
        await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        await Expect(Home(page)).ToBeVisibleAsync();
        await Expect(page.GetByText("Không tải được dữ liệu.")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ST_M1_013()
    {
        CaseId = "ST_M1_013";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await GotoAndWaitAsync(page, "/profile");
        await page.Locator("button.app-user").ClickAsync();
        var logout = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync(),
            r => r.Url.Contains("/common/auth/logout"));
        logout.Status.Should().BeInRange(200, 299);
        await page.WaitForURLAsync(u => u.Contains("/login"));
        await page.GoBackAsync();
        await page.WaitForURLAsync(u => u.Contains("/login"));
        await page.ReloadAsync();
        PathOf(page).Should().Be("/login");
        await Expect(page.GetByLabel("Tên đăng nhập")).ToBeVisibleAsync();
        (await page.EvaluateAsync<string?>("localStorage.getItem('nxmtd.accessToken')")).Should().BeNull();
    }

    [Fact]
    public async Task ST_M1_014()
    {
        CaseId = "ST_M1_014";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await page.EvaluateAsync("localStorage.setItem('nxmtd.accessToken','invalid'); localStorage.setItem('nxmtd.refreshToken','invalid')");
        var refreshCalls = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/common/auth/refresh")) refreshCalls++; };
        await page.GotoAsync("/profile");
        await page.WaitForURLAsync(u => u.Contains("/login"));
        await Expect(page.GetByLabel("Tên đăng nhập")).ToBeVisibleAsync();
        refreshCalls.Should().BeLessOrEqualTo(2);
        (await page.EvaluateAsync<string?>("localStorage.getItem('nxmtd.accessToken')")).Should().BeNull();
    }

    private static async Task OpenEditAsync(IPage page)
    {
        await page.GotoAsync("/profile");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sửa" }).ClickAsync();
    }

    private static ILocator ProfileModal(IPage page) =>
        page.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Cập nhật hồ sơ" });

    [Fact]
    public async Task ST_M1_015()
    {
        CaseId = "ST_M1_015";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        await modal.GetByLabel("Ngày sinh").FillAsync("15/03/2000");
        await modal.GetByLabel("Ngày sinh").PressAsync("Enter");
        await modal.GetByLabel("Số điện thoại").FillAsync("0912345678");
        var put = await page.RunAndWaitForRequestAsync(
            () => modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync(),
            r => r.Url.Contains("/common/profile") && r.Method == "PUT");
        put.PostData.Should().Contain("\"dateOfBirth\":\"2000-03-15\"").And.Contain("\"phone\":\"0912345678\"");
        await Expect(page.Locator(".ant-message-notice").Filter(new() { HasText = "Đã lưu." })).ToBeVisibleAsync();
        await Expect(modal).ToHaveCountAsync(0);
        await Expect(page.GetByText("15/03/2000")).ToBeVisibleAsync();
        await Expect(page.GetByText("0912345678")).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByText("15/03/2000")).ToBeVisibleAsync();
        await Expect(page.GetByText("0912345678")).ToBeVisibleAsync();
        var again = await LoggedInAsync(acc);
        await again.GotoAsync("/profile");
        await Expect(again.GetByText("0912345678")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_016()
    {
        CaseId = "ST_M1_016";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        var phone = modal.GetByLabel("Số điện thoại");
        const string invalid = "Số điện thoại không hợp lệ (10 số bắt đầu bằng 0, hoặc +84 và 9 số).";
        await phone.FillAsync("0912345678");
        await Expect(modal.GetByText(invalid)).ToHaveCountAsync(0);
        await phone.FillAsync("+84912345678");
        await Expect(modal.GetByText(invalid)).ToHaveCountAsync(0);
        await phone.FillAsync("091234567");
        await Expect(modal.GetByText(invalid)).ToBeVisibleAsync();
        await phone.FillAsync("+8491234567");
        await Expect(modal.GetByText(invalid)).ToBeVisibleAsync();
        await phone.FillAsync("abc");
        await Expect(phone).ToHaveValueAsync("");
        await phone.FillAsync("09123456789012345");
        (await phone.InputValueAsync()).Length.Should().BeLessOrEqualTo(12);
        var puts = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/common/profile") && r.Method == "PUT") puts++; };
        await phone.FillAsync("091234567");
        await modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();
        await Expect(modal.GetByText(invalid)).ToBeVisibleAsync();
        puts.Should().Be(0);
        await phone.FillAsync("+84912345678");
        await modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();
        await Expect(modal).ToHaveCountAsync(0);
        await page.ReloadAsync();
        await Expect(page.GetByText("+84912345678")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_017()
    {
        CaseId = "ST_M1_017";
        var acc = await NewActiveAccountAsync();
        var page = await LoggedInAsync(acc);
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        await modal.GetByLabel("Số điện thoại").FillAsync("0912345678");
        await modal.GetByLabel("Ngày sinh").FillAsync("15/03/2000");
        await modal.GetByLabel("Ngày sinh").PressAsync("Enter");
        await modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();
        await Expect(modal).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sửa" }).ClickAsync();
        await modal.GetByLabel("Số điện thoại").FillAsync("");
        var put = await page.RunAndWaitForRequestAsync(
            () => modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync(),
            r => r.Url.Contains("/common/profile") && r.Method == "PUT");
        put.PostData.Should().Contain("\"phone\":null");
        await Expect(modal).ToHaveCountAsync(0);
        await Expect(page.GetByText("null")).ToHaveCountAsync(0);
        await page.ReloadAsync();
        await Expect(page.GetByText("0912345678")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sửa" }).ClickAsync();
        var dob = modal.GetByLabel("Ngày sinh");
        await dob.ClickAsync();
        await page.GetByText("Hôm nay", new() { Exact = true }).ClickAsync();
        await Expect(modal.GetByLabel("Ngày sinh")).ToHaveValueAsync(DateTime.Now.ToString("dd/MM/yyyy"));
        await dob.ClickAsync();
        var tomorrow = DateTime.Now.Date.AddDays(1);
        await Expect(page.Locator("td.ant-picker-cell-today")).Not.ToHaveClassAsync(new Regex("ant-picker-cell-disabled"));
        await Expect(page.Locator("td.ant-picker-cell-in-view.ant-picker-cell-disabled").First)
            .ToHaveTextAsync(tomorrow.Day.ToString());
    }

    [Fact]
    public async Task ST_M1_018()
    {
        CaseId = "ST_M1_018";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        var puts = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/common/profile") && r.Method == "PUT") puts++; };
        await modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();
        await Expect(modal).ToHaveCountAsync(0);
        await Expect(page.Locator(".ant-message-notice")).ToHaveCountAsync(0);
        puts.Should().Be(0);
    }

    private static async Task FillPasswordsAsync(ILocator modal, string? current, string? next, string? confirm)
    {
        if (current is not null) await modal.GetByLabel("Mật khẩu hiện tại", new() { Exact = true }).FillAsync(current);
        if (next is not null) await modal.GetByLabel("Mật khẩu mới", new() { Exact = true }).FillAsync(next);
        if (confirm is not null) await modal.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true }).FillAsync(confirm);
    }

    private static ILocator SaveButton(ILocator modal) => modal.GetByRole(AriaRole.Button, new() { Name = "Lưu" });

    private static ILocator Toast(IPage page, string text) =>
        page.Locator(".ant-message-notice").Filter(new() { HasText = text });

    [Fact]
    public async Task ST_M1_019()
    {
        CaseId = "ST_M1_019";
        var acc = await NewActiveAccountAsync();
        var a = await LoggedInAsync(acc);
        var b = await LoggedInAsync(acc);
        await GotoAndWaitAsync(b, "/profile");
        await OpenEditAsync(a);
        var modal = ProfileModal(a);
        const string next = "MoiMoi@2026x";
        await FillPasswordsAsync(modal, acc.TempPassword, next, next);
        var resp = await a.RunAndWaitForResponseAsync(() => SaveButton(modal).ClickAsync(),
            r => r.Url.Contains("/profile/change-password"));
        resp.Status.Should().Be(200);
        await Expect(Toast(a, "Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất.")).ToBeVisibleAsync();
        PathOf(a).Should().Be("/profile");
        await b.GotoAsync("/requests");
        await b.WaitForURLAsync(u => u.Contains("/login"));
        var old = await NewAnonymousPageAsync();
        await old.GotoAsync("/login");
        var oldResp = await old.RunAndWaitForResponseAsync(() => FillLoginAsync(old, acc.Username, acc.TempPassword),
            r => r.Url.Contains("/common/auth/login"));
        oldResp.Status.Should().BeInRange(400, 499);
        var fresh = await NewAnonymousPageAsync();
        await LoginUiAsync(fresh, acc.Username, next);
        await Expect(Home(fresh)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_020()
    {
        CaseId = "ST_M1_020";
        var acc = await NewActiveAccountAsync();
        var a = await LoggedInAsync(acc);
        var b = await LoggedInAsync(acc);
        await OpenEditAsync(a);
        var modal = ProfileModal(a);
        await FillPasswordsAsync(modal, "Sai@hienTai1", "MoiMoi@2026x", "MoiMoi@2026x");
        var resp = await a.RunAndWaitForResponseAsync(() => SaveButton(modal).ClickAsync(),
            r => r.Url.Contains("/profile/change-password"));
        resp.Status.Should().Be(409);
        await Expect(modal.GetByText("Mật khẩu hiện tại không đúng.")).ToBeVisibleAsync();
        await Expect(modal.GetByLabel("Mật khẩu mới", new() { Exact = true })).ToHaveValueAsync("MoiMoi@2026x");
        var again = await NewAnonymousPageAsync();
        await LoginUiAsync(again, acc.Username, acc.TempPassword);
        await b.ReloadAsync();
        PathOf(b).Should().NotBe("/login");
        await Expect(Home(b)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_021()
    {
        CaseId = "ST_M1_021";
        var acc = await NewActiveAccountAsync();
        var page = await LoggedInAsync(acc);
        var posts = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/profile/change-password") && r.Method == "POST") posts++; };
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        var p50 = new string('a', 49) + "1";
        await FillPasswordsAsync(modal, acc.TempPassword, p50, p50);
        await SaveButton(modal).ClickAsync();
        await Expect(Toast(page, "Đã đổi mật khẩu")).ToBeVisibleAsync();
        await Expect(modal).ToHaveCountAsync(0);
        posts.Should().Be(1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Sửa" }).ClickAsync();
        var p51 = new string('b', 50) + "1";
        await FillPasswordsAsync(modal, p50, p51, p51);
        await SaveButton(modal).ClickAsync();
        await Expect(modal.GetByText("Mật khẩu tối đa 50 ký tự.")).ToBeVisibleAsync();

        await FillPasswordsAsync(modal, p50, "KhopKhop@1", "KhongKhop@2");
        await SaveButton(modal).ClickAsync();
        await Expect(modal.GetByText("Mật khẩu nhập lại không khớp.")).ToBeVisibleAsync();

        await modal.GetByLabel("Mật khẩu hiện tại", new() { Exact = true }).FillAsync("");
        await FillPasswordsAsync(modal, null, "KhopKhop@1", "KhopKhop@1");
        await SaveButton(modal).ClickAsync();
        await Expect(modal.GetByText("Vui lòng nhập thông tin này.")).ToBeVisibleAsync();
        posts.Should().Be(1);

        var fresh = await NewAnonymousPageAsync();
        await LoginUiAsync(fresh, acc.Username, p50);
        await Expect(Home(fresh)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_022()
    {
        CaseId = "ST_M1_022";
        var acc = await NewActiveAccountAsync();
        var page = await LoggedInAsync(acc);
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        await FillPasswordsAsync(modal, acc.TempPassword, "a", "a");
        await SaveButton(modal).ClickAsync();
        var ok = Toast(page, "Đã đổi mật khẩu");
        var err = page.Locator(".ant-message-error, .ant-form-item-explain-error");
        await Expect(ok.Or(err).First).ToBeVisibleAsync();
        await Expect(SaveButton(modal).Or(page.Locator(".ant-message-notice")).First).ToBeVisibleAsync();
        await Expect(modal.Locator(".ant-btn-loading")).ToHaveCountAsync(0);
        if (await ok.CountAsync() > 0)
        {
            var fresh = await NewAnonymousPageAsync();
            await LoginUiAsync(fresh, acc.Username, "a");
            await Expect(Home(fresh)).ToBeVisibleAsync();
        }
        else
        {
            await Expect(err.First).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task ST_M1_023()
    {
        CaseId = "ST_M1_023";
        var acc = await NewActiveAccountAsync();
        var page = await LoggedInAsync(acc);
        await page.RouteAsync("**/api/v1/common/profile", async route =>
        {
            if (route.Request.Method == "PUT")
                await route.FulfillAsync(new() { Status = 500, ContentType = "application/json",
                    Body = "{\"success\":false,\"message\":\"Loi gia lap\"}" });
            else await route.ContinueAsync();
        });
        await OpenEditAsync(page);
        var modal = ProfileModal(page);
        const string next = "MoiMoi@2026x";
        await FillPasswordsAsync(modal, acc.TempPassword, next, next);
        await modal.GetByLabel("Số điện thoại").FillAsync("0987654321");
        await SaveButton(modal).ClickAsync();
        await Expect(Toast(page, "Đã đổi mật khẩu")).ToBeVisibleAsync();
        await Expect(page.Locator(".ant-message-error")).ToBeVisibleAsync();
        await Expect(modal).ToBeVisibleAsync();
        await page.UnrouteAllAsync();
        await page.ReloadAsync();
        await Expect(page.GetByText("0987654321")).ToHaveCountAsync(0);
        var fresh = await NewAnonymousPageAsync();
        await LoginUiAsync(fresh, acc.Username, next);
        await Expect(Home(fresh)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_024()
    {
        CaseId = "ST_M1_024";
        var admin = await NewPageAsRoleAsync("Admin");
        var created = await AccountKit.CreateAsync(admin);
        var acc = await AccountKit.ResetPasswordAsync(admin, created);
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        await FillLoginAsync(page, acc.Username, acc.TempPassword);
        var notice = page.GetByText(new Regex("mật khẩu tạm"));
        await Expect(notice.First).ToBeVisibleAsync();
        await Expect(page.GetByLabel("Mật khẩu hiện tại", new() { Exact = true })).ToHaveCountAsync(0);
        foreach (var path in new[] { "/profile", "/requests" })
        {
            await page.GotoAsync(path);
            await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByLabel("Mật khẩu hiện tại", new() { Exact = true })).ToHaveCountAsync(0);
        }
        var me = await page.RunAndWaitForResponseAsync(() => page.ReloadAsync(), r => r.Url.Contains("/common/auth/me"));
        (await me.TextAsync()).Should().Contain("\"isTemporaryPassword\":true");
        await page.GetByLabel("Mật khẩu mới", new() { Exact = true }).FillAsync(AccountKit.KnownPassword);
        await page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true }).FillAsync(AccountKit.KnownPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Đổi mật khẩu" }).ClickAsync();
        await Expect(Home(page)).ToBeVisibleAsync();
        var me2 = await page.RunAndWaitForResponseAsync(() => page.ReloadAsync(), r => r.Url.Contains("/common/auth/me"));
        (await me2.TextAsync()).Should().Contain("\"isTemporaryPassword\":false");
        await Expect(Home(page)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M1_025()
    {
        CaseId = "ST_M1_025";
        var admin = await NewPageAsRoleAsync("Admin");
        var acc = await AccountKit.CreateAsync(admin);
        var page = await NewAnonymousPageAsync();
        await page.GotoAsync("/login");
        await FillLoginAsync(page, acc.Username, acc.TempPassword);
        await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Đăng xuất" }).ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/login"));
        await page.GoBackAsync();
        await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToHaveCountAsync(0);
        await page.GotoAsync("/login");
        await FillLoginAsync(page, acc.Username, acc.TempPassword);
        await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync("/");
        await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Home(page)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ST_M1_026()
    {
        CaseId = "ST_M1_026";
        var admin = await NewPageAsRoleAsync("Admin");
        await GotoAndWaitAsync(admin, "/");
        await admin.Keyboard.PressAsync("Control+K");
        await Home(admin).FillAsync("nhan vien");
        var option = admin.Locator(".ant-select-item-option").Filter(new() { HasText = "Nhân viên" });
        await Expect(option.First).ToBeVisibleAsync();
        await option.First.ClickAsync();
        await admin.WaitForURLAsync(u => u.Contains("/admin/employees"));

        var worker = await NewPageAsRoleAsync("Worker");
        var denied = 0;
        worker.Response += (_, r) => { if (r.Status is 401 or 403) denied++; };
        await GotoAndWaitAsync(worker, "/");
        foreach (var term in new[] { "nhan vien", "duyet luong" })
        {
            await worker.Keyboard.PressAsync("Control+K");
            await Home(worker).FillAsync(term);
            await Expect(worker.Locator(".ant-select-item-option").Filter(new() { HasText = "Nhân viên" })).ToHaveCountAsync(0);
            await Expect(worker.Locator(".ant-select-item-option").Filter(new() { HasText = "Duyệt bảng lương" })).ToHaveCountAsync(0);
        }
        denied.Should().Be(0);
    }

    [Fact]
    public async Task ST_M1_027()
    {
        CaseId = "ST_M1_027";
        var acc = await SharedAccountAsync();
        var page = await LoggedInAsync(acc);
        await GotoAndWaitAsync(page, "/profile");
        var code = (await page.Locator("th.ant-descriptions-item-label", new() { HasTextRegex = new Regex("^Mã nhân viên$") })
            .Locator("xpath=following-sibling::td[1]").InnerTextAsync()).Trim();
        await page.Locator("button.app-user").ClickAsync();
        var shown = (await page.Locator(".app-user-pop__meta").Filter(new() { HasText = "Mã NV" }).InnerTextAsync()).Trim();
        shown.Should().Be($"Mã NV: {code}");
    }
}
