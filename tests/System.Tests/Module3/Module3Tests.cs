using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Playwright;
using static System.Tests.UiKit;

namespace System.Tests.Module3;

public class Module3Tests : BaseSystemTest, IClassFixture<CleanupFixture>
{
    private const string Required = "Vui lòng nhập thông tin này.";
    private static readonly SemaphoreSlim ProductLock = new(1, 1);
    private static string? _product;

    private async Task<IPage> AsAsync(string role)
    {
        var page = await NewPageAsRoleAsync(role);
        page.SetDefaultTimeout(15000);
        return page;
    }

    private static ILocator FieldErrors(ILocator scope) => scope.Locator(".ant-form-item-explain-error");

    private static async Task<string> TokenAsync(IPage page) =>
        await page.EvaluateAsync<string>("localStorage.getItem('nxmtd.accessToken')");

    private static async Task<Dictionary<string, string>> AuthAsync(IPage page) =>
        new() { ["Authorization"] = $"Bearer {await TokenAsync(page)}" };

    private static async Task<IResponse> SaveAsync(IPage page, ILocator dlg, string urlPart, string button = "Lưu") =>
        await page.RunAndWaitForResponseAsync(() => Button(dlg, button).ClickAsync(),
            r => r.Url.Contains(urlPart) && r.Request.Method is "POST" or "PUT" && !r.Url.Contains("excel"));

    // ---------------------------------------------------------------- khách hàng

    private static async Task<ILocator> OpenCustomerFormAsync(IPage page)
    {
        await page.GotoAsync("/admin/customers");
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm khách" }).ClickAsync();
        return Dialog(page, "Thêm khách");
    }

    private static string Phone() => "09" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("00000000");

    private static async Task FillCustomerAsync(ILocator dlg, string name, string? phone = null,
        string? email = null, string address = "ST_AUTO_ 12 Nguyen Trai, TP.HCM")
    {
        await dlg.GetByLabel("Tên khách hàng").FillAsync(name);
        await dlg.GetByLabel("Số điện thoại").FillAsync(phone ?? Phone());
        await dlg.GetByLabel("Email").FillAsync(email ?? $"st_auto{Next()}@example.com");
        await dlg.GetByLabel("Địa chỉ mặc định").FillAsync(address);
    }

    private static async Task<ILocator> CustomerRowAsync(IPage page, string name)
    {
        await page.GotoAsync("/admin/customers");
        var search = page.GetByPlaceholder("Tìm theo mã, tên...");
        await search.FillAsync(name.Length > 60 ? name[..60] : name);
        await search.PressAsync("Enter");
        return Row(page, name);
    }

    private async Task<string> CreateCustomerAsync(IPage page)
    {
        var name = "ST_AUTO_ KH " + Next();
        var dlg = await OpenCustomerFormAsync(page);
        await FillCustomerAsync(dlg, name);
        (await SaveAsync(page, dlg, "/admin/customers")).Status.Should().BeInRange(200, 299);
        return name;
    }

    private static async Task<ILocator> OrderCustomerOptionsAsync(IPage page, string name)
    {
        await page.GotoAsync("/admin/orders/new");
        var box = page.GetByLabel("Khách hàng");
        await box.ClickAsync();
        await box.FillAsync(name);
        return Options(page).Filter(new() { HasText = name });
    }

