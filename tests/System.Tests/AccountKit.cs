using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace System.Tests;

public sealed record TempAccount(string Code, string Username, string TempPassword);

public static class AccountKit
{
    public const string KnownPassword = "StAuto@2026";
    public static readonly ConcurrentBag<string> Created = new();
    public static readonly ConcurrentBag<string> Teams = new();
    private static int _seq;

    public static async Task<TempAccount> CreateAsync(IPage admin)
    {
        var n = Interlocked.Increment(ref _seq);
        var code = $"ST_AUTO_{DateTime.UtcNow:mmssff}{n}";
        await admin.GotoAsync("/admin/employees");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm nhân viên văn phòng" }).ClickAsync();
        var form = admin.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Mã nhân viên" });
        await form.GetByLabel("Mã nhân viên", new() { Exact = true }).FillAsync(code);
        await form.GetByLabel("Họ tên", new() { Exact = true }).FillAsync("Kiem Thu Tu Dong");
        await form.GetByLabel("Vai trò", new() { Exact = true }).ClickAsync();
        await admin.Locator(".ant-select-item-option[title='Thủ kho']").ClickAsync();
        await form.GetByRole(AriaRole.Button, new() { Name = "Lưu" }).ClickAsync();
        var temp = admin.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Mật khẩu tạm" });
        await temp.Locator("h2").WaitForAsync();
        var username = (await temp.Locator("strong").First.InnerTextAsync()).Trim();
        var password = (await temp.Locator("h2").InnerTextAsync()).Trim();
        await temp.GetByRole(AriaRole.Button).Last.ClickAsync();
        Created.Add(code);
        return new TempAccount(code, username, password);
    }

    public static async Task<TempAccount> ResetPasswordAsync(IPage admin, TempAccount a)
    {
        await admin.GotoAsync("/admin/employees");
        await admin.GetByPlaceholder("Tìm theo mã, tên...").FillAsync(a.Code);
        var row = admin.GetByRole(AriaRole.Row).Filter(new() { HasText = a.Code });
        await row.GetByRole(AriaRole.Button, new() { Name = "Xem" }).ClickAsync();
        await admin.GetByRole(AriaRole.Button, new() { Name = "Đặt lại mật khẩu" }).ClickAsync();
        await admin.GetByRole(AriaRole.Button, new() { Name = "Xác nhận" }).Last.ClickAsync();
        var temp = admin.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Mật khẩu tạm" });
        await temp.Locator("h2").WaitForAsync();
        var password = (await temp.Locator("h2").InnerTextAsync()).Trim();
        await temp.GetByRole(AriaRole.Button).Last.ClickAsync();
        return a with { TempPassword = password };
    }

    public static async Task ActivateAsync(IPage page, TempAccount a, string newPassword = KnownPassword)
    {
        await page.GotoAsync("/login");
        await BaseSystemTest.FillLoginAsync(page, a.Username, a.TempPassword);
        await page.GetByLabel("Mật khẩu mới", new() { Exact = true }).FillAsync(newPassword);
        await page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true }).FillAsync(newPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Đổi mật khẩu" }).ClickAsync();
        await Assertions.Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToHaveCountAsync(0);
    }

    public static async Task ResignAsync(IPage admin, string code)
    {
        await admin.GotoAsync("/admin/employees");
        await admin.GetByPlaceholder("Tìm theo mã, tên...").FillAsync(code);
        var row = admin.GetByRole(AriaRole.Row).Filter(new() { HasText = code });
        await row.GetByRole(AriaRole.Button, new() { Name = "Xem" }).ClickAsync();
        await admin.GetByRole(AriaRole.Button, new() { Name = "Cho nghỉ việc" }).ClickAsync();
        await admin.GetByLabel("Lý do nghỉ việc").FillAsync("ST_AUTO_ don dep du lieu kiem thu");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Xác nhận" }).ClickAsync();
        await Assertions.Expect(admin.GetByLabel("Lý do nghỉ việc")).ToHaveCountAsync(0);
    }
}

public sealed class CleanupFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (AccountKit.Created.IsEmpty && AccountKit.Teams.IsEmpty) return;
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync();
        var ctx = await browser.NewContextAsync(new() { BaseURL = BaseSystemTest.BaseUrl, Locale = "vi-VN" });
        var page = await ctx.NewPageAsync();
        page.SetDefaultTimeout(10000);
        var (u, p) = BaseSystemTest.GetAccount("Admin");
        await BaseSystemTest.LoginUiAsync(page, u, p);
        foreach (var code in AccountKit.Created.Distinct())
        {
            try { await AccountKit.ResignAsync(page, code); } catch { }
        }
        foreach (var team in AccountKit.Teams.Distinct())
        {
            try
            {
                var row = await UiKit.TeamRowAsync(page, team);
                if (await UiKit.Button(row, "Ngừng").CountAsync() == 0) continue;
                await UiKit.Button(row, "Ngừng").ClickAsync();
                await UiKit.ConfirmAsync(page);
                await Assertions.Expect(UiKit.Button(row, "Dùng lại")).ToBeVisibleAsync();
            }
            catch { }
        }
    }
}
