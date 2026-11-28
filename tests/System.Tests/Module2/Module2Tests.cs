using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using static System.Tests.UiKit;

namespace System.Tests.Module2;

public class Module2Tests : BaseSystemTest, IClassFixture<CleanupFixture>
{
    private const string Required = "Vui lòng nhập thông tin này.";

    private async Task<IPage> AsAsync(string role)
    {
        var page = await NewPageAsRoleAsync(role);
        page.SetDefaultTimeout(15000);
        return page;
    }

    private async Task<IPage> AnonAsync()
    {
        var page = await NewAnonymousPageAsync();
        page.SetDefaultTimeout(15000);
        return page;
    }

    private async Task<IPage> LoggedInAsync(string user, string pass)
    {
        var page = await AnonAsync();
        await LoginUiAsync(page, user, pass);
        await Expect(page.GetByLabel("Tìm chức năng")).ToBeVisibleAsync();
        return page;
    }

    private async Task<TempAccount> ActivateAsync(TempAccount a)
    {
        var page = await AnonAsync();
        await AccountKit.ActivateAsync(page, a);
        return a with { TempPassword = AccountKit.KnownPassword };
    }

    private async Task<int> LoginStatusAsync(IPage page, string user, string pass)
    {
        await page.GotoAsync("/login");
        var r = await page.RunAndWaitForResponseAsync(() => FillLoginAsync(page, user, pass), x => x.Url.Contains("/common/auth/login"));
        return r.Status;
    }

    private static async Task<IResponse> SaveAsync(IPage page, ILocator dlg, string urlPart, string button = "Lưu") =>
        await page.RunAndWaitForResponseAsync(() => Button(dlg, button).ClickAsync(),
            r => r.Url.Contains(urlPart) && r.Request.Method != "GET" && r.Request.Method != "OPTIONS");

    private static async Task<string> TokenAsync(IPage page) =>
        await page.EvaluateAsync<string>("localStorage.getItem('nxmtd.accessToken')");