    [Fact]
    public async Task ST_M3_001()
    {
        CaseId = "ST_M3_001";
        var admin = await AsAsync("Admin");
        var name = "ST_AUTO_ Cty May Á Châu " + Next();
        var dlg = await OpenCustomerFormAsync(admin);
        await FillCustomerAsync(dlg, name);
        var resp = await SaveAsync(admin, dlg, "/admin/customers");
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        await Expect(await CustomerRowAsync(admin, name)).ToHaveCountAsync(1);
        await Expect(await OrderCustomerOptionsAsync(admin, name)).ToHaveCountAsync(1);
        var options = await admin.Context.APIRequest.GetAsync($"{BaseUrl}/api/v1/admin/customers/options",
            new() { Headers = await AuthAsync(admin) });
        JsonDocument.Parse(await options.TextAsync()).RootElement.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("customerName").GetString()).Should().Contain(name);
        resp.Status.Should().Be(201);
    }

    [Fact]
    public async Task ST_M3_002()
    {
        CaseId = "ST_M3_002";
        var admin = await AsAsync("Admin");
        var name = "ST_AUTO_ KH " + Next();
        var dlg = await OpenCustomerFormAsync(admin);
        await dlg.GetByLabel("Tên khách hàng").FillAsync(name);
        await dlg.GetByLabel("Địa chỉ mặc định").FillAsync("ST_AUTO_ dia chi");
        await Button(dlg, "Lưu").ClickAsync();
        await Expect(Toast(admin, "Đã lưu.")).ToBeVisibleAsync();
        var row = await CustomerRowAsync(admin, name);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).Not.ToContainTextAsync(new Regex("null|undefined"));
    }

    [Fact]
    public async Task ST_M3_003()
    {
        CaseId = "ST_M3_003";
        var admin = await AsAsync("Admin");
        var dlg = await OpenCustomerFormAsync(admin);
        var name = ("ST_AUTO_ " + Next() + new string('a', 200))[..200];
        var address = ("ST_AUTO_ " + new string('b', 500))[..500];
        var email = (Next() + new string('a', 64))[..64] + "@" + new string('b', 31) + ".com";
        email.Length.Should().Be(100);
        foreach (var (label, value, max) in new[] { ("Tên khách hàng", name, 200), ("Địa chỉ mặc định", address, 500), ("Email", email, 100) })
        {
            var input = dlg.GetByLabel(label);
            await input.FillAsync(value + "x");
            await input.BlurAsync();
            if ((await input.InputValueAsync()).Length > max)
                await Expect(FieldErrors(dlg).First).ToBeVisibleAsync();
            await input.FillAsync("");
        }
        await dlg.GetByLabel("Email").FillAsync("abc@");
        await dlg.GetByLabel("Email").BlurAsync();
        await Expect(FieldErrors(dlg).Filter(new() { HasText = "Email" }).First).ToBeVisibleAsync();
        await FillCustomerAsync(dlg, name, email: email, address: "");
        await Button(dlg, "Lưu").ClickAsync();
        await Expect(FieldErrors(dlg).First).ToBeVisibleAsync();
        await dlg.GetByLabel("Địa chỉ mặc định").FillAsync(address);
        (await SaveAsync(admin, dlg, "/admin/customers")).Status.Should().BeInRange(200, 299);
        var row = await CustomerRowAsync(admin, name);
        await Expect(row).ToContainTextAsync(name);
        await Expect(row).ToContainTextAsync(email);
        await Expect(row).ToContainTextAsync(address);
    }

    [Fact]
    public async Task ST_M3_004()
    {
        CaseId = "ST_M3_004";
        var admin = await AsAsync("Admin");
        var name = await CreateCustomerAsync(admin);
        var dlg = await OpenCustomerFormAsync(admin);
        var second = Phone();
        await FillCustomerAsync(dlg, name, phone: second);
        var resp = await SaveAsync(admin, dlg, "/admin/customers");
        if (resp.Status >= 400)
        {
            resp.Status.Should().BeLessThan(500);
            await Expect(Errors(admin).First).ToBeVisibleAsync();
            await Expect(dlg.GetByLabel("Tên khách hàng")).ToHaveValueAsync(name);
        }
        else
        {
            var row = await CustomerRowAsync(admin, name);
            await Expect(row).ToHaveCountAsync(2);
            await Expect(row.Filter(new() { HasText = second })).ToHaveCountAsync(1);
        }
    }

    [Fact]
    public async Task ST_M3_005()
    {
        CaseId = "ST_M3_005";
        var admin = await AsAsync("Admin");
        var name = await CreateCustomerAsync(admin);
        var row = await CustomerRowAsync(admin, name);
        await Button(row, "Ngừng hợp tác").ClickAsync();
        var modal = Dialog(admin, "Ngừng hợp tác");
        (await SaveAsync(admin, modal, "/discontinue", "Xác nhận")).Status.Should().BeInRange(200, 299);
        row = await CustomerRowAsync(admin, name);
        await Expect(row).ToContainTextAsync("Ngừng hợp tác");
        await Expect(Button(row, "Hợp tác lại")).ToBeVisibleAsync();
        var options = await OrderCustomerOptionsAsync(admin, name);
        await Expect(admin.Locator(".ant-select-dropdown:visible")).ToBeVisibleAsync();
        await Expect(options).ToHaveCountAsync(0);
        row = await CustomerRowAsync(admin, name);
        await admin.RunAndWaitForResponseAsync(async () =>
        {
            await Button(row, "Hợp tác lại").ClickAsync();
            var confirm = Button(admin, "Xác nhận").Last;
            try { await confirm.ClickAsync(new() { Timeout = 3000 }); }
            catch (TimeoutException) { }
        }, r => r.Url.Contains("/reactivate"));
        await Expect(await OrderCustomerOptionsAsync(admin, name)).ToHaveCountAsync(1);
    }

    // ---------------------------------------------------------------- mã hàng

    private static byte[] ProductWorkbook(string code)
    {
        using var wb = new XLWorkbook(Path.Combine(AppContext.BaseDirectory, "TestData", "product_template.xlsx"));
        wb.Worksheet("Mã hàng").Cell("A4").Value = code;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static FilePayload Payload(string name, string mime, byte[] data) => new() { Name = name, MimeType = mime, Buffer = data };

    private static async Task ChooseAsync(IPage page, ILocator trigger, params FilePayload[] files)
    {
        var chooser = await page.RunAndWaitForFileChooserAsync(() => trigger.ClickAsync());
        await chooser.SetFilesAsync(files);
    }

    private static async Task<ILocator> OpenProductFormAsync(IPage page)
    {
        await page.GotoAsync("/admin/products");
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm mới" }).ClickAsync();
        return Dialog(page, "Thêm mới");
    }

    private static async Task ImportSpecAsync(IPage page, ILocator dlg, string code)
    {
        await ChooseAsync(page, dlg.GetByRole(AriaRole.Button, new() { Name = "Chọn 1 file chung cho tất cả" }),
            Payload("st_auto_mahang.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ProductWorkbook(code)));
        var review = Dialog(page, "Kiểm tra dữ liệu từ Excel");
        await Button(review, "Xác nhận").ClickAsync();
        await Assertions.Expect(review).ToHaveCountAsync(0);
    }

    private static async Task<string> CreateProductAsync(IPage admin)
    {
        var code = "ST_AUTO_P" + Next();
        var dlg = await OpenProductFormAsync(admin);
        await dlg.GetByLabel("Mã hàng").FillAsync(code);
        await ImportSpecAsync(admin, dlg, code);
        var resp = await admin.RunAndWaitForResponseAsync(() => Button(dlg, "Lưu").ClickAsync(),
            r => r.Url.EndsWith("/admin/products") && r.Request.Method == "POST", new() { Timeout = 30000 });
        resp.Status.Should().BeInRange(200, 299, "tạo mã hàng mồi: " + await resp.TextAsync());
        await Assertions.Expect(dlg).ToHaveCountAsync(0, new() { Timeout = 30000 });
        return code;
    }

    private static async Task<ILocator> ProductRowAsync(IPage page, string code, string path = "/admin/products")
    {
        await page.GotoAsync(path);
        var search = page.GetByPlaceholder("Tìm theo mã, tên...");
        await search.FillAsync(code);
        await search.PressAsync("Enter");
        return page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true }) });
    }

    private static async Task<byte[]> ImageAsync(IPage page, string mime) =>
        Convert.FromBase64String(await page.EvaluateAsync<string>(
            "m => { const c = document.createElement('canvas'); c.width = 60; c.height = 60; const x = c.getContext('2d'); x.fillStyle = '#2a7'; x.fillRect(0,0,60,60); return c.toDataURL(m).split(',')[1]; }", mime));

    [Fact]
    public async Task ST_M3_006()
    {
        CaseId = "ST_M3_006";
        var admin = await AsAsync("Admin");
        var upper = "ST_AUTO_AO-POLO." + Next();
        var dlg = await OpenProductFormAsync(admin);
        await dlg.GetByLabel("Mã hàng").FillAsync(upper.ToLowerInvariant());
        await dlg.GetByLabel("Mô tả").FillAsync("ST_AUTO_ mo ta");
        await ChooseAsync(admin, dlg.GetByRole(AriaRole.Button, new() { Name = "Chọn ảnh từ máy" }),
            Payload("chinh.png", "image/png", await ImageAsync(admin, "image/png")));
        await ChooseAsync(admin, dlg.GetByRole(AriaRole.Button, new() { Name = "Thêm ảnh" }),
            Payload("phu.jpg", "image/jpeg", await ImageAsync(admin, "image/jpeg")));
        await Expect(dlg).ToContainTextAsync("1/5 ảnh");
        await ImportSpecAsync(admin, dlg, upper);
        await Expect(dlg.GetByLabel("Mã hàng")).ToHaveValueAsync(upper);
        var resp = await admin.RunAndWaitForResponseAsync(() => Button(dlg, "Lưu").ClickAsync(),
            r => r.Url.EndsWith("/admin/products") && r.Request.Method == "POST", new() { Timeout = 30000 });
        resp.Status.Should().BeInRange(200, 299);
        resp.Request.PostData.Should().Contain($"\"productCode\":\"{upper}\"");
        await Expect(Toast(admin, "Đã lưu.").First).ToBeVisibleAsync();
        var row = await ProductRowAsync(admin, upper);
        await Expect(row.Locator("img").First).ToBeVisibleAsync();
        await Expect(row.Locator("img").First).ToHaveJSPropertyAsync("complete", true);
        (await row.Locator("img").First.EvaluateAsync<int>("i => i.naturalWidth")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ST_M3_007()
    {
        CaseId = "ST_M3_007";
        var admin = await AsAsync("Admin");
        var dlg = await OpenProductFormAsync(admin);
        var input = dlg.GetByLabel("Mã hàng");
        var codeError = dlg.Locator(".ant-form-item").Filter(new() { Has = admin.GetByLabel("Mã hàng") })
            .Locator(".ant-form-item-explain-error");
        var thirty = ("ST_AUTO_" + Next() + new string('A', 30))[..30];
        await input.FillAsync(thirty);
        await input.BlurAsync();
        await Expect(codeError).ToHaveCountAsync(0);
        foreach (var bad in new[] { thirty + "A", "ÁO01", "AO 03" })
        {
            await input.FillAsync(bad);
            await input.BlurAsync();
            await Expect(codeError.First).ToBeVisibleAsync();
        }
        var code = "ST_AUTO_T" + Next();
        await input.FillAsync($"  {code}  ");
        await input.BlurAsync();
        await Expect(codeError).ToHaveCountAsync(0);
        await ImportSpecAsync(admin, dlg, code);
        var resp = await admin.RunAndWaitForResponseAsync(() => Button(dlg, "Lưu").ClickAsync(),
            r => r.Url.EndsWith("/admin/products") && r.Request.Method == "POST", new() { Timeout = 30000 });
        resp.Status.Should().BeInRange(200, 299);
        resp.Request.PostData.Should().Contain($"\"productCode\":\"{code}\"");
        await Expect(await ProductRowAsync(admin, code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M3_008()
    {
        CaseId = "ST_M3_008";
        var admin = await AsAsync("Admin");
        var dlg = await OpenProductFormAsync(admin);
        await dlg.GetByLabel("Mã hàng").FillAsync("ak-0522");
        await dlg.GetByLabel("Mã hàng").BlurAsync();
        await Button(dlg, "Lưu").ClickAsync();
        await Expect(admin.GetByText(new Regex("đã tồn tại|đã có trong hệ thống")).First).ToBeVisibleAsync();
        await admin.RouteAsync(new Regex(@"/api/v1/admin/products\?"), r =>
            r.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"success\":false,\"message\":\"Loi gia lap\"}" }));
        dlg = await OpenProductFormAsync(admin);
        var code = "ST_AUTO_K" + Next();
        await dlg.GetByLabel("Mã hàng").FillAsync(code);
        await dlg.GetByLabel("Mã hàng").BlurAsync();
        await Button(dlg, "Lưu").ClickAsync();
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await Expect(dlg.Locator(".ant-btn-loading")).ToHaveCountAsync(0);
        await admin.UnrouteAllAsync();
        await Expect(await ProductRowAsync(admin, code)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ST_M3_009()
    {
        CaseId = "ST_M3_009";
        var admin = await AsAsync("Admin");
        var dlg = await OpenProductFormAsync(admin);
        var main = dlg.GetByRole(AriaRole.Button, new() { Name = "Chọn ảnh từ máy" });
        foreach (var bad in new[] { Payload("a.gif", "image/gif", Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7")),
                                    Payload("a.pdf", "application/pdf", System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 test")) })
        {
            await ChooseAsync(admin, main, bad);
            await Expect(Errors(admin).First).ToBeVisibleAsync();
            await Expect(main).ToBeVisibleAsync();
        }
        var png = await ImageAsync(admin, "image/png");
        var add = dlg.GetByRole(AriaRole.Button, new() { Name = "Thêm ảnh" });
        await ChooseAsync(admin, add, Enumerable.Range(1, 6).Select(i => Payload($"p{i}.png", "image/png", png)).ToArray());
        await Expect(dlg).ToContainTextAsync("5/5 ảnh");
        await Expect(dlg).Not.ToContainTextAsync("6/5 ảnh");
        var huge = new byte[20 * 1024 * 1024];
        png.CopyTo(huge, 0);
        await ChooseAsync(admin, main, Payload("to.png", "image/png", huge));
        await Expect(Errors(admin).First).ToBeVisibleAsync(new() { Timeout = 60000 });
    }

    [Fact]
    public async Task ST_M3_010()
    {
        CaseId = "ST_M3_010";
        var admin = await AsAsync("Admin");
        const string code = "AK-0522";
        var row = await ProductRowAsync(admin, code);
        await Button(row, "Sửa").ClickAsync();
        var dlg = admin.GetByRole(AriaRole.Dialog).Filter(new() { Has = admin.GetByLabel("Mô tả") });
        var input = dlg.GetByLabel("Mã hàng");
        await Expect(input).ToBeDisabledAsync();
        await input.HoverAsync(new() { Force = true });
        await Expect(admin.GetByRole(AriaRole.Tooltip)).ToContainTextAsync("Mã hàng đã dùng trong đơn hàng, không đổi được mã.");
        var description = dlg.GetByLabel("Mô tả");
        await Expect(description).Not.ToHaveValueAsync("");
        var original = await description.InputValueAsync();
        var changed = "ST_AUTO_ " + Next();
        try
        {
            await description.FillAsync(changed);
            var resp = await SaveAsync(admin, dlg, "/admin/products/");
            resp.Request.Method.Should().Be("PUT");
            resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
            var body = resp.Request.PostData ?? "";
            if (body.Contains("\"productCode\"")) body.Should().Contain($"\"productCode\":\"{code}\"");
            row = await ProductRowAsync(admin, code);
            await Button(row, "Sửa").ClickAsync();
            await Expect(description).ToHaveValueAsync(changed);
            await admin.GotoAsync("/admin/orders");
            await Button(Row(admin, "DH-2511-005"), "Xem").ClickAsync();
            await Expect(admin.GetByText(code).First).ToBeVisibleAsync();
        }
        finally
        {
            row = await ProductRowAsync(admin, code);
            await Button(row, "Sửa").ClickAsync();
            await Expect(description).Not.ToHaveValueAsync("");
            if (await description.InputValueAsync() != original)
            {
                await description.FillAsync(original);
                await SaveAsync(admin, dlg, "/admin/products/");
            }
        }
    }

    private static async Task<ILocator> OpenSpecAsync(IPage page, string code)
    {
        var row = await ProductRowAsync(page, code);
        await Button(row, "Định mức").ClickAsync();
        var spec = Dialog(page, "Định mức mã hàng");
        await spec.GetByRole(AriaRole.Tab).First.WaitForAsync();
        return spec;
    }

    [Fact]
    public async Task ST_M3_011()
    {
        CaseId = "ST_M3_011";
        var admin = await AsAsync("Admin");
        var code = await CreateProductAsync(admin);
        var spec = await OpenSpecAsync(admin, code);
        foreach (var tab in new[] { "Thành phần", "Màu – size", "Định mức NVL", "Nhóm bìa" })
            await Expect(spec.GetByRole(AriaRole.Tab, new() { Name = tab })).ToBeVisibleAsync();
        await spec.GetByRole(AriaRole.Button, new() { Name = "Thêm thành phần" }).ClickAsync();
        var added = spec.GetByRole(AriaRole.Tabpanel).GetByRole(AriaRole.Row).Last;
        await Button(spec, "Lưu").ClickAsync();
        await Expect(spec.GetByText("Mỗi thành phần phải có tên.")).ToBeVisibleAsync();
        var part = "ST_AUTO_ chi tiet " + Next();
        await added.GetByRole(AriaRole.Textbox).First.FillAsync(part);
        var resp = await SaveAsync(admin, spec, "/spec");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(Toast(admin, "Đã lưu").First).ToBeVisibleAsync();
        spec = await OpenSpecAsync(admin, code);
        await Expect(spec.GetByRole(AriaRole.Tabpanel).Locator($"input[value='{part}']")).ToHaveCountAsync(1);
        var acc = await AsAsync("Accountant");
        var form = await OpenOperationFormAsync(acc, code);
        await form.GetByLabel("Chi tiết vải công đoạn làm trên").ClickAsync();
        await Expect(Options(acc).Filter(new() { HasText = "Thân trước" }).First).ToBeVisibleAsync();
        await form.GetByLabel("Chi tiết vải công đoạn làm trên").FillAsync(part);
        await Expect(Options(acc).Filter(new() { HasText = part })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M3_012()
    {
        CaseId = "ST_M3_012";
        var acc = await AsAsync("Accountant");
        await GotoAndWaitAsync(acc, "/accounting/products");
        PathOf(acc).Should().Be("/accounting/products");
        await Expect(acc.GetByRole(AriaRole.Button, new() { Name = "Thêm mới" })).ToHaveCountAsync(0);
        await Expect(acc.GetByRole(AriaRole.Button, new() { Name = "Định mức" }).First).ToBeVisibleAsync();
        foreach (var path in new[] { "/admin/products", "/admin/customers" })
        {
            await acc.GotoAsync(path);
            await acc.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        }
        var resp = await acc.Context.APIRequest.PutAsync($"{BaseUrl}/api/v1/admin/products/1",
            new() { Headers = await AuthAsync(acc), DataObject = new { productCode = "ST_AUTO_HACK", description = "ST_AUTO_" } });
        resp.Status.Should().Be(403);
    }

    // ---------------------------------------------------------------- công đoạn

    private async Task<string> SharedProductAsync()
    {
        await ProductLock.WaitAsync();
        try { return _product ??= await CreateProductAsync(await AsAsync("Admin")); }
        finally { ProductLock.Release(); }
    }

    private static async Task SelectProductAsync(IPage page, string code)
    {
        await page.GotoAsync("/operations");
        var box = page.GetByRole(AriaRole.Main).GetByRole(AriaRole.Combobox).First;
        await box.ClickAsync();
        await box.FillAsync(code);
        await page.RunAndWaitForResponseAsync(() => Options(page).Filter(new() { HasText = code }).First.ClickAsync(),
            r => r.Url.Contains("/operations?"));
    }

    private static async Task<ILocator> OpenOperationFormAsync(IPage page, string product)
    {
        await SelectProductAsync(page, product);
        await page.GetByRole(AriaRole.Button, new() { Name = "Thêm công đoạn" }).ClickAsync();
        var dlg = Dialog(page, "Thêm công đoạn");
        await dlg.GetByLabel("Mã công đoạn").WaitForAsync();
        return dlg;
    }

    private static async Task FillOperationAsync(ILocator dlg, string code, int sequence, string rate = "1500")
    {
        await dlg.GetByLabel("Mã công đoạn").FillAsync(code);
        await dlg.GetByLabel("Tên công đoạn").FillAsync("ST_AUTO_ cong doan " + code);
        await dlg.GetByLabel("Thứ tự").FillAsync(sequence.ToString());
        await dlg.GetByLabel("Đơn giá (₫").FillAsync(rate);
    }

    private static string OperationCode() => "ST_AUTO_CD" + Next();
    private static int Sequence() => int.Parse(Next()) % 30000 + 100;

    private async Task<(IPage Page, string Product, string Code)> NewOperationAsync(bool final = false, IPage? page = null)
    {
        var product = await SharedProductAsync();
        page ??= await AsAsync("Accountant");
        var dlg = await OpenOperationFormAsync(page, product);
        var code = OperationCode();
        await FillOperationAsync(dlg, code, Sequence());
        if (final) await dlg.GetByLabel("Là công đoạn cuối").CheckAsync();
        var resp = await SaveAsync(page, dlg, "/operations");
        resp.Status.Should().BeInRange(200, 299, "tạo công đoạn mồi: " + await resp.TextAsync());
        await Assertions.Expect(dlg).ToHaveCountAsync(0);
        return (page, product, code);
    }

    private static async Task<ILocator> OperationRowAsync(IPage page, string product, string code)
    {
        await SelectProductAsync(page, product);
        await page.GetByPlaceholder("Tìm mã, tên").FillAsync(code);
        return Row(page, code);
    }

    [Fact]
    public async Task ST_M3_013()
    {
        CaseId = "ST_M3_013";
        var product = await SharedProductAsync();
        var acc = await AsAsync("Accountant");
        var dlg = await OpenOperationFormAsync(acc, product);
        var code = OperationCode();
        var group = "ST_AUTO_ May tay " + Next();
        await FillOperationAsync(dlg, code, 1);
        await dlg.GetByLabel("Bậc (1-9)").FillAsync("3");
        await dlg.GetByLabel("TG chuẩn (giây)").FillAsync("12.5");
        await dlg.GetByLabel("Nhóm công đoạn").FillAsync(group);
        await dlg.GetByLabel("Nhóm công đoạn").PressAsync("Enter");
        var parts = dlg.GetByLabel("Chi tiết vải công đoạn làm trên");
        await parts.ClickAsync();
        await Options(acc).Filter(new() { HasText = "Thân trước" }).First.ClickAsync();
        await dlg.Locator(".ant-modal-title").ClickAsync();
        var resp = await SaveAsync(acc, dlg, "/operations");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        resp.Request.PostData.Should().Contain(code).And.Contain("1500").And.Contain(group);
        await Expect(Toast(acc, "Đã lưu.").First).ToBeVisibleAsync();
        var row = await OperationRowAsync(acc, product, code);
        await Expect(row).ToContainTextAsync("1.500");
        await Expect(row).ToContainTextAsync("Nháp");
        var (u, p) = GetAccount("Accountant");
        var again = await NewAnonymousPageAsync();
        await LoginUiAsync(again, u, p);
        InvalidateRole("Accountant");
        await Expect(await OperationRowAsync(again, product, code)).ToContainTextAsync("Nháp");
        resp.Request.PostData.Should().Contain("\"standardTimeSec\":12.5", "payload gửi " + resp.Request.PostData);
    }

    private static async Task CheckFieldAsync(ILocator dlg, string label, string value, bool valid)
    {
        var input = dlg.GetByLabel(label);
        await input.FillAsync(value);
        await input.BlurAsync();
        var error = dlg.Locator(".ant-form-item").Filter(new() { Has = dlg.Page.GetByLabel(label) }).Last
            .Locator(".ant-form-item-explain-error");
        if (valid) await Assertions.Expect(error).ToHaveCountAsync(0);
        else await Assertions.Expect(error.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M3_014()
    {
        CaseId = "ST_M3_014";
        var product = await SharedProductAsync();
        var acc = await AsAsync("Accountant");
        var dlg = await OpenOperationFormAsync(acc, product);
        var codeInput = dlg.GetByLabel("Mã công đoạn");
        var thirty = ("ST_AUTO_CD" + Next() + new string('A', 30))[..30];
        await codeInput.FillAsync("");
        await codeInput.PressSequentiallyAsync(thirty + "A");
        (await codeInput.InputValueAsync()).Length.Should().BeLessOrEqualTo(30);
        await codeInput.FillAsync("CD#1");
        await codeInput.BlurAsync();
        await SoftAsync("CD#1 không báo lỗi pattern (ô tự bỏ ký tự #)", () => Expect(dlg.GetByText("Mã chỉ gồm chữ không dấu, số và các ký tự . _ -")).ToBeVisibleAsync());
        await codeInput.FillAsync(thirty);
        var nameInput = dlg.GetByLabel("Tên công đoạn");
        await nameInput.FillAsync("");
        await nameInput.PressSequentiallyAsync(new string('n', 201), new() { Timeout = 60000 });
        (await nameInput.InputValueAsync()).Length.Should().Be(200);
        foreach (var (label, values, valid) in new[]
                 {
                     ("Thứ tự", new[] { "1", "32767" }, true), ("Bậc (1-9)", new[] { "1", "9" }, true),
                     ("TG chuẩn (giây)", new[] { "0.01", "9999999.99", "" }, true), ("Thứ tự", new[] { "0", "32768" }, false),
                     ("Bậc (1-9)", new[] { "0", "10" }, false), ("TG chuẩn (giây)", new[] { "0" }, false),
                 })
            foreach (var v in values)
                await SoftAsync($"{label}='{v}' {(valid ? "phải hợp lệ" : "không báo lỗi")}", () => CheckFieldAsync(dlg, label, v, valid));
        await dlg.GetByLabel("Thứ tự").FillAsync(Sequence().ToString());
        await dlg.GetByLabel("Bậc (1-9)").FillAsync("9");
        await dlg.GetByLabel("TG chuẩn (giây)").FillAsync("");
        await dlg.GetByLabel("Đơn giá (₫").FillAsync("1000");
        var resp = await SaveAsync(acc, dlg, "/operations");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        var row = await OperationRowAsync(acc, product, thirty);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row.GetByRole(AriaRole.Cell, new() { Name = "0", Exact = true })).ToHaveCountAsync(0);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_015()
    {
        CaseId = "ST_M3_015";
        var product = await SharedProductAsync();
        var acc = await AsAsync("Accountant");
        var dlg = await OpenOperationFormAsync(acc, product);
        await FillOperationAsync(dlg, OperationCode(), Sequence());
        var rate = dlg.GetByLabel("Đơn giá (₫");
        foreach (var (value, message) in new[] { ("0", "Đơn giá phải lớn hơn 0."), ("100000001", "Đơn giá không được vượt quá 100.000.000 đồng.") })
        {
            await rate.FillAsync(value);
            await rate.BlurAsync();
            await Button(dlg, "Lưu").ClickAsync();
            await SoftAsync($"đơn giá {value} không hiện đúng câu '{message}'", () => Expect(dlg.GetByText(message)).ToBeVisibleAsync());
        }
        await Button(dlg, "Hủy").ClickAsync();
        foreach (var (value, shown) in new[] { ("0.01", "0,01"), ("100000000", "100.000.000") })
        {
            dlg = await OpenOperationFormAsync(acc, product);
            var code = OperationCode();
            await FillOperationAsync(dlg, code, Sequence(), value);
            var resp = await SaveAsync(acc, dlg, "/operations");
            resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
            var saved = await OperationRowAsync(acc, product, code);
            await SoftAsync($"đơn giá nhập {value} không được lưu/hiển thị là {shown}", () => Expect(saved).ToContainTextAsync(shown));
        }
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_016()
    {
        CaseId = "ST_M3_016";
        var (acc, product, first) = await NewOperationAsync(final: true);
        var (_, _, second) = await NewOperationAsync(page: acc);
        var row = await OperationRowAsync(acc, product, second);
        await Button(row, "Sửa").ClickAsync();
        var dlg = Dialog(acc, "Sửa công đoạn");
        await dlg.GetByLabel("Là công đoạn cuối").CheckAsync();
        await SoftAsync("không có thông báo công đoạn cuối hiện tại sẽ chuyển", () => Expect(dlg.GetByText("Công đoạn cuối hiện tại sẽ chuyển sang công đoạn này.")).ToBeVisibleAsync());
        var resp = await SaveAsync(acc, dlg, "/operations/");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        var list = await acc.RunAndWaitForResponseAsync(() => SelectProductAsync(acc, product),
            r => r.Url.Contains("/operations?"));
        var url = Regex.Replace(list.Url, "pageSize=\\d+", "pageSize=100");
        var api = await acc.Context.APIRequest.GetAsync(url, new() { Headers = await AuthAsync(acc) });
        var data = JsonDocument.Parse(await api.TextAsync()).RootElement.GetProperty("data");
        var items = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("items");
        var finals = items.EnumerateArray()
            .Where(e => (e.TryGetProperty("isFinalStep", out var a) && a.GetBoolean()) || (e.TryGetProperty("isFinal", out var b) && b.GetBoolean()))
            .Select(e => e.GetProperty("operationCode").GetString()).ToList();
        finals.Should().Equal(second);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_017()
    {
        CaseId = "ST_M3_017";
        var (acc, product, code) = await NewOperationAsync();
        var dlg = await OpenOperationFormAsync(acc, product);
        await FillOperationAsync(dlg, code, Sequence());
        var resp = await SaveAsync(acc, dlg, "/operations");
        resp.Status.Should().BeInRange(400, 499);
        await Expect(Errors(acc).First).ToBeVisibleAsync();
        await Expect(dlg.GetByLabel("Mã công đoạn")).ToHaveValueAsync(code);
        await Expect(await OperationRowAsync(acc, product, code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M3_018()
    {
        CaseId = "ST_M3_018";
        var product = await SharedProductAsync();
        var acc = await AsAsync("Accountant");
        var dlg = await OpenOperationFormAsync(acc, product);
        var code = OperationCode();
        await FillOperationAsync(dlg, code, Sequence());
        await DateAsync(dlg.GetByLabel("Đơn giá hiệu lực từ"), "01/01/2025");
        await SoftAsync("không hiện gợi ý ngày hiệu lực đã qua (pastDateHint)", () => Expect(dlg.GetByText("Ngày hiệu lực đã qua")).ToBeVisibleAsync());
        var resp = await SaveAsync(acc, dlg, "/operations");
        if (resp.Status < 300)
        {
            resp.Request.PostData.Should().Contain("2025-01-01");
            await Button(await OperationRowAsync(acc, product, code), "Xem").ClickAsync();
            await Expect(acc.GetByRole(AriaRole.Dialog).Last).ToContainTextAsync("01/01/2025");
        }
        else
        {
            resp.Status.Should().BeLessThan(500);
            await Expect(Errors(acc).First).ToBeVisibleAsync();
        }
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_019()
    {
        CaseId = "ST_M3_019";
        var (acc, product, code) = await NewOperationAsync();
        var group = "ST_AUTO_ May tay " + Next();
        await SelectProductAsync(acc, product);
        await acc.GetByRole(AriaRole.Button, new() { Name = "Nhóm công đoạn" }).ClickAsync();
        var dlg = Dialog(acc, "Nhóm công đoạn");
        var input = dlg.GetByPlaceholder("Tên nhóm mới, vd May tay");
        await input.PressSequentiallyAsync(new string('g', 201), new() { Timeout = 60000 });
        (await input.InputValueAsync()).Length.Should().Be(200);
        await input.FillAsync(group);
        var add = await SaveAsync(acc, dlg, "/operation-groups", "Thêm nhóm");
        add.Status.Should().BeInRange(200, 299);
        await Expect(Row(dlg, group)).ToHaveCountAsync(1);
        var row = await OperationRowAsync(acc, product, code);
        await Button(row, "Sửa").ClickAsync();
        var edit = Dialog(acc, "Sửa công đoạn");
        await edit.GetByLabel("Nhóm công đoạn").ClickAsync();
        await edit.GetByLabel("Nhóm công đoạn").FillAsync(group);
        await Options(acc).Filter(new() { HasText = group }).First.ClickAsync();
        (await SaveAsync(acc, edit, "/operations/")).Status.Should().BeInRange(200, 299);
        await SelectProductAsync(acc, product);
        await acc.GetByRole(AriaRole.Button, new() { Name = "Nhóm công đoạn" }).ClickAsync();
        await Expect(Row(dlg, group)).ToHaveCountAsync(1);
        await Button(Row(dlg, group), "Xóa nhóm").ClickAsync();
        await ConfirmAsync(acc);
        await Expect(acc.Locator(".ant-message-notice, .ant-notification-notice").First).ToBeVisibleAsync();
    }

    private static async Task<ILocator> OpenOperationImportAsync(IPage page, string product)
    {
        await SelectProductAsync(page, product);
        await page.GetByRole(AriaRole.Button, new() { Name = "Nhập từ Excel" }).ClickAsync();
        return Dialog(page, "Nhập công đoạn");
    }

    private static Task UploadAsync(IPage page, ILocator dlg, string name, byte[] data) =>
        ChooseAsync(page, dlg.GetByRole(AriaRole.Button, new() { Name = "Chọn file Excel" }),
            Payload(name, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", data));

    private static byte[] OperationWorkbook(params (string Name, int Price)[] steps)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("QTCN");
        var header = new[] { "STT", "BƯỚC CÔNG VIỆC", "BẬC", "TGCT", "THIẾT BỊ", "TIỀN" };
        for (var c = 0; c < header.Length; c++) ws.Cell(1, c + 1).Value = header[c];
        for (var i = 0; i < steps.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = i + 201;
            ws.Cell(i + 2, 2).Value = steps[i].Name;
            ws.Cell(i + 2, 3).Value = 3;
            ws.Cell(i + 2, 4).Value = 20;
            ws.Cell(i + 2, 5).Value = "1K";
            ws.Cell(i + 2, 6).Value = steps[i].Price;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task ST_M3_020()
    {
        CaseId = "ST_M3_020";
        var admin = await AsAsync("Admin");
        var product = await CreateProductAsync(admin);
        var acc = await AsAsync("Accountant");
        var dlg = await OpenOperationImportAsync(acc, product);
        var tag = Next();
        await UploadAsync(acc, dlg, "st_auto_qtcn.xlsx", OperationWorkbook(($"ST_AUTO_ buoc mot {tag}", 1000), ($"ST_AUTO_ buoc hai {tag}", 1100)));
        var lines = dlg.Locator("tbody tr.ant-table-row");
        var first = lines.First.GetByRole(AriaRole.Textbox).First;
        await Expect(first).ToHaveValueAsync($"ST_AUTO_ buoc mot {tag}");
        var edited = $"ST_AUTO_ da sua tay {tag}";
        await first.FillAsync(edited);
        await first.BlurAsync();
        await dlg.GetByRole(AriaRole.Button, new() { Name = "Thêm dòng" }).ClickAsync();
        await Expect(lines).ToHaveCountAsync(3);
        var extra = lines.Last;
        var added = $"ST_AUTO_ dong them {tag}";
        var cells = extra.Locator("input:not([type=checkbox])");
        var values = new[] { "203", added, "3", "20", "1K", "1200" };
        for (var n = 0; n < Math.Min(values.Length, await cells.CountAsync()); n++)
            await cells.Nth(n).FillAsync(values[n]);
        await cells.Last.BlurAsync();
        var run = Button(dlg, "Nhập công đoạn");
        await Expect(run).ToBeEnabledAsync(new() { Timeout = 20000 });
        var resp = await acc.RunAndWaitForResponseAsync(async () =>
        {
            await run.ClickAsync();
            var confirm = Button(acc, "Xác nhận").Last;
            try { await confirm.ClickAsync(new() { Timeout = 3000 }); }
            catch (TimeoutException) { }
        }, r => r.Url.Contains("/operations/import") && !r.Url.Contains("preview"), new() { Timeout = 30000 });
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await SelectProductAsync(acc, product);
        foreach (var name in new[] { edited, $"ST_AUTO_ buoc hai {tag}", added })
            await Expect(Row(acc, name)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M3_021()
    {
        CaseId = "ST_M3_021";
        var product = await SharedProductAsync();
        var acc = await AsAsync("Accountant");
        var imports = 0;
        acc.Request += (_, r) => { if (r.Url.Contains("/operations/import") && !r.Url.Contains("preview")) imports++; };
        var big = new byte[50 * 1024 * 1024];
        foreach (var (name, data) in new[]
                 {
                     ("a.csv", System.Text.Encoding.UTF8.GetBytes("STT,BUOC\n1,A\n")),
                     ("a.xls", System.Text.Encoding.UTF8.GetBytes("khong phai excel")),
                     ("hong.xlsx", System.Text.Encoding.UTF8.GetBytes("file hong")),
                     ("to.xlsx", big),
                 })
        {
            var dlg = await OpenOperationImportAsync(acc, product);
            await UploadAsync(acc, dlg, name, data);
            await Assertions.Expect(Errors(acc).First).ToBeVisibleAsync(new() { Timeout = 90000 });
            await Expect(Button(dlg, "Nhập công đoạn")).ToBeDisabledAsync();
        }
        imports.Should().Be(0);
    }

    // ---------------------------------------------------------------- đơn giá

    private static async Task SubmitRateAsync(IPage acc, string product, string code)
    {
        var row = await OperationRowAsync(acc, product, code);
        var inRow = row.GetByRole(AriaRole.Button, new() { Name = "Gửi duyệt" });
        if (await inRow.CountAsync() == 0)
        {
            await row.GetByRole(AriaRole.Checkbox).CheckAsync();
            inRow = acc.GetByRole(AriaRole.Button, new() { Name = "Gửi duyệt" });
        }
        var resp = await acc.RunAndWaitForResponseAsync(async () =>
        {
            await inRow.First.ClickAsync();
            var confirm = Button(acc, "Xác nhận").Last;
            try { await confirm.ClickAsync(new() { Timeout = 3000 }); }
            catch (TimeoutException) { }
        }, r => r.Url.Contains("/piece-rates") && r.Url.Contains("submit"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
    }

    private async Task<(IPage Acc, string Product, string Code)> NewPendingRateAsync(IPage? page = null)
    {
        var (acc, product, code) = await NewOperationAsync(page: page);
        await SubmitRateAsync(acc, product, code);
        return (acc, product, code);
    }

    private static async Task<ILocator> AdminRateRowAsync(IPage admin, string code)
    {
        await admin.GotoAsync("/admin/piece-rates");
        await admin.Locator(".ant-select").Filter(new() { HasText = "/ trang" }).First.ClickAsync();
        await Options(admin).Filter(new() { HasText = "100 / trang" }).ClickAsync();
        return Row(admin, code);
    }

    private static async Task<IResponse> DecideAsync(IPage admin, ILocator row, string button, string? note, string urlPart)
    {
        await Button(row, button).ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Last;
        await modal.GetByRole(AriaRole.Textbox).First.WaitForAsync();
        if (note is not null) await modal.GetByRole(AriaRole.Textbox).First.FillAsync(note);
        return await admin.RunAndWaitForResponseAsync(() => modal.GetByRole(AriaRole.Button).Last.ClickAsync(),
            r => r.Url.Contains(urlPart) && r.Request.Method == "POST");
    }

    [Fact]
    public async Task ST_M3_022()
    {
        CaseId = "ST_M3_022";
        var (acc, product, code) = await NewOperationAsync();
        await SubmitRateAsync(acc, product, code);
        await Expect(Toast(acc, "Đã gửi duyệt.")).ToBeVisibleAsync();
        var admin = await AsAsync("Admin");
        var row = await AdminRateRowAsync(admin, code);
        await Expect(row).ToContainTextAsync("Chờ duyệt");
        (await DecideAsync(admin, row, "Duyệt", null, "/approve")).Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã duyệt đơn giá.")).ToBeVisibleAsync();
        await Expect(await OperationRowAsync(acc, product, code)).Not.ToContainTextAsync("Chờ duyệt");
        await Expect(await AdminRateRowAsync(admin, code)).ToContainTextAsync("Đã duyệt");
        await Button(await OperationRowAsync(acc, product, code), "Xem").ClickAsync();
        await Expect(acc.GetByRole(AriaRole.Dialog).Last).ToContainTextAsync("Đã duyệt");
    }

    [Fact]
    public async Task ST_M3_023()
    {
        CaseId = "ST_M3_023";
        var (acc, product, code) = await NewPendingRateAsync();
        var admin = await AsAsync("Admin");
        var note = "ST_AUTO_ tu choi " + Next();
        (await DecideAsync(admin, await AdminRateRowAsync(admin, code), "Từ chối", note, "/reject")).Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã từ chối đơn giá.")).ToBeVisibleAsync();
        var row = await OperationRowAsync(acc, product, code);
        await SoftAsync("danh sách công đoạn của Kế toán không hiện 'Bị từ chối' sau khi Admin từ chối (hiện 'Chưa có')", () => Expect(row).ToContainTextAsync("Bị từ chối"));
        await Button(row, "Xem").ClickAsync();
        await SoftAsync("chi tiết công đoạn không hiện ghi chú từ chối", () => Expect(acc.GetByRole(AriaRole.Dialog).Last).ToContainTextAsync(note));
        row = await OperationRowAsync(acc, product, code);
        await row.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("đơn giá", RegexOptions.IgnoreCase) }).First.ClickAsync();
        var form = acc.GetByRole(AriaRole.Dialog).Last;
        await form.GetByLabel("Đơn giá (đồng").FillAsync("1700");
        var saved = await SaveAsync(acc, form, "/piece-rates");
        saved.Status.Should().BeInRange(200, 299, await saved.TextAsync());
        await SubmitRateAsync(acc, product, code);
        row = await AdminRateRowAsync(admin, code);
        var pending = row.Filter(new() { HasText = "Chờ duyệt" });
        await Expect(pending).ToHaveCountAsync(1);
        (await DecideAsync(admin, pending, "Duyệt", null, "/approve")).Status.Should().BeInRange(200, 299);
        await Button(await OperationRowAsync(acc, product, code), "Xem").ClickAsync();
        var history = acc.GetByRole(AriaRole.Dialog).Last;
        await Expect(history).ToContainTextAsync("Đã duyệt");
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_024()
    {
        CaseId = "ST_M3_024";
        var codes = new List<string>();
        IPage? acc = null;
        for (var i = 0; i < 3; i++)
        {
            var created = await NewPendingRateAsync(acc);
            acc = created.Acc;
            codes.Add(created.Code);
        }
        var admin = await AsAsync("Admin");
        await AdminRateRowAsync(admin, codes[0]);
        foreach (var code in codes) await Row(admin, code).GetByRole(AriaRole.Checkbox).CheckAsync();
        var bulk = admin.GetByRole(AriaRole.Button, new() { Name = "Duyệt các dòng đã chọn (3)" });
        await bulk.ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Last;
        var resp = await admin.RunAndWaitForResponseAsync(() => modal.GetByRole(AriaRole.Button).Last.ClickAsync(),
            r => r.Url.Contains("/approve-many"));
        resp.Status.Should().BeInRange(200, 299);
        await Expect(Toast(admin, "Đã duyệt 3 đơn giá.")).ToBeVisibleAsync();
        foreach (var code in codes)
        {
            await Expect(Row(admin, code).Filter(new() { HasText = "Chờ duyệt" })).ToHaveCountAsync(0);
            await Expect(Row(admin, code)).ToContainTextAsync("Đã duyệt");
        }
    }

    [Fact]
    public async Task ST_M3_025()
    {
        CaseId = "ST_M3_025";
        var (_, _, code) = await NewPendingRateAsync();
        var tab1 = await AsAsync("Admin");
        var tab2 = await AsAsync("Admin");
        var row1 = await AdminRateRowAsync(tab1, code);
        var row2 = await AdminRateRowAsync(tab2, code);
        await Expect(row2).ToContainTextAsync("Chờ duyệt");
        (await DecideAsync(tab1, row1, "Duyệt", null, "/approve")).Status.Should().BeInRange(200, 299);
        var late = await DecideAsync(tab2, row2, "Từ chối", "ST_AUTO_ tu choi muon", "/reject");
        late.Status.Should().BeInRange(400, 499);
        await Expect(Errors(tab2).First).ToBeVisibleAsync();
        await Expect(await AdminRateRowAsync(tab1, code)).ToContainTextAsync("Đã duyệt");
    }

    [Fact]
    public async Task ST_M3_026()
    {
        CaseId = "ST_M3_026";
        var admin = await AsAsync("Admin");
        await SelectProductAsync(admin, "AK-0522");
        await Expect(Row(admin, "OP01")).ToBeVisibleAsync();
        foreach (var name in new[] { "Thêm công đoạn", "Nhập từ Excel", "Nhóm công đoạn" })
            await SoftAsync($"nút '{name}' bị ẩn hẳn thay vì disabled", () => Expect(admin.GetByRole(AriaRole.Button, new() { Name = name })).ToBeDisabledAsync());
        var resp = await admin.Context.APIRequest.PostAsync($"{BaseUrl}/api/v1/admin/operations", new()
        {
            Headers = await AuthAsync(admin),
            DataObject = new { productId = 1, operationCode = "ST_AUTO_ADMIN", operationName = "ST_AUTO_", sequence = 999, rate = 1 },
        });
        resp.Status.Should().BeInRange(400, 499);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M3_027()
    {
        CaseId = "ST_M3_027";
        var admin = await AsAsync("Admin");
        var code = await CreateProductAsync(admin);
        var row = await ProductRowAsync(admin, code);
        await Button(row, "Ngừng sản xuất").ClickAsync();
        var resp = await admin.RunAndWaitForResponseAsync(() => ConfirmAsync(admin), r => r.Url.Contains("/discontinue"));
        resp.Status.Should().BeInRange(200, 299);
        await Expect(await ProductRowAsync(admin, code)).ToContainTextAsync("Ngừng sản xuất");
        await admin.GotoAsync("/admin/orders/new");
        var picker = admin.Locator(".ant-select").Filter(new() { HasText = "Chọn mã hàng" }).First;
        await picker.ClickAsync();
        await picker.GetByRole(AriaRole.Combobox).FillAsync(code);
        await Expect(admin.Locator(".ant-select-dropdown:visible")).ToBeVisibleAsync();
        await Expect(Options(admin).Filter(new() { HasText = code })).ToHaveCountAsync(0);
        await admin.GotoAsync("/admin/orders");
        await Button(Row(admin, "DH-2511-005"), "Xem").ClickAsync();
        await Expect(admin.GetByText("AK-0522").Or(admin.GetByText("AP-0715")).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ST_M3_028()
    {
        CaseId = "ST_M3_028";
        var worker = await AsAsync("Worker");
        await GotoAndWaitAsync(worker, "/");
        var forbidden = new List<string>();
        worker.Request += (_, r) => { if (Regex.IsMatch(r.Url, "/api/v1/(admin|accountant)/(customers|operations)")) forbidden.Add(r.Url); };
        foreach (var path in new[] { "/admin/customers", "/operations" })
        {
            await worker.GotoAsync(path);
            await worker.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        }
        forbidden.Should().BeEmpty();
        var (user, pass) = GetAccount("Accountant");
        var acc = await NewAnonymousPageAsync();
        await LoginUiAsync(acc, user, pass);
        InvalidateRole("Accountant");
        var rates = 0;
        acc.Request += (_, r) => { if (r.Url.Contains("/api/v1/admin/piece-rates")) rates++; };
        await acc.GotoAsync("/admin/piece-rates");
        await acc.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        rates.Should().Be(0);
        await acc.Locator("button.app-user").ClickAsync();
        await acc.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync();
        await acc.WaitForURLAsync(u => u.Contains("/login"));
        await acc.GoBackAsync();
        await acc.WaitForURLAsync(u => u.Contains("/login"));
        await Expect(acc.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
    }
}
