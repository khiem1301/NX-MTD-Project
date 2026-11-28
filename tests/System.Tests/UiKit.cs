using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Playwright;

namespace System.Tests;

public static class UiKit
{
    private static int _seq = (int)(DateTime.UtcNow.Ticks / 10_000_000 % 100_000);

    public static string Next() => (Interlocked.Increment(ref _seq) % 100_000).ToString("00000");
    public static string Code(string tag = "") => $"ST_AUTO_{tag}{Next()}";

    public static string PersonName()
    {
        var letters = new string(Guid.NewGuid().ToString("N").Take(7).Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());
        return $"Kiem Thu {char.ToUpper(letters[0])}{letters[1..]}";
    }

    public static string Today => DateTime.Now.ToString("dd/MM/yyyy");

    public static ILocator Dialog(IPage page, string name) =>
        page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("^" + Regex.Escape(name)) });

    public static ILocator Toast(IPage page, string text) =>
        page.Locator(".ant-message-notice").Filter(new() { HasText = text });

    public static ILocator Errors(IPage page) =>
        page.Locator(".ant-message-error, .ant-form-item-explain-error, .ant-notification-notice-error, .ant-alert-error");

    public static ILocator Options(IPage page) => page.Locator(".ant-select-dropdown:visible .ant-select-item-option");

    public static async Task SelectAsync(IPage page, ILocator combobox, string text)
    {
        await combobox.ClickAsync();
        if (await combobox.IsEditableAsync()) await combobox.FillAsync(text);
        await Options(page).Filter(new() { HasText = text }).First.ClickAsync();
    }

    public static async Task DateAsync(ILocator input, string value)
    {
        await input.ClickAsync();
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    public static ILocator Row(ILocator scope, string key) =>
        scope.GetByRole(AriaRole.Row).Filter(new() { HasText = key });

    public static ILocator Row(IPage page, string key) =>
        page.GetByRole(AriaRole.Row).Filter(new() { HasText = key });

    public static ILocator Button(ILocator scope, string name) =>
        scope.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public static ILocator Button(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public static async Task ConfirmAsync(IPage page) => await Button(page, "Xác nhận").Last.ClickAsync();

    public static async Task PageSize100Async(IPage page)
    {
        await page.Locator(".ant-select").Filter(new() { HasText = "/ trang" }).First.ClickAsync();
        await Options(page).Filter(new() { HasText = "100 / trang" }).ClickAsync();
    }

    public static async Task SearchEmployeeAsync(IPage page, string keyword, string basePath = "/admin/employees")
    {
        await page.GotoAsync(basePath);
        await page.GetByPlaceholder("Tìm theo mã, tên...").FillAsync(keyword);
        await page.GetByPlaceholder("Tìm theo mã, tên...").PressAsync("Enter");
    }

    public static async Task<ILocator> OpenEmployeeAsync(IPage page, string code, string basePath = "/admin/employees")
    {
        await SearchEmployeeAsync(page, code, basePath);
        await Button(page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true }) }), "Xem").ClickAsync();
        var dlg = Dialog(page, code + " ·");
        await dlg.WaitForAsync();
        return dlg;
    }

    public static async Task<ILocator> OpenCreateEmployeeAsync(IPage page, string basePath = "/admin/employees")
    {
        await page.GotoAsync(basePath);
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm nhân viên văn phòng" }).ClickAsync();
        return Dialog(page, "Thêm nhân viên văn phòng");
    }

    public static async Task FillEmployeeAsync(IPage page, ILocator form, string code, string name,
        string role = "Thủ kho", string? start = null, string? attendance = null)
    {
        await form.GetByLabel("Mã nhân viên").FillAsync(code);
        await form.GetByLabel("Họ tên").FillAsync(name);
        await SelectAsync(page, form.GetByRole(AriaRole.Combobox, new() { Name = "Vai trò" }), role);
        if (attendance is not null) await form.GetByLabel("Mã chấm công").FillAsync(attendance);
        if (start is not null) await DateAsync(form.GetByLabel("Ngày vào làm"), start);
    }

    public static async Task<string> CreateTeamAsync(IPage page, string type = "Tổ may")
    {
        var code = Code("T");
        await page.GotoAsync("/teams");
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm tổ" }).ClickAsync();
        var dlg = Dialog(page, "Thêm tổ");
        await dlg.GetByLabel("Mã tổ").FillAsync(code);
        await SelectAsync(page, dlg.GetByRole(AriaRole.Combobox, new() { Name = "Loại tổ" }), type);
        await Button(dlg, "Lưu").ClickAsync();
        await Assertions.Expect(dlg).ToHaveCountAsync(0);
        AccountKit.Teams.Add(code);
        return code;
    }

    public static async Task<ILocator> TeamRowAsync(IPage page, string code)
    {
        await page.GotoAsync("/teams");
        await PageSize100Async(page);
        return page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true }) });
    }

    public static async Task<ILocator> OpenTeamAsync(IPage page, string code)
    {
        var row = await TeamRowAsync(page, code);
        await Button(row, "Xem").ClickAsync();
        var dlg = Dialog(page, code);
        await dlg.GetByRole(AriaRole.Tab).First.WaitForAsync();
        return dlg;
    }

    public static byte[] ImportWorkbook(IEnumerable<string?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("NV");
        var header = new[] { "Mã NV", "Họ tên", "Giới tính", "Ngày sinh", "SĐT", "Vai trò", "Mã tổ", "Hình thức lương", "Ngày vào làm", "Mã chấm công" };
        for (var c = 0; c < header.Length; c++) ws.Cell(1, c + 1).Value = header[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++) ws.Cell(r, c + 1).SetValue(row[c] ?? "");
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static string?[] WorkerRow(string code, string name, string? team, string role = "WarehouseKeeper") =>
        new[] { code, name, "Nam", "01/01/1995", "", role, team, "Monthly", "01/06/2026", "" };

    public static async Task<ILocator> UploadImportAsync(IPage page, byte[] content)
    {
        await page.GotoAsync("/admin/employees");
        await Button(page, "Nhập từ Excel").ClickAsync();
        var dlg = Dialog(page, "Nhập nhân viên từ Excel");
        await dlg.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "st_auto_nv.xlsx",
            MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Buffer = content,
        });
        return dlg;
    }

    public static async Task<TempAccount> CreateMemberAsync(IPage admin, string? team)
    {
        var code = Code("W");
        var form = await OpenCreateEmployeeAsync(admin);
        await FillEmployeeAsync(admin, form, code, PersonName(), start: "01/06/2026");
        if (team is not null) await SelectAsync(admin, form.GetByRole(AriaRole.Combobox, new() { Name = "Tổ" }), team);
        AccountKit.Created.Add(code);
        await Button(form, "Lưu").ClickAsync();
        var temp = Dialog(admin, "Mật khẩu tạm");
        var user = (await temp.Locator("strong").First.InnerTextAsync()).Trim();
        var pass = (await temp.GetByRole(AriaRole.Heading).Last.InnerTextAsync()).Trim();
        await temp.GetByRole(AriaRole.Button).Last.ClickAsync();
        return new TempAccount(code, user, pass);
    }
}