    private static async Task<IAPIResponse> ApiPostAsync(IPage page, string path, object body)
    {
        var token = await TokenAsync(page);
        return await page.Context.APIRequest.PostAsync(BaseUrl + path, new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = body,
        });
    }

    private async Task<(IPage Admin, TempAccount Account)> NewOfficeAsync()
    {
        var admin = await AsAsync("Admin");
        return (admin, await AccountKit.CreateAsync(admin));
    }

    [Fact]
    public async Task ST_M2_001()
    {
        CaseId = "ST_M2_001";
        var admin = await AsAsync("Admin");
        var code = Code("VP");
        var name = PersonName();
        var form = await OpenCreateEmployeeAsync(admin);
        await FillEmployeeAsync(admin, form, code, name, "Kế toán", attendance: "9" + Next() + "1");
        await SelectAsync(admin, form.GetByRole(AriaRole.Combobox, new() { Name = "Giới tính" }), "Nam");
        await DateAsync(form.GetByLabel("Ngày sinh"), "15/03/1990");
        await form.GetByLabel("Số điện thoại").FillAsync("0912345678");
        AccountKit.Created.Add(code);
        var resp = await SaveAsync(admin, form, "/admin/employees");
        resp.Status.Should().Be(201);
        await Expect(Toast(admin, "Đã thêm nhân viên và tạo tài khoản.")).ToBeVisibleAsync();
        var temp = Dialog(admin, "Mật khẩu tạm");
        var user = (await temp.Locator("strong").First.InnerTextAsync()).Trim();
        var pass = (await temp.GetByRole(AriaRole.Heading).Last.InnerTextAsync()).Trim();
        user.Should().NotBeEmpty();
        pass.Should().NotBeEmpty();
        await Expect(temp.GetByRole(AriaRole.Button, new() { Name = "Sao chép" })).ToBeVisibleAsync();
        await SearchEmployeeAsync(admin, code);
        await Expect(Row(admin, code)).ToContainTextAsync(Today);
        var page = await AnonAsync();
        (await LoginStatusAsync(page, user, pass)).Should().Be(200);
        await Expect(page.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M2_002()
    {
        CaseId = "ST_M2_002";
        var mgr = await AsAsync("Manager");
        await mgr.GotoAsync("/manager/employees");
        await mgr.Locator(".ant-select").Filter(new() { HasText = "Vai trò:" }).ClickAsync();
        await Expect(Options(mgr).First).ToBeVisibleAsync();
        var options = (await Options(mgr).AllInnerTextsAsync()).Select(t => t.Trim()).ToList();
        options.Should().NotContain("Admin").And.NotContain("Kế toán");
        await mgr.Keyboard.PressAsync("Escape");
        var code = Code("M");
        var form = await OpenCreateEmployeeAsync(mgr, "/manager/employees");
        await FillEmployeeAsync(mgr, form, code, PersonName());
        AccountKit.Created.Add(code);
        var resp = await SaveAsync(mgr, form, "/manager/employees");
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Toast(mgr, "Đã thêm nhân viên và tạo tài khoản.")).ToBeVisibleAsync();
        var admin = await AsAsync("Admin");
        await SearchEmployeeAsync(admin, code);
        await Expect(Row(admin, code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M2_003()
    {
        CaseId = "ST_M2_003";
        var (admin, acc) = await NewOfficeAsync();
        var form = await OpenCreateEmployeeAsync(admin);
        var name = PersonName();
        await FillEmployeeAsync(admin, form, acc.Code, name);
        var resp = await SaveAsync(admin, form, "/admin/employees");
        resp.Status.Should().BeInRange(400, 499);
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await Expect(form.GetByLabel("Họ tên")).ToHaveValueAsync(name);
        await Expect(form.Locator(".ant-btn-loading")).ToHaveCountAsync(0);
        await SearchEmployeeAsync(admin, acc.Code);
        await Expect(Row(admin, acc.Code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M2_004()
    {
        CaseId = "ST_M2_004";
        var admin = await AsAsync("Admin");
        var form = await OpenCreateEmployeeAsync(admin);
        var input = form.GetByLabel("Mã nhân viên");
        const string invalid = "Mã chỉ gồm chữ, số và . _ -";
        var twenty = ("ST_AUTO_" + Next() + "ABCDEFGHIJ")[..20];
        await input.FillAsync(twenty);
        await input.BlurAsync();
        (await input.InputValueAsync()).Length.Should().Be(20);
        await Expect(form.GetByText(invalid)).ToHaveCountAsync(0);
        await input.FillAsync("");
        await input.PressSequentiallyAsync(twenty + "Z");
        (await input.InputValueAsync()).Length.Should().BeLessOrEqualTo(20);
        await input.FillAsync("NV@01");
        await input.BlurAsync();
        await Expect(form.GetByText(invalid)).ToBeVisibleAsync();
        await input.FillAsync("   ");
        await input.BlurAsync();
        await Expect(form.GetByText(Required).First).ToBeVisibleAsync();
        await FillEmployeeAsync(admin, form, $"ST_AUTO_{Next()}.a_b-c", PersonName());
        await Expect(form.GetByText(invalid)).ToHaveCountAsync(0);
        var shown = await input.InputValueAsync();
        AccountKit.Created.Add(shown);
        var resp = await SaveAsync(admin, form, "/admin/employees");
        resp.Status.Should().BeInRange(200, 299);
        await SearchEmployeeAsync(admin, shown);
        await Expect(admin.GetByRole(AriaRole.Cell, new() { Name = shown, Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M2_005()
    {
        CaseId = "ST_M2_005";
        var admin = await AsAsync("Admin");
        var form = await OpenCreateEmployeeAsync(admin);
        var name = form.GetByLabel("Họ tên");
        var name100 = "Nguyen " + new string('a', 93);
        await name.FillAsync(name100);
        await name.BlurAsync();
        (await name.InputValueAsync()).Length.Should().Be(100);
        await Expect(form.Locator(".ant-form-item-explain-error")).ToHaveCountAsync(0);
        await name.FillAsync(name100 + "a");
        await name.BlurAsync();
        if ((await name.InputValueAsync()).Length > 100)
            await Expect(form.Locator(".ant-form-item-explain-error").First).ToBeVisibleAsync();
        await name.FillAsync("Nguyễn Văn 2");
        await name.BlurAsync();
        await Expect(form.GetByText("Họ tên chỉ gồm chữ cái", new() { Exact = false })).ToBeVisibleAsync();
        await name.FillAsync(name100);
        var att = form.GetByLabel("Mã chấm công");
        foreach (var bad in new[] { "0", "100000000" })
        {
            await att.FillAsync(bad);
            await att.BlurAsync();
            await Expect(form.Locator(".ant-form-item-explain-error").First).ToBeVisibleAsync();
        }
        var code = Code("B");
        await FillEmployeeAsync(admin, form, code, name100, attendance: "99999999");
        AccountKit.Created.Add(code);
        var resp = await SaveAsync(admin, form, "/admin/employees");
        resp.Status.Should().BeInRange(200, 299);
        await SearchEmployeeAsync(admin, code);
        await Expect(Row(admin, code)).ToContainTextAsync("99999999");
        await Expect(Row(admin, code)).ToContainTextAsync(name100);
    }

    [Fact]
    public async Task ST_M2_006()
    {
        CaseId = "ST_M2_006";
        var admin = await AsAsync("Admin");
        var name = PersonName();
        var users = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var code = Code("D");
            var form = await OpenCreateEmployeeAsync(admin);
            await FillEmployeeAsync(admin, form, code, name);
            var resp = await SaveAsync(admin, form, "/admin/employees");
            if (resp.Status >= 400)
            {
                i.Should().Be(1);
                resp.Status.Should().BeLessThan(500);
                await Expect(Errors(admin).First).ToBeVisibleAsync();
                return;
            }
            AccountKit.Created.Add(code);
            users.Add((await Dialog(admin, "Mật khẩu tạm").Locator("strong").First.InnerTextAsync()).Trim());
        }
        users.Select(u => u.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ST_M2_007()
    {
        CaseId = "ST_M2_007";
        var admin = await AsAsync("Admin");
        var code = Code("A");
        var form = await OpenCreateEmployeeAsync(admin);
        await FillEmployeeAsync(admin, form, code, PersonName(), attendance: "23");
        var resp = await SaveAsync(admin, form, "/admin/employees");
        if (resp.Status < 300) AccountKit.Created.Add(code);
        resp.Status.Should().BeInRange(400, 499);
        await Expect(Errors(admin).First).ToContainTextAsync(new Regex("chấm công", RegexOptions.IgnoreCase));
        await SearchEmployeeAsync(admin, code);
        await Expect(admin.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ST_M2_008()
    {
        CaseId = "ST_M2_008";
        var (admin, acc) = await NewOfficeAsync();
        var dlg = await OpenEmployeeAsync(admin, acc.Code);
        await Button(dlg, "Sửa").ClickAsync();
        var edit = Dialog(admin, "Sửa nhân viên");
        await Expect(edit.GetByLabel("Mã nhân viên")).ToBeDisabledAsync();
        await edit.GetByLabel("Số điện thoại").FillAsync("0987000111");
        await SelectAsync(admin, edit.GetByRole(AriaRole.Combobox, new() { Name = "Tổ" }), "VP");
        var resp = await SaveAsync(admin, edit, "/admin/employees/");
        resp.Request.Method.Should().Be("PUT");
        resp.Request.PostData.Should().Contain("\"rowVer\"").And.NotContain("\"employeeCode\"");
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        for (var i = 0; i < 2; i++)
        {
            var viewer = i == 0 ? admin : await LoggedInAsync(GetAccount("Admin").Username, GetAccount("Admin").Password);
            var again = await OpenEmployeeAsync(viewer, acc.Code);
            await Expect(again).ToContainTextAsync("0987000111");
            await Expect(again).ToContainTextAsync("VP");
            await Expect(again.GetByRole(AriaRole.Tabpanel)).ToContainTextAsync("0987000111");
        }
    }

    [Fact]
    public async Task ST_M2_009()
    {
        CaseId = "ST_M2_009";
        var (tab1, acc) = await NewOfficeAsync();
        var tab2 = await AsAsync("Admin");
        var d1 = await OpenEmployeeAsync(tab1, acc.Code);
        var d2 = await OpenEmployeeAsync(tab2, acc.Code);
        await Button(d1, "Sửa").ClickAsync();
        await Button(d2, "Sửa").ClickAsync();
        var e1 = Dialog(tab1, "Sửa nhân viên");
        var e2 = Dialog(tab2, "Sửa nhân viên");
        await e1.GetByLabel("Số điện thoại").FillAsync("0911222333");
        (await SaveAsync(tab1, e1, "/admin/employees/")).Status.Should().BeInRange(200, 299);
        await e2.GetByLabel("Họ tên").FillAsync("Ten Bi Xung Dot");
        var second = await SaveAsync(tab2, e2, "/admin/employees/");
        second.Status.Should().BeInRange(400, 499);
        await Expect(Errors(tab2).First).ToBeVisibleAsync();
        var check = await OpenEmployeeAsync(tab1, acc.Code);
        await Expect(check).ToContainTextAsync("0911222333");
        await Expect(check).Not.ToContainTextAsync("Ten Bi Xung Dot");
    }

    [Fact]
    public async Task ST_M2_010()
    {
        CaseId = "ST_M2_010";
        var admin = await AsAsync("Admin");
        var form = await OpenCreateEmployeeAsync(admin);
        await form.GetByRole(AriaRole.Combobox, new() { Name = "Vai trò" }).ClickAsync();
        await Expect(Options(admin).First).ToBeVisibleAsync();
        await Expect(Options(admin).Filter(new() { HasText = "Công nhân" })).ToHaveCountAsync(1);
        await Options(admin).Filter(new() { HasText = "Công nhân" }).ClickAsync();
        await Expect(form).ToContainTextAsync("Khoán sản phẩm");
    }

    private static async Task<ILocator> OpenResignAsync(IPage page, ILocator dlg)
    {
        await Button(dlg, "Cho nghỉ việc").ClickAsync();
        var modal = page.GetByRole(AriaRole.Dialog).Filter(new() { Has = page.GetByLabel("Lý do nghỉ việc") }).Last;
        await modal.WaitForAsync();
        return modal;
    }

    [Fact]
    public async Task ST_M2_011()
    {
        CaseId = "ST_M2_011";
        var (admin, acc) = await NewOfficeAsync();
        var active = await ActivateAsync(acc);
        var dlg = await OpenEmployeeAsync(admin, acc.Code);
        var modal = await OpenResignAsync(admin, dlg);
        await modal.GetByLabel("Lý do nghỉ việc").FillAsync("ST_AUTO_ nghi viec");
        var resp = await SaveAsync(admin, modal, "/resign", "Xác nhận");
        resp.Status.Should().BeInRange(200, 299);
        resp.Request.PostData.Should().Contain("resignDate").And.Contain("reason").And.Contain("rowVer");
        await Expect(admin.GetByLabel("Lý do nghỉ việc")).ToHaveCountAsync(0);
        await Expect(dlg).ToContainTextAsync("Đã nghỉ");
        await Expect(Button(dlg, "Cho nghỉ việc")).ToHaveCountAsync(0);
        var login = await AnonAsync();
        (await LoginStatusAsync(login, active.Username, active.TempPassword)).Should().BeInRange(400, 499);
        await Expect(login.Locator(".ant-alert-error")).ToBeVisibleAsync();
        await admin.GotoAsync("/admin/employees");
        await admin.Locator(".ant-select").Filter(new() { HasText = "Trạng thái:" }).ClickAsync();
        await Options(admin).Filter(new() { HasText = "Đã nghỉ" }).ClickAsync();
        await admin.GetByPlaceholder("Tìm theo mã, tên...").FillAsync(acc.Code);
        await Expect(Row(admin, acc.Code)).ToContainTextAsync("Đã nghỉ");
    }

    [Fact]
    public async Task ST_M2_012()
    {
        CaseId = "ST_M2_012";
        var admin = await AsAsync("Admin");
        var code = Code("R");
        var form = await OpenCreateEmployeeAsync(admin);
        await FillEmployeeAsync(admin, form, code, PersonName(), start: "01/06/2026");
        AccountKit.Created.Add(code);
        (await SaveAsync(admin, form, "/admin/employees")).Status.Should().BeInRange(200, 299);
        foreach (var date in new[] { "01/01/2026", DateTime.Now.AddDays(1).ToString("dd/MM/yyyy") })
        {
            var dlg = await OpenEmployeeAsync(admin, code);
            var modal = await OpenResignAsync(admin, dlg);
            await DateAsync(modal.GetByLabel("Ngày nghỉ"), date);
            await modal.GetByLabel("Lý do nghỉ việc").FillAsync("ST_AUTO_ ngay khong hop le");
            var resp = await SaveAsync(admin, modal, "/resign", "Xác nhận");
            resp.Status.Should().BeInRange(400, 499, $"ngày nghỉ {date} không hợp lệ, payload {resp.Request.PostData}");
            await Expect(Errors(admin).First).ToBeVisibleAsync();
            await Expect(modal.GetByLabel("Lý do nghỉ việc")).ToHaveValueAsync("ST_AUTO_ ngay khong hop le");
        }
        await SearchEmployeeAsync(admin, code);
        await Expect(Row(admin, code)).ToContainTextAsync("Đang làm");
    }

    [Fact]
    public async Task ST_M2_013()
    {
        CaseId = "ST_M2_013";
        var (admin, acc) = await NewOfficeAsync();
        var dlg = await OpenEmployeeAsync(admin, acc.Code);
        var modal = await OpenResignAsync(admin, dlg);
        var reason = modal.GetByLabel("Lý do nghỉ việc");
        await reason.FillAsync("    ");
        await Button(modal, "Xác nhận").ClickAsync();
        await Expect(modal.GetByText("Vui lòng nhập lý do.")).ToBeVisibleAsync();
        var max = "ST_AUTO_" + new string('x', 492);
        await reason.FillAsync("");
        await reason.PressSequentiallyAsync(max + "Y", new() { Timeout = 60000 });
        (await reason.InputValueAsync()).Length.Should().Be(500);
        await reason.FillAsync(max);
        (await SaveAsync(admin, modal, "/resign", "Xác nhận")).Status.Should().BeInRange(200, 299);
        var again = await OpenEmployeeAsync(admin, acc.Code);
        await Expect(again.GetByRole(AriaRole.Tabpanel)).ToContainTextAsync(max);
    }

    [Fact]
    public async Task ST_M2_014()
    {
        CaseId = "ST_M2_014";
        var admin = await AsAsync("Admin");
        var dlg = await OpenEmployeeAsync(admin, GetAccount("Admin").Username);
        await Expect(Button(dlg, "Sửa")).ToBeVisibleAsync();
        await Expect(Button(dlg, "Cho nghỉ việc")).ToHaveCountAsync(0);
        var id = Regex.Match(admin.Url, @"id=(\d+)").Groups[1].Value;
        id.Should().NotBeEmpty();
        var token = await TokenAsync(admin);
        var detail = await admin.Context.APIRequest.GetAsync($"{BaseUrl}/api/v1/admin/employees/{id}",
            new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } });
        var rowVer = Regex.Match(await detail.TextAsync(), "\"rowVer\":\"([^\"]+)\"").Groups[1].Value;
        var resp = await ApiPostAsync(admin, $"/api/v1/admin/employees/{id}/resign",
            new { resignDate = DateTime.Now.ToString("yyyy-MM-dd"), reason = "ST_AUTO_ tu cho nghi", rowVer });
        resp.Status.Should().BeInRange(400, 499);
    }

    [Fact]
    public async Task ST_M2_015()
    {
        CaseId = "ST_M2_015";
        var (admin, acc) = await NewOfficeAsync();
        var active = await ActivateAsync(acc);
        var session = await LoggedInAsync(active.Username, active.TempPassword);
        var dlg = await OpenEmployeeAsync(admin, acc.Code);
        await Button(dlg, "Khóa tài khoản").ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Filter(new() { Has = admin.GetByLabel("Lý do khóa") }).Last;
        var locks = 0;
        admin.Request += (_, r) => { if (r.Url.EndsWith("/account/lock")) locks++; };
        await Button(modal, "Xác nhận").ClickAsync();
        await Expect(modal.Locator(".ant-form-item-explain-error, .ant-form-item-has-error").First).ToBeVisibleAsync();
        locks.Should().Be(0);
        await modal.GetByLabel("Lý do khóa").FillAsync("ST_AUTO_ khoa thu");
        (await SaveAsync(admin, modal, "/account/lock", "Xác nhận")).Status.Should().BeInRange(200, 299);
        await Expect(Button(dlg, "Mở khóa")).ToBeVisibleAsync();
        await session.GotoAsync("/profile");
        await session.WaitForURLAsync(u => u.Contains("/login"));
        var blocked = await AnonAsync();
        (await LoginStatusAsync(blocked, active.Username, active.TempPassword)).Should().BeInRange(400, 499);
        await Expect(blocked.Locator(".ant-alert-error")).ToBeVisibleAsync();
        await SearchEmployeeAsync(admin, acc.Code);
        await Expect(Row(admin, acc.Code)).ToContainTextAsync("Đã khóa");
        var d2 = await OpenEmployeeAsync(admin, acc.Code);
        await admin.RunAndWaitForResponseAsync(() => Button(d2, "Mở khóa").ClickAsync(), r => r.Url.Contains("/account/unlock"));
        await Expect(Button(d2, "Khóa tài khoản")).ToBeVisibleAsync();
        await LoggedInAsync(active.Username, active.TempPassword);
        await SearchEmployeeAsync(admin, acc.Code);
        await Expect(Row(admin, acc.Code)).ToContainTextAsync("Hoạt động");
    }

    [Fact]
    public async Task ST_M2_016()
    {
        CaseId = "ST_M2_016";
        var (admin, acc) = await NewOfficeAsync();
        var active = await ActivateAsync(acc);
        var session = await LoggedInAsync(active.Username, active.TempPassword);
        var dlg = await OpenEmployeeAsync(admin, acc.Code);
        await Button(dlg, "Đặt lại mật khẩu").ClickAsync();
        await ConfirmAsync(admin);
        var temp = Dialog(admin, "Mật khẩu tạm");
        var pass = (await temp.GetByRole(AriaRole.Heading).Last.InnerTextAsync()).Trim();
        await temp.GetByRole(AriaRole.Button).Last.ClickAsync();
        var again = await OpenEmployeeAsync(admin, acc.Code);
        await Expect(again).Not.ToContainTextAsync(pass);
        await session.GotoAsync("/profile");
        await session.WaitForURLAsync(u => u.Contains("/login"));
        var old = await AnonAsync();
        (await LoginStatusAsync(old, active.Username, active.TempPassword)).Should().BeInRange(400, 499);
        var fresh = await AnonAsync();
        (await LoginStatusAsync(fresh, active.Username, pass)).Should().Be(200);
        await Expect(fresh.GetByLabel("Nhập lại mật khẩu mới", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M2_017()
    {
        CaseId = "ST_M2_017";
        var admin = await AsAsync("Admin");
        var prefix = "ST_AUTO_I" + Next();
        var rows = Enumerable.Range(1, 8).Select(i => WorkerRow($"{prefix}{i}", PersonName(), null)).ToList();
        rows.Add(WorkerRow($"{prefix}9", PersonName(), null, "VaiTroSai"));
        rows.Add(WorkerRow($"{prefix}X", "", null));
        var dlg = await UploadImportAsync(admin, ImportWorkbook(rows));
        var run = Button(dlg, "Nhập 8 dòng hợp lệ");
        await Expect(run).ToBeEnabledAsync();
        await Expect(dlg.Locator(".ant-alert-warning")).ToBeVisibleAsync();
        await run.ClickAsync();
        for (var i = 1; i <= 8; i++) AccountKit.Created.Add($"{prefix}{i}");
        var resp = await admin.RunAndWaitForResponseAsync(() => ConfirmAsync(admin),
            r => r.Url.Contains("/employee-import") && r.Request.Method == "POST");
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Button(dlg, "Sao chép tất cả")).ToBeVisibleAsync();
        await Expect(dlg).ToContainTextAsync("Đã nhập 8 nhân viên");
        await Expect(dlg).ToContainTextAsync("Dòng không nhập");
        await SearchEmployeeAsync(admin, prefix);
        await Expect(Row(admin, prefix)).ToHaveCountAsync(8);
    }

    [Fact]
    public async Task ST_M2_018()
    {
        CaseId = "ST_M2_018";
        var admin = await AsAsync("Admin");
        var prefix = "ST_AUTO_E" + Next();
        var allBad = ImportWorkbook(new[] { WorkerRow($"{prefix}1", PersonName(), null, "VaiTroSai"), WorkerRow($"{prefix}2", "", null) });
        var dlg = await UploadImportAsync(admin, allBad);
        await Expect(Button(dlg, "Nhập 0 dòng hợp lệ")).ToBeDisabledAsync();
        var fake = await UploadImportAsync(admin, System.Text.Encoding.UTF8.GetBytes("Ma NV,Ho ten\nA,B\n"));
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await Expect(fake.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("dòng hợp lệ") })).ToBeDisabledAsync();
        await admin.RouteAsync(new Regex("/employee-import"), async route =>
        {
            if (route.Request.Method == "POST" && !route.Request.Url.Contains("preview"))
                await route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"success\":false,\"message\":\"Loi gia lap\"}" });
            else await route.ContinueAsync();
        });
        var good = await UploadImportAsync(admin, ImportWorkbook(new[] { WorkerRow($"{prefix}3", PersonName(), null) }));
        await Button(good, "Nhập 1 dòng hợp lệ").ClickAsync();
        await ConfirmAsync(admin);
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await Expect(good.Locator(".ant-btn-loading")).ToHaveCountAsync(0);
        await admin.UnrouteAllAsync();
        await SearchEmployeeAsync(admin, prefix);
        await Expect(admin.GetByText("Không có dữ liệu phù hợp.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M2_019()
    {
        CaseId = "ST_M2_019";
        var admin = await AsAsync("Admin");
        var code = Code("T");
        await admin.GotoAsync("/teams");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm tổ" }).ClickAsync();
        var dlg = Dialog(admin, "Thêm tổ");
        await dlg.GetByLabel("Mã tổ").FillAsync(code);
        await SelectAsync(admin, dlg.GetByRole(AriaRole.Combobox, new() { Name = "Loại tổ" }), "Tổ may");
        AccountKit.Teams.Add(code);
        var resp = await SaveAsync(admin, dlg, "/teams");
        resp.Status.Should().BeInRange(200, 299);
        resp.Request.PostData.Should().Contain("teamCode").And.Contain("teamType");
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        var row = await TeamRowAsync(admin, code);
        await Expect(row.GetByRole(AriaRole.Cell, new() { Name = "0", Exact = true })).ToBeVisibleAsync();
        var form = await OpenCreateEmployeeAsync(admin);
        var team = form.GetByRole(AriaRole.Combobox, new() { Name = "Tổ" });
        await team.ClickAsync();
        await team.FillAsync(code);
        await Expect(Options(admin).Filter(new() { HasText = code })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M2_020()
    {
        CaseId = "ST_M2_020";
        var admin = await AsAsync("Admin");
        var existing = await CreateTeamAsync(admin);
        await admin.GotoAsync("/teams");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm tổ" }).ClickAsync();
        var dlg = Dialog(admin, "Thêm tổ");
        var input = dlg.GetByLabel("Mã tổ");
        await input.FillAsync(existing);
        await SelectAsync(admin, dlg.GetByRole(AriaRole.Combobox, new() { Name = "Loại tổ" }), "Tổ may");
        var dup = await SaveAsync(admin, dlg, "/teams");
        dup.Status.Should().BeInRange(400, 499);
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await input.FillAsync("");
        await input.PressSequentiallyAsync(("ST_AUTO_" + Next() + "ABCDEFGHIJ")[..20] + "Z");
        (await input.InputValueAsync()).Length.Should().Be(20);
        await input.FillAsync("MAY 01");
        await input.BlurAsync();
        await Expect(dlg.GetByText("Mã chỉ gồm chữ, số và . _ -")).ToBeVisibleAsync();
        var twenty = ("ST_AUTO_" + Next() + "ABCDEFGHIJ")[..20];
        await input.FillAsync(twenty);
        AccountKit.Teams.Add(twenty);
        (await SaveAsync(admin, dlg, "/teams")).Status.Should().BeInRange(200, 299);
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm tổ" }).ClickAsync();
        await Dialog(admin, "Thêm tổ").GetByLabel("Mã tổ").FillAsync(Code("T"));
        await Button(Dialog(admin, "Thêm tổ"), "Lưu").ClickAsync();
        await Expect(Dialog(admin, "Thêm tổ").GetByText(Required)).ToBeVisibleAsync();
        var row = await TeamRowAsync(admin, existing);
        await Expect(row).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M2_021()
    {
        CaseId = "ST_M2_021";
        var admin = await AsAsync("Admin");
        var code = await CreateTeamAsync(admin);
        var row = await TeamRowAsync(admin, code);
        await Button(row, "Ngừng").ClickAsync();
        await ConfirmAsync(admin);
        await Expect(Toast(admin, "Đã lưu.").First).ToBeVisibleAsync();
        row = await TeamRowAsync(admin, code);
        await Expect(row).ToContainTextAsync("Ngừng");
        await Expect(Button(row, "Dùng lại")).ToBeVisibleAsync();
        var form = await OpenCreateEmployeeAsync(admin);
        var team = form.GetByRole(AriaRole.Combobox, new() { Name = "Tổ" });
        await team.ClickAsync();
        await team.FillAsync(code);
        await Expect(Options(admin).Filter(new() { HasText = code })).ToHaveCountAsync(0);
        await admin.GotoAsync("/order-line-teams");
        await Expect(admin.GetByText(code)).ToHaveCountAsync(0);
        row = await TeamRowAsync(admin, code);
        await Button(row, "Dùng lại").ClickAsync();
        if (await Button(admin, "Xác nhận").CountAsync() > 0) await ConfirmAsync(admin);
        await Expect(Toast(admin, "Đã lưu.").First).ToBeVisibleAsync();
        row = await TeamRowAsync(admin, code);
        await Expect(row).ToContainTextAsync("Hoạt động");
    }

    [Fact]
    public async Task ST_M2_022()
    {
        CaseId = "ST_M2_022";
        var admin = await AsAsync("Admin");
        var team = await CreateTeamAsync(admin);
        var workers = new[] { await CreateMemberAsync(admin, null), await CreateMemberAsync(admin, null) };
        var dlg = await OpenTeamAsync(admin, team);
        await Button(dlg, "Thêm nhân viên").ClickAsync();
        var add = Dialog(admin, "Thêm nhân viên");
        var pick = add.GetByRole(AriaRole.Combobox, new() { Name = "Nhân viên chưa thuộc tổ nào" });
        foreach (var w in workers)
        {
            await pick.ClickAsync();
            await pick.FillAsync(w.Code);
            await Options(admin).Filter(new() { HasText = w.Code }).ClickAsync();
        }
        await admin.Keyboard.PressAsync("Escape");
        var resp = await SaveAsync(admin, add, "/add-members", "Xác nhận");
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        var again = await OpenTeamAsync(admin, team);
        await Expect(again.GetByRole(AriaRole.Tab, new() { Name = "Số người (2)" })).ToBeVisibleAsync();
        await SearchEmployeeAsync(admin, workers[0].Code);
        await Expect(Row(admin, workers[0].Code)).ToContainTextAsync(team);
        var active = await ActivateAsync(workers[0]);
        var me = await LoggedInAsync(active.Username, active.TempPassword);
        await me.GotoAsync("/profile");
        await Expect(me.GetByRole(AriaRole.Main)).ToContainTextAsync(team);
    }

    private static async Task<ILocator> OpenTransferAsync(IPage page, string team, string code)
    {
        var dlg = await OpenTeamAsync(page, team);
        await Row(dlg, code).GetByRole(AriaRole.Checkbox).CheckAsync();
        await Button(dlg, "Chuyển sang tổ khác").ClickAsync();
        var modal = Dialog(page, "Chuyển sang tổ khác");
        await modal.WaitForAsync();
        return modal;
    }

    [Fact]
    public async Task ST_M2_023()
    {
        CaseId = "ST_M2_023";
        var admin = await AsAsync("Admin");
        var from = await CreateTeamAsync(admin);
        var to = await CreateTeamAsync(admin);
        var w = await CreateMemberAsync(admin, from);
        var modal = await OpenTransferAsync(admin, from, w.Code);
        await SelectAsync(admin, modal.GetByRole(AriaRole.Combobox, new() { Name = "Tổ đích" }), to);
        var date = modal.GetByLabel("Ngày hiệu lực");
        await date.ClickAsync();
        var tomorrow = DateTime.Now.AddDays(1);
        await Expect(admin.Locator($".ant-picker-dropdown:visible td.ant-picker-cell-in-view.ant-picker-cell-disabled")
            .Filter(new() { HasTextRegex = new Regex($"^{tomorrow.Day}$") })).ToHaveCountAsync(1);
        await DateAsync(date, "01/01/2026");
        var early = await SaveAsync(admin, modal, "/transfer-in", "Xác nhận");
        early.Status.Should().BeInRange(400, 499, "payload gửi " + early.Request.PostData);
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await DateAsync(date, Today);
        var ok = await SaveAsync(admin, modal, "/transfer-in", "Xác nhận");
        ok.Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        await SearchEmployeeAsync(admin, w.Code);
        await Expect(Row(admin, w.Code)).ToContainTextAsync(to);
        var detail = await OpenEmployeeAsync(admin, w.Code);
        await Expect(detail.GetByRole(AriaRole.Tabpanel)).ToContainTextAsync(to);
    }

    [Fact]
    public async Task ST_M2_024()
    {
        CaseId = "ST_M2_024";
        var admin = await AsAsync("Admin");
        var dlg = await OpenTeamAsync(admin, "MAY1");
        var leader = Row(dlg, "TTMAY");
        await Expect(leader.GetByRole(AriaRole.Checkbox)).ToBeDisabledAsync();
        await Expect(dlg.GetByText("cần thôi giữ chức trước khi chuyển hoặc loại khỏi tổ")).ToBeVisibleAsync();
        await Expect(Button(dlg, "Chuyển sang tổ khác")).ToBeDisabledAsync();
        var row = await TeamRowAsync(admin, "MAY1");
        await Expect(row).ToContainTextAsync("Nguyễn Thị Hồng");
    }

    private static async Task AppointAsync(IPage page, ILocator dlg, string code, string button)
    {
        await Assertions.Expect(Row(dlg, code)).ToBeVisibleAsync();
        var task = page.WaitForResponseAsync(r => r.Url.Contains("/appoint"));
        await Button(Row(dlg, code), button).ClickAsync();
        // Có người đang giữ chức thì hiện Popconfirm, chưa có thì gọi API ngay
        var confirm = Button(page, "Xác nhận").Last;
        try
        {
            await confirm.WaitForAsync(new() { Timeout = 4000 });
            await confirm.ClickAsync();
        }
        catch (TimeoutException) { }
        (await task).Status.Should().BeInRange(200, 299);
    }

    private const string Mtd = "MAY1", NewLeader = "CNMAY1", OldLeader = "TTMAY";

    private async Task RestoreLeaderAsync(IPage admin)
    {
        var dlg = await OpenTeamAsync(admin, Mtd);
        await Expect(Row(dlg, OldLeader)).ToBeVisibleAsync();
        if (await Row(dlg, OldLeader).GetByRole(AriaRole.Cell, new() { Name = "Tổ trưởng", Exact = true }).CountAsync() == 0)
            await AppointAsync(admin, dlg, OldLeader, "Bổ nhiệm tổ trưởng");
        InvalidateRole("Worker");
    }

    [Fact]
    public async Task ST_M2_025()
    {
        CaseId = "ST_M2_025";
        var admin = await AsAsync("Admin");
        try
        {
            var dlg = await OpenTeamAsync(admin, Mtd);
            await AppointAsync(admin, dlg, NewLeader, "Bổ nhiệm tổ trưởng");
            await Expect(Toast(admin, "Đã lưu.").First).ToBeVisibleAsync();
            dlg = await OpenTeamAsync(admin, Mtd);
            await Expect(Row(dlg, NewLeader).GetByRole(AriaRole.Cell, new() { Name = "Tổ trưởng", Exact = true })).ToBeVisibleAsync();
            await Expect(Row(dlg, OldLeader).GetByRole(AriaRole.Cell, new() { Name = "Công nhân", Exact = true })).ToBeVisibleAsync();
            var (u, p) = GetAccount("Worker");
            var me = await LoggedInAsync(u, p);
            foreach (var path in new[] { "/team/meals", "/hr/grades" })
            {
                await GotoAndWaitAsync(me, path);
                PathOf(me).Should().Be(path);
            }
        }
        finally { await RestoreLeaderAsync(admin); }
    }

    [Fact]
    public async Task ST_M2_026()
    {
        CaseId = "ST_M2_026";
        var admin = await AsAsync("Admin");
        try
        {
            var dlg = await OpenTeamAsync(admin, Mtd);
            await AppointAsync(admin, dlg, NewLeader, "Bổ nhiệm tổ trưởng");
            var (u, p) = GetAccount("Worker");
            var me = await LoggedInAsync(u, p);
            await GotoAndWaitAsync(me, "/team/meals");
            PathOf(me).Should().Be("/team/meals");
            dlg = await OpenTeamAsync(admin, Mtd);
            await Button(Row(dlg, NewLeader), "Thôi giữ chức").ClickAsync();
            var dismiss = await admin.RunAndWaitForResponseAsync(() => ConfirmAsync(admin), r => r.Url.Contains("/dismiss"));
            dismiss.Status.Should().BeInRange(200, 299);
            await me.ReloadAsync();
            await me.GotoAsync("/team/meals");
            await Expect(me.GetByLabel("Tìm chức năng").Or(me.GetByLabel("Tên đăng nhập"))).ToBeVisibleAsync();
            if (PathOf(me) == "/login")
            {
                await FillLoginAsync(me, u, p);
                await me.WaitForURLAsync(url => !url.Contains("/login"));
                await me.GotoAsync("/team/meals");
            }
            await me.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/");
            var token = await TokenAsync(me);
            var api = await me.Context.APIRequest.GetAsync($"{BaseUrl}/api/v1/team-leader/meals/suggestion?date={DateTime.Now:yyyy-MM-dd}",
                new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } });
            api.Status.Should().Be(403);
        }
        finally { await RestoreLeaderAsync(admin); }
    }

    private static string GradeCodeText() => "T" + Next()[1..];

    private static async Task<ILocator> OpenAddGradeAsync(IPage page)
    {
        await page.GotoAsync("/hr/grades");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Bậc & mức tiền" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm bậc" }).ClickAsync();
        return Dialog(page, "Thêm bậc");
    }

    private static ILocator GradeRow(IPage page, string code) =>
        page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true }) });

    private static async Task DeleteGradeAsync(IPage page, string code)
    {
        var token = await TokenAsync(page);
        await page.Context.APIRequest.DeleteAsync($"{BaseUrl}/api/v1/admin/grade-codes/{code}",
            new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } });
    }

    [Fact]
    public async Task ST_M2_027()
    {
        CaseId = "ST_M2_027";
        var admin = await AsAsync("Admin");
        var dlg = await OpenAddGradeAsync(admin);
        var code = dlg.GetByLabel("Mã bậc");
        await code.FillAsync("");
        await code.PressSequentiallyAsync("ABCDEF");
        (await code.InputValueAsync()).Should().HaveLength(5);
        await code.FillAsync("b-1");
        await code.BlurAsync();
        await Expect(dlg.GetByText("Chỉ gồm chữ và số, tối đa 5 ký tự.")).ToBeVisibleAsync();
        var five = "T" + Next()[1..];
        var lower = "z" + Next()[1..];
        foreach (var value in new[] { five, lower })
        {
            if (await dlg.CountAsync() == 0) dlg = await OpenAddGradeAsync(admin);
            await dlg.GetByLabel("Mã bậc").FillAsync(value);
            await dlg.GetByLabel("Tiền bậc / tháng").FillAsync("100000");
            var resp = await SaveAsync(admin, dlg, "/grade-codes");
            resp.Status.Should().BeInRange(200, 299);
            await Expect(Toast(admin, "Đã lưu bậc thợ.").First).ToBeVisibleAsync();
        }
        var token = await TokenAsync(admin);
        var options = await admin.Context.APIRequest.GetAsync($"{BaseUrl}/api/v1/common/grade-code-options",
            new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } });
        var json = (await options.TextAsync()).ToUpperInvariant();
        json.Should().Contain($"\"{five.ToUpperInvariant()}\"").And.Contain($"\"{lower.ToUpperInvariant()}\"");
        await DeleteGradeAsync(admin, five);
        await DeleteGradeAsync(admin, lower.ToUpperInvariant());
    }

    [Fact]
    public async Task ST_M2_028()
    {
        CaseId = "ST_M2_028";
        var admin = await AsAsync("Admin");
        var value = GradeCodeText();
        var dlg = await OpenAddGradeAsync(admin);
        await dlg.GetByLabel("Mã bậc").FillAsync(value);
        await dlg.GetByLabel("Tiền bậc / tháng").FillAsync("100000");
        (await SaveAsync(admin, dlg, "/grade-codes")).Status.Should().BeInRange(200, 299);
        var tomorrow = DateTime.Now.AddDays(1);

        await Button(GradeRow(admin, value), "Đổi tiền").ClickAsync();
        var change = admin.GetByRole(AriaRole.Dialog).Last;
        await change.GetByRole(AriaRole.Spinbutton).FillAsync("200000");
        var date = change.GetByLabel("Hiệu lực từ");
        var before = await date.InputValueAsync();
        await DateAsync(date, "01/01/2020");
        await change.Locator(".ant-modal-title").ClickAsync();
        (await date.InputValueAsync()).Should().NotBe("01/01/2020", "ngày trước earliest phải bị chặn");
        await DateAsync(date, tomorrow.ToString("dd/MM/yyyy"));
        (await SaveAsync(admin, change, "/grade-levels")).Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã lưu mức tiền bậc.")).ToBeVisibleAsync();

        await admin.GotoAsync("/hr/grades");
        await admin.GetByRole(AriaRole.Tab, new() { Name = "Bậc & mức tiền" }).ClickAsync();
        await Button(GradeRow(admin, value), "Xem").ClickAsync();
        var history = admin.GetByRole(AriaRole.Dialog).Last;
        await history.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sửa") }).First.ClickAsync();
        var editor = Dialog(admin, "Sửa mức tiền bậc");
        var editDate = editor.GetByLabel("Hiệu lực từ");
        await DateAsync(editDate, Today);
        await editor.Locator(".ant-modal-title").ClickAsync();
        (await editDate.InputValueAsync()).Should().NotBe(Today, "Sửa mức tiền không cho chọn hôm nay");
        await DateAsync(editDate, tomorrow.ToString("dd/MM/yyyy"));
        (await SaveAsync(admin, editor, "/grade-levels")).Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã lưu mức tiền bậc.").First).ToBeVisibleAsync();
    }

    private const string GradeWorker = "CNMAY1 - Nguyễn Thị Lan";

    private async Task<(IPage Leader, string Reason)> ProposeAsync(string worker, string grade, string role = "TeamDeputy")
    {
        var leader = await AsAsync(role);
        await leader.GotoAsync("/hr/grades");
        await leader.GetByRole(AriaRole.Button, new() { Name = "Đề xuất bậc" }).ClickAsync();
        var dlg = Dialog(leader, "Đề xuất bậc");
        var reason = "ST_AUTO_ de xuat " + Next();
        await SelectAsync(leader, dlg.GetByLabel("Công nhân"), worker);
        await dlg.GetByRole(AriaRole.Combobox, new() { Name = "Bậc đề xuất" }).ClickAsync();
        await Options(leader).Filter(new() { HasTextRegex = new Regex($"^{grade}\\b") }).First.ClickAsync();
        await dlg.GetByLabel("Lý do đề xuất").FillAsync(reason);
        (await SaveAsync(leader, dlg, "/grade-evaluations")).Status.Should().BeInRange(200, 299);
        await Expect(Toast(leader, "Đã gửi đề xuất bậc thợ.")).ToBeVisibleAsync();
        await leader.GotoAsync("/hr/grades");
        return (leader, reason);
    }

    [Fact]
    public async Task ST_M2_029()
    {
        CaseId = "ST_M2_029";
        // CNMAY11 (tổ MAY2 của TeamLeader): đặt lại và kích hoạt mật khẩu "1" để xem hồ sơ
        var setup = await AsAsync("Admin");
        var seed = await AccountKit.ResetPasswordAsync(setup, new TempAccount("CNMAY11", "CNMAY11", ""));
        await AccountKit.ActivateAsync(await AnonAsync(), seed, "1");
        await SearchEmployeeAsync(setup, "CNMAY11");
        var current = (await GradeRow(setup, "CNMAY11").GetByRole(AriaRole.Cell).Nth(5).InnerTextAsync()).Trim();
        var target = current == "A" ? "B" : "A";
        var (leader, reason) = await ProposeAsync("CNMAY11", target, "TeamLeader");
        var admin = await AsAsync("Admin");
        await admin.GotoAsync("/hr/grades");
        await Expect(Row(admin, reason)).ToContainTextAsync("Chờ quyết định");
        await Button(Row(admin, reason), "Áp dụng").ClickAsync();
        var apply = Dialog(admin, "Áp dụng bậc thợ");
        await DateAsync(apply.GetByLabel("Hiệu lực từ"), Today);
        (await admin.RunAndWaitForResponseAsync(() => apply.GetByRole(AriaRole.Button).Last.ClickAsync(),
            r => r.Url.Contains("/apply"))).Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã áp dụng bậc thợ.")).ToBeVisibleAsync();
        var worker = await LoggedInAsync("CNMAY11", "1");
        await worker.GotoAsync("/profile");
        await Expect(worker.Locator("th.ant-descriptions-item-label", new() { HasText = "Bậc thợ" })
            .Locator("xpath=following-sibling::td[1]")).ToHaveTextAsync(target);
        await SearchEmployeeAsync(admin, "CNMAY11");
        await Expect(GradeRow(admin, "CNMAY11").GetByRole(AriaRole.Cell).Nth(5)).ToHaveTextAsync(target);
        foreach (var page in new[] { leader, admin })
        {
            await page.GotoAsync("/hr/grades");
            await Expect(Row(page, reason)).ToContainTextAsync("Đã áp dụng");
        }
    }

    [Fact]
    public async Task ST_M2_030()
    {
        CaseId = "ST_M2_030";
        var (leader, reason) = await ProposeAsync("CNMAY3", "B");
        await leader.GetByRole(AriaRole.Button, new() { Name = "Đề xuất bậc" }).ClickAsync();
        var dlg = Dialog(leader, "Đề xuất bậc");
        var pick = dlg.GetByLabel("Công nhân");
        await pick.ClickAsync();
        if (await pick.IsEditableAsync()) await pick.FillAsync("CNMAY3");
        await Expect(Options(leader).Filter(new() { HasText = "CNMAY3" }).First).ToHaveClassAsync(new Regex("ant-select-item-option-disabled"));
        var admin = await AsAsync("Admin");
        await admin.GotoAsync("/hr/grades");
        await Button(Row(admin, reason), "Không áp dụng").ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Last;
        var note = modal.GetByRole(AriaRole.Textbox);
        var max = "ST_AUTO_" + new string('n', 492);
        await note.PressSequentiallyAsync(max + "Z", new() { Timeout = 60000 });
        (await note.InputValueAsync()).Length.Should().Be(500);
        var resp = await admin.RunAndWaitForResponseAsync(() => modal.Locator(".ant-modal-footer button").Last.ClickAsync(),
            r => r.Url.Contains("/grade-evaluations") && r.Request.Method == "POST");
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã ghi nhận không áp dụng bậc thợ.")).ToBeVisibleAsync();
        await leader.GotoAsync("/hr/grades");
        await Expect(Row(leader, reason)).ToContainTextAsync("Không áp dụng");
        await Expect(Row(leader, reason)).ToContainTextAsync(max[..100]);
        await SearchEmployeeAsync(admin, "CNMAY3");
        await Expect(GradeRow(admin, "CNMAY3").GetByRole(AriaRole.Cell).Nth(5)).ToHaveTextAsync("A");
    }

    [Fact]
    public async Task ST_M2_031()
    {
        CaseId = "ST_M2_031";
        var admin = await AsAsync("Admin");
        const string team = "MAY1", code = "CNMAY6";
        await admin.GotoAsync("/hr/grades");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Đặt bậc trực tiếp" }).ClickAsync();
        var dlg = Dialog(admin, "Đặt bậc trực tiếp");
        var worker = dlg.GetByRole(AriaRole.Combobox, new() { Name = "Công nhân" });
        await Expect(worker).ToBeDisabledAsync();
        await SelectAsync(admin, dlg.GetByRole(AriaRole.Combobox, new() { Name = "Tổ" }), team);
        await Expect(worker).ToBeEnabledAsync();
        await SelectAsync(admin, worker, code);
        await dlg.GetByRole(AriaRole.Combobox, new() { Name = "Bậc mới" }).ClickAsync();
        await Options(admin).Filter(new() { HasTextRegex = new Regex("^C\\b") }).First.ClickAsync();
        await DateAsync(dlg.GetByLabel("Hiệu lực từ"), Today);
        await dlg.GetByLabel("Lý do").FillAsync("ST_AUTO_ dat truc tiep");
        var resp = await SaveAsync(admin, dlg, "/grade-evaluations/set");
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã đặt bậc thợ.")).ToBeVisibleAsync();
        await SearchEmployeeAsync(admin, code);
        await Expect(GradeRow(admin, code).GetByRole(AriaRole.Cell).Nth(5)).ToHaveTextAsync("C");
    }

    [Fact]
    public async Task ST_M2_032()
    {
        CaseId = "ST_M2_032";
        var leader = await AsAsync("TeamLeader");
        await GotoAndWaitAsync(leader, "/hr/grades");
        await Expect(leader.GetByRole(AriaRole.Tab, new() { Name = "Đề xuất bậc" })).ToBeVisibleAsync();
        var tab = leader.GetByRole(AriaRole.Tab, new() { Name = "Bậc & mức tiền" });
        if (await tab.CountAsync() > 0)
        {
            await tab.ClickAsync();
            foreach (var name in new[] { "Thêm bậc", "Sửa", "Đổi tiền", "Xóa" })
                await Expect(leader.GetByRole(AriaRole.Button, new() { Name = name })).ToHaveCountAsync(0);
        }
        var resp = await ApiPostAsync(leader, "/api/v1/admin/grade-levels",
            new { gradeCode = "A", monthlyAmount = 1, effectiveDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd") });
        resp.Status.Should().Be(403);
    }

    [Fact]
    public async Task ST_M2_033()
    {
        CaseId = "ST_M2_033";
        var worker = await AsAsync("Worker");
        await GotoAndWaitAsync(worker, "/");
        var forbidden = 0;
        worker.Request += (_, r) => { if (r.Url.Contains("/teams") && r.Url.Contains("/api/") || r.Url.Contains("/api/v1/admin/")) forbidden++; };
        foreach (var path in new[] { "/teams", "/admin/employees" })
        {
            await worker.GotoAsync(path);
            await worker.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        }
        forbidden.Should().Be(0);
        var mgr = await AsAsync("Manager");
        await GotoAndWaitAsync(mgr, "/");
        var adminCalls = 0;
        mgr.Request += (_, r) => { if (r.Url.Contains("/api/v1/admin/")) adminCalls++; };
        await mgr.GotoAsync("/admin/employees/123");
        await mgr.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        adminCalls.Should().Be(0);
        throw new InvalidOperationException(
            "Phần Worker và Manager đạt; phần TeamLeader tổ cắt gõ /hr/grades không chạy được: thiếu tài khoản tổ trưởng tổ cắt");
    }

    [Fact]
    public async Task ST_M2_034()
    {
        CaseId = "ST_M2_034";
        var (u, p) = GetAccount("Admin");
        var admin = await LoggedInAsync(u, p);
        await OpenEmployeeAsync(admin, "CNMAY1");
        var id = Regex.Match(admin.Url, @"id=(\d+)").Groups[1].Value;
        id.Should().NotBeEmpty();
        await admin.GotoAsync($"/admin/employees/{id}");
        await admin.WaitForURLAsync(url => url.EndsWith($"/admin/employees?id={id}"));
        var dlg = Dialog(admin, "CNMAY1 ·");
        await Expect(dlg).ToBeVisibleAsync();
        await dlg.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await admin.Locator("button.app-user").ClickAsync();
        await admin.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync();
        await admin.WaitForURLAsync(url => url.Contains("/login"));
        await admin.GoBackAsync();
        await admin.WaitForURLAsync(url => url.Contains("/login"));
        await Expect(admin.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
        (await admin.EvaluateAsync<string?>("localStorage.getItem('nxmtd.accessToken')")).Should().BeNull();
        InvalidateRole("Admin");
    }

    [Fact]
    public async Task ST_M2_035()
    {
        CaseId = "ST_M2_035";
        var leader = await AsAsync("TeamLeader");
        await leader.GotoAsync("/hr/grades");
        await leader.GetByRole(AriaRole.Button, new() { Name = "AI gợi ý bậc cả tổ" }).ClickAsync();
        var dlg = Dialog(leader, "AI gợi ý bậc thợ");
        var confirm = dlg.GetByRole(AriaRole.Button, new() { Name = "Xác nhận gửi đề xuất" });
        var generate = dlg.GetByRole(AriaRole.Button, new() { Name = "Tạo gợi ý" });
        await Expect(generate.Or(dlg.GetByRole(AriaRole.Button, new() { Name = "Tạo lại" })).First).ToBeVisibleAsync();
        if (await generate.CountAsync() > 0)
        {
            var gen = await leader.RunAndWaitForResponseAsync(() => generate.ClickAsync(),
                r => r.Url.Contains("/grade-suggestions/generate"), new() { Timeout = 120000 });
            gen.Status.Should().Be(200, "tạo gợi ý AI: " + await gen.TextAsync());
        }
        else
        {
            await dlg.GetByRole(AriaRole.Button, new() { Name = "Tạo lại" }).ClickAsync();
            await leader.Locator(".ant-modal-confirm").GetByRole(AriaRole.Button, new() { Name = "Tạo lại" }).ClickAsync();
        }
        await Expect(confirm).ToBeEnabledAsync(new() { Timeout = 120000 });
        await dlg.GetByRole(AriaRole.Button, new() { Name = "Tạo lại" }).ClickAsync();
        await leader.Locator(".ant-modal-confirm").GetByRole(AriaRole.Button, new() { Name = "Tạo lại" }).ClickAsync();
        await Expect(confirm).ToBeEnabledAsync(new() { Timeout = 120000 });
        var remove = dlg.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Bỏ|Xóa|delete)") });
        var total = await remove.CountAsync();
        total.Should().BeGreaterThan(2);
        for (var left = total; left > 2; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }
        await Expect(confirm).ToContainTextAsync("(2)");
        await confirm.ClickAsync();
        await leader.Locator(".ant-modal-confirm").GetByRole(AriaRole.Button, new() { Name = "Xác nhận" }).ClickAsync();
        await Expect(leader.Locator(".ant-message-success")).ToBeVisibleAsync();
        var admin = await AsAsync("Admin");
        await admin.GotoAsync("/hr/grades");
        await Expect(Row(admin, "Lê Văn Đức").Filter(new() { HasText = "Chờ quyết định" })).ToHaveCountAsync(2);
    }
}
