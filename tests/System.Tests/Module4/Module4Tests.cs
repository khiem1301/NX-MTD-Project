using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using static System.Tests.UiKit;

namespace System.Tests.Module4;

[TestCaseOrderer("System.Tests.AlphabeticalOrderer", "System.Tests")]
public class Module4Tests : BaseSystemTest, IClassFixture<CleanupFixture>
{
    private const string Customer = "Công ty TNHH Xuất nhập khẩu Bình Minh";
    private const string Product = "AK-0522";
    private const string StartOrder = "DH-2511-008";   // đơn seed trạng thái Mới, đủ NVL
    private const string RunningOrder = "DH-2511-005"; // đơn seed đang sản xuất
    private const string StopOrder = "DH-2510-003";    // đơn seed đang sản xuất, gần giao xong
    private const string FixLines = "Vui lòng sửa các ô báo đỏ ở phần mã hàng.";

    private static readonly Regex DetailUrl = new(@"/admin/orders/(\d+)(\?[^/]*)?$");

    private async Task<IPage> AsAsync(string role)
    {
        var page = await NewPageAsRoleAsync(role);
        page.SetDefaultTimeout(15000);
        return page;
    }

    private static async Task<Dictionary<string, string>> AuthAsync(IPage page) => new()
    {
        ["Authorization"] = "Bearer " + await page.EvaluateAsync<string>("localStorage.getItem('nxmtd.accessToken')"),
    };

    private static async Task<JsonElement> GetDataAsync(IPage page, string path)
    {
        var r = await page.Context.APIRequest.GetAsync(BaseUrl + path, new() { Headers = await AuthAsync(page) });
        return JsonDocument.Parse(await r.TextAsync()).RootElement.GetProperty("data").Clone();
    }

    private static string Date(int days) => DateTime.Now.AddDays(days).ToString("dd/MM/yyyy");

    private static ILocator Main(IPage page) => page.GetByRole(AriaRole.Main);
    private static ILocator SaveButton(IPage page) => Main(page).GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(loading )?Lưu$") }).Last;
    private static ILocator CodeInput(IPage page) => page.GetByLabel("Mã đơn hàng");
    private static ILocator Address(IPage page) => page.GetByLabel("Địa chỉ giao hàng");
    private static ILocator Note(IPage page) => page.GetByLabel("Ghi chú", new() { Exact = true });
    private static ILocator Price(IPage page) => page.GetByRole(AriaRole.Spinbutton, new() { Name = "VD: 15.000" }).First;
    private static ILocator Lines(IPage page) => Main(page).Locator("tbody tr.ant-table-row");
    private static ILocator Qty(IPage page, int line, int size) => Lines(page).Nth(line).GetByRole(AriaRole.Spinbutton).Nth(size);
    private static ILocator Color(IPage page, int line) => Lines(page).Nth(line).GetByRole(AriaRole.Combobox);

    private static async Task OpenNewOrderAsync(IPage page)
    {
        await page.GotoAsync("/admin/orders/new");
        await Assertions.Expect(CodeInput(page)).Not.ToHaveValueAsync("");
    }

    private static async Task PickCustomerAsync(IPage page, string name) =>
        await SelectAsync(page, page.GetByRole(AriaRole.Combobox, new() { Name = "Khách hàng" }), name);

    private static async Task FillHeaderAsync(IPage page, string? code = null)
    {
        if (code is not null) await CodeInput(page).FillAsync(code);
        await PickCustomerAsync(page, Customer);
        await DateAsync(page.GetByLabel("Ngày giao"), Date(30));
        await Address(page).FillAsync("ST_AUTO_ 56 Le Van Viet, TP.HCM");
        await Note(page).FillAsync("ST_AUTO_ don kiem thu");
    }

    private static async Task PickProductAsync(IPage page, string product = Product)
    {
        var picker = page.Locator(".ant-select").Filter(new() { HasText = "Chọn mã hàng" }).First;
        await picker.ClickAsync();
        await picker.GetByRole(AriaRole.Combobox).FillAsync(product);
        await Options(page).Filter(new() { HasText = product }).First.ClickAsync();
    }

    private static async Task SetColorAsync(IPage page, int line, string color)
    {
        await Color(page, line).FillAsync(color);
        await Color(page, line).PressAsync("Tab");
    }

    private static async Task FillBlockAsync(IPage page, string price = "15000", string quantity = "10")
    {
        await PickProductAsync(page);
        await Price(page).FillAsync(price);
        await SetColorAsync(page, 0, "Đen");
        await Qty(page, 0, 0).FillAsync(quantity);
    }

    private static async Task<IResponse> SubmitAsync(IPage page) =>
        await page.RunAndWaitForResponseAsync(() => SaveButton(page).ClickAsync(),
            r => Regex.IsMatch(r.Url, @"/admin/customer-orders(/\d+)?$") && r.Request.Method is "POST" or "PUT");

    private sealed record Order(int Id, string Code);

    private static async Task<Order> CreateOrderAsync(IPage page)
    {
        var code = "ST_AUTO_DH" + Next();
        await OpenNewOrderAsync(page);
        await FillHeaderAsync(page, code);
        await FillBlockAsync(page);
        var resp = await SubmitAsync(page);
        resp.Status.Should().BeInRange(200, 299, "tạo đơn mồi: " + await resp.TextAsync());
        await page.WaitForURLAsync(DetailUrl);
        return new Order(int.Parse(DetailUrl.Match(page.Url).Groups[1].Value), code);
    }

    private static async Task<ILocator> OrderRowAsync(IPage page, string code, string path = "/admin/orders")
    {
        await page.GotoAsync(path);
        var search = page.GetByPlaceholder("Tìm theo mã, tên...");
        await search.FillAsync(code);
        await search.PressAsync("Enter");
        return page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = code, Exact = true }) });
    }

    private static async Task<int> OpenOrderAsync(IPage page, string code, string path = "/admin/orders")
    {
        await Button(await OrderRowAsync(page, code, path), "Xem").ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/orders/\d+$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Tab).First).ToBeVisibleAsync();
        return int.Parse(page.Url.Split('/').Last());
    }

    [Fact]
    public async Task ST_M4_001()
    {
        CaseId = "ST_M4_001";
        var admin = await AsAsync("Admin");
        var next = await admin.RunAndWaitForResponseAsync(() => admin.GotoAsync("/admin/orders/new"), r => r.Url.Contains("/next-code"));
        var suggested = Regex.Match(await next.TextAsync(), "\"data\":\"([^\"]+)\"").Groups[1].Value;
        await Expect(CodeInput(admin)).ToHaveValueAsync(suggested);
        await FillHeaderAsync(admin);
        await FillBlockAsync(admin);
        await Qty(admin, 0, 1).FillAsync("20");
        await Qty(admin, 0, 2).FillAsync("30");
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm màu" }).First.ClickAsync();
        await SetColorAsync(admin, 1, "Trắng");
        for (var i = 0; i < 3; i++) await Qty(admin, 1, i).FillAsync("5");
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        var body = resp.Request.PostData!;
        await SoftAsync("payload POST /admin/customer-orders không theo cấu trúc blocks[].lines[] (thực tế gửi lines[] ở cấp gốc)", () =>
        {
            body.Should().Contain("\"blocks\"").And.Contain("\"lines\"");
            return Task.CompletedTask;
        });
        foreach (var text in new[] { "Đen", "Trắng" })
            (body.Contains(text) || body.Contains(JsonSerializer.Serialize(text).Trim('"'))).Should().BeTrue($"payload thiếu màu {text}");
        await admin.WaitForURLAsync(DetailUrl);
        for (var i = 0; i < 2; i++)
        {
            await Expect(Main(admin)).ToContainTextAsync("1.125.000");
            await Expect(Main(admin)).ToContainTextAsync("75");
            await Expect(Main(admin)).ToContainTextAsync(suggested);
            await admin.ReloadAsync();
        }
        await Expect(await OrderRowAsync(admin, suggested)).ToContainTextAsync("Mới");
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_002()
    {
        CaseId = "ST_M4_002";
        var admin = await AsAsync("Admin");
        await OpenNewOrderAsync(admin);
        await admin.GetByRole(AriaRole.Combobox, new() { Name = "Khách hàng" }).ClickAsync();
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm khách" }).ClickAsync();
        var dlg = Dialog(admin, "Thêm khách");
        var name = "ST_AUTO_ KH " + Next();
        await dlg.GetByLabel("Tên khách hàng").FillAsync(name);
        await dlg.GetByLabel("Số điện thoại").FillAsync("09" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("00000000"));
        await dlg.GetByLabel("Email").FillAsync($"st_auto{Next()}@example.com");
        await dlg.GetByLabel("Địa chỉ mặc định").FillAsync("ST_AUTO_ dia chi");
        await Button(dlg, "Lưu").ClickAsync();
        await Expect(dlg).ToHaveCountAsync(0);
        await Expect(Main(admin)).ToContainTextAsync(name);
        var code = "ST_AUTO_DH" + Next();
        await CodeInput(admin).FillAsync(code);
        await DateAsync(admin.GetByLabel("Ngày giao"), Date(30));
        await Address(admin).FillAsync("ST_AUTO_ dia chi giao");
        await Note(admin).FillAsync("ST_AUTO_ don kiem thu");
        await FillBlockAsync(admin);
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(await OrderRowAsync(admin, code)).ToContainTextAsync(name);
        await admin.GotoAsync("/admin/customers");
        await admin.GetByPlaceholder("Tìm theo mã, tên...").FillAsync(name);
        await admin.GetByPlaceholder("Tìm theo mã, tên...").PressAsync("Enter");
        await Expect(Row(admin, name)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M4_003()
    {
        CaseId = "ST_M4_003";
        var admin = await AsAsync("Admin");
        var existing = await CreateOrderAsync(admin);
        await OpenNewOrderAsync(admin);
        await FillHeaderAsync(admin, existing.Code);
        await FillBlockAsync(admin, quantity: "37");
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(400, 499);
        await Expect(Main(admin).Locator(".ant-form-item").Filter(new() { Has = CodeInput(admin) })
            .Locator(".ant-form-item-explain-error")).ToBeVisibleAsync();
        await Expect(CodeInput(admin)).ToHaveValueAsync(existing.Code);
        await Expect(Qty(admin, 0, 0)).ToHaveValueAsync("37");
        await Expect(await OrderRowAsync(admin, existing.Code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M4_004()
    {
        CaseId = "ST_M4_004";
        var admin = await AsAsync("Admin");
        await OpenNewOrderAsync(admin);
        var thirty = ("ST_AUTO_" + Next() + new string('A', 30))[..30];
        await CodeInput(admin).FillAsync(thirty);
        await CodeInput(admin).BlurAsync();
        var error = Main(admin).Locator(".ant-form-item").Filter(new() { Has = CodeInput(admin) }).Locator(".ant-form-item-explain-error");
        await Expect(error).ToHaveCountAsync(0);
        await CodeInput(admin).FillAsync(thirty + "A");
        await CodeInput(admin).BlurAsync();
        await SoftAsync("31 ký tự không báo 'Mã đơn hàng tối đa 30 ký tự.'", () => Expect(admin.GetByText("Mã đơn hàng tối đa 30 ký tự.")).ToBeVisibleAsync());
        await CodeInput(admin).FillAsync("ĐH01");
        await CodeInput(admin).BlurAsync();
        await SoftAsync("'ĐH01' không báo lỗi ký tự", () =>
            Expect(admin.GetByText("Mã đơn hàng chỉ gồm chữ không dấu, số và . _ -")).ToBeVisibleAsync());
        var code = "ST_AUTO_DH" + Next();
        await FillHeaderAsync(admin, $"  {code}  ");
        await FillBlockAsync(admin);
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await SoftAsync("mã đơn gửi đi chưa được trim", () =>
        {
            resp.Request.PostData.Should().Contain($"\"orderCode\":\"{code}\"");
            return Task.CompletedTask;
        });
        await Expect(await OrderRowAsync(admin, code)).ToHaveCountAsync(1);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_005()
    {
        CaseId = "ST_M4_005";
        var admin = await AsAsync("Admin");
        await OpenNewOrderAsync(admin);
        var received = admin.GetByLabel("Ngày nhận");
        var due = admin.GetByLabel("Ngày giao");
        const string tooEarly = "Ngày giao phải sau hoặc bằng ngày nhận.";
        await DateAsync(due, Date(0));
        await Expect(admin.GetByText(tooEarly)).ToHaveCountAsync(0);
        await DateAsync(due, Date(-1));
        await SaveButton(admin).ClickAsync();
        await SoftAsync("ngày giao trước ngày nhận không báo lỗi", () => Expect(admin.GetByText(tooEarly)).ToBeVisibleAsync());
        var code = "ST_AUTO_DH" + Next();
        await FillHeaderAsync(admin, code);
        await DateAsync(received, "10/01/2020");
        await DateAsync(due, "20/01/2020");
        await FillBlockAsync(admin);
        var resp = await SubmitAsync(admin);
        if (resp.Status < 300)
        {
            await admin.WaitForURLAsync(DetailUrl);
            await admin.ReloadAsync();
            await Expect(Main(admin)).ToContainTextAsync("10/01/2020");
            await Expect(Main(admin)).ToContainTextAsync("20/01/2020");
        }
        else
        {
            resp.Status.Should().BeLessThan(500);
            await Expect(Errors(admin).First).ToBeVisibleAsync();
        }
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_006()
    {
        CaseId = "ST_M4_006";
        var admin = await AsAsync("Admin");
        await OpenNewOrderAsync(admin);
        var address = ("ST_AUTO_" + new string('a', 500))[..500];
        var note = ("ST_AUTO_" + new string('n', 1000))[..1000];
        foreach (var (input, value, max, label) in new[] { (Address(admin), address, 500, "Địa chỉ"), (Note(admin), note, 1000, "Ghi chú") })
        {
            await input.FillAsync(value + "Z");
            await input.BlurAsync();
            if ((await input.InputValueAsync()).Length > max)
                await SoftAsync($"{label} {max + 1} ký tự không bị chặn và không báo lỗi", () => Expect(
                    Main(admin).Locator(".ant-form-item").Filter(new() { Has = input }).Locator(".ant-form-item-explain-error")).ToBeVisibleAsync());
        }
        var code = "ST_AUTO_DH" + Next();
        await FillHeaderAsync(admin, code);
        await FillBlockAsync(admin);
        await Address(admin).FillAsync("");
        await SaveButton(admin).ClickAsync();
        await Expect(Main(admin).Locator(".ant-form-item").Filter(new() { Has = Address(admin) }).Locator(".ant-form-item-explain-error")).ToBeVisibleAsync();
        await Address(admin).FillAsync(address);
        await Note(admin).FillAsync(note);
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await admin.WaitForURLAsync(DetailUrl);
        await admin.ReloadAsync();
        await Expect(Main(admin)).ToContainTextAsync(address);
        await Main(admin).GetByText("Mở rộng").ClickAsync();
        await Expect(Main(admin)).ToContainTextAsync(note);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_007()
    {
        CaseId = "ST_M4_007";
        var admin = await AsAsync("Admin");
        var posts = 0;
        admin.Request += (_, r) => { if (r.Url.EndsWith("/admin/customer-orders") && r.Method == "POST") posts++; };
        await OpenNewOrderAsync(admin);
        await FillHeaderAsync(admin, "ST_AUTO_DH" + Next());
        await Price(admin).FillAsync("15000");
        await SetColorAsync(admin, 0, "Đen");
        await Qty(admin, 0, 0).FillAsync("10");
        await SaveButton(admin).ClickAsync();
        await SoftAsync("không chọn mã hàng không báo 'Vui lòng chọn mã hàng.'", () => Expect(admin.GetByText("Vui lòng chọn mã hàng.").First).ToBeVisibleAsync());
        await PickProductAsync(admin);

        var priceError = new Regex("Đơn giá CMT (không được âm|tối đa)|nhập đơn giá CMT");
        foreach (var good in new[] { "0", "100000000" })
        {
            await Price(admin).FillAsync(good);
            await Price(admin).BlurAsync();
            await SoftAsync($"đơn giá {good} bị báo lỗi", () => Expect(admin.GetByText(priceError)).ToHaveCountAsync(0));
        }
        async Task BadAsync(string what, Func<Task> enter)
        {
            await enter();
            await Qty(admin, 0, 0).BlurAsync();
            var before = posts;
            string? sent = null;
            // Chặn mọi request tạo đơn để giá trị sai không tạo đơn thật
            await admin.RouteAsync(new Regex(@"/admin/customer-orders$"), r => r.Request.Method == "POST"
                ? Capture(r) : r.ContinueAsync());
            Task Capture(IRoute route) { sent = route.Request.PostData; return route.FulfillAsync(new() { Status = 418, ContentType = "application/json", Body = "{}" }); }
            await SaveButton(admin).ClickAsync();
            await SoftAsync(what, async () =>
            {
                await Expect(Main(admin).Locator(".ant-form-item-has-error, .ant-input-number-status-error, .ant-input-status-error, .ant-select-status-error, .ant-form-item-explain-error").First).ToBeVisibleAsync(new() { Timeout = 3000 });
                posts.Should().Be(before);
            });
            await admin.UnrouteAllAsync();
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "m4_007_payload.txt"),
                what + " => " + (sent is null ? "khong gui API" : string.Join(" ", Regex.Matches(sent, "(cmtUnitPrice|orderedQty).:[0-9.]+").Select(m => m.Value))) + Environment.NewLine);
        }
        foreach (var bad in new[] { "-1", "100000000,01", "1500,555" })
        {
            await BadAsync($"đơn giá nhập '{bad}' không bị chặn", () => Price(admin).FillAsync(bad));
            await Price(admin).FillAsync("15000");
        }
        await Qty(admin, 0, 0).FillAsync("1000000");
        await Qty(admin, 0, 0).BlurAsync();
        await SoftAsync("SL 1.000.000 bị báo lỗi", () => Expect(admin.GetByText("Số lượng mỗi ô tối đa")).ToHaveCountAsync(0));
        foreach (var bad in new[] { "1000001", "1,5" })
        {
            await BadAsync($"SL nhập '{bad}' không bị chặn", () => Qty(admin, 0, 0).FillAsync(bad));
            await Qty(admin, 0, 0).FillAsync("10");
        }
        await admin.GetByRole(AriaRole.Button, new() { Name = "Thêm màu" }).First.ClickAsync();
        await BadAsync("2 màu trùng tên không bị chặn", async () =>
        {
            await SetColorAsync(admin, 1, "Đen");
            await Qty(admin, 1, 0).FillAsync("5");
        });
        await Button(Lines(admin).Nth(1), "Xóa").ClickAsync();
        await BadAsync("block không có số lượng không bị chặn", () => Qty(admin, 0, 0).FillAsync(""));
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_008()
    {
        CaseId = "ST_M4_008";
        var tab1 = await AsAsync("Admin");
        var order = await CreateOrderAsync(tab1);
        var tab2 = await AsAsync("Admin");
        foreach (var tab in new[] { tab1, tab2 })
        {
            await tab.GotoAsync($"/admin/orders/{order.Id}/edit");
            await Expect(Qty(tab, 0, 0)).ToHaveValueAsync("10");
        }
        await Qty(tab1, 0, 0).FillAsync("44");
        (await SubmitAsync(tab1)).Status.Should().BeInRange(200, 299);
        await Note(tab2).FillAsync("ST_AUTO_ ghi chu tab 2");
        var second = await SubmitAsync(tab2);
        second.Status.Should().BeInRange(400, 499);
        await Expect(Errors(tab2).First).ToBeVisibleAsync();
        await Expect(CodeInput(tab2)).ToBeVisibleAsync();
        await tab1.GotoAsync($"/admin/orders/{order.Id}/edit");
        await Expect(Qty(tab1, 0, 0)).ToHaveValueAsync("44");
        await Expect(Note(tab1)).ToHaveValueAsync("ST_AUTO_ don kiem thu");
    }

    [Fact]
    public async Task ST_M4_009()
    {
        CaseId = "ST_M4_009";
        var admin = await AsAsync("Admin");
        var source = await CreateOrderAsync(admin);
        await Button(Main(admin), "Sao chép đơn").ClickAsync();
        await admin.WaitForURLAsync(u => u.Contains($"copyFrom={source.Id}"));
        await Expect(Qty(admin, 0, 0)).ToHaveValueAsync("10");
        await Expect(Color(admin, 0)).ToHaveValueAsync("Đen");
        await Expect(CodeInput(admin)).Not.ToHaveValueAsync(source.Code);
        var code = "ST_AUTO_DH" + Next();
        await CodeInput(admin).FillAsync(code);
        await DateAsync(admin.GetByLabel("Ngày giao"), Date(40));
        await Note(admin).FillAsync("ST_AUTO_ don sao chep");
        await Qty(admin, 0, 0).FillAsync("22");
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        resp.Request.Method.Should().Be("POST");
        await admin.GotoAsync($"/admin/orders/{source.Id}/edit");
        await Expect(Qty(admin, 0, 0)).ToHaveValueAsync("10");
        await admin.GotoAsync("/admin/orders/new?copyFrom=999999");
        await Expect(CodeInput(admin).Or(Errors(admin).First).First).ToBeVisibleAsync();
        await Expect(Main(admin)).Not.ToHaveTextAsync("");
    }

    [Fact]
    public async Task ST_M4_010()
    {
        CaseId = "ST_M4_010";
        var admin = await AsAsync("Admin");
        await CreateOrderAsync(admin);
        await Button(Main(admin), "Hủy đơn hàng").ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Last;
        var resp = await admin.RunAndWaitForResponseAsync(() => modal.GetByRole(AriaRole.Button).Last.ClickAsync(),
            r => r.Url.EndsWith("/cancel"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        for (var i = 0; i < 2; i++)
        {
            await Expect(Main(admin)).ToContainTextAsync("Đã hủy");
            await Expect(Button(Main(admin), "Sửa")).ToHaveCountAsync(0);
            await Expect(Button(Main(admin), "Hủy đơn hàng")).ToHaveCountAsync(0);
            await admin.ReloadAsync();
        }
    }

    [Fact]
    public async Task ST_M4_011()
    {
        CaseId = "ST_M4_011";
        var admin = await AsAsync("Admin");
        await OpenOrderAsync(admin, StopOrder);
        var stops = 0;
        admin.Request += (_, r) => { if (r.Url.EndsWith("/stop") && r.Method == "POST") stops++; };
        await Button(Main(admin), "Dừng đơn hàng").ClickAsync();
        var modal = admin.GetByRole(AriaRole.Dialog).Last;
        var confirm = modal.GetByRole(AriaRole.Button).Last;
        if (await confirm.IsEnabledAsync()) await confirm.ClickAsync();
        await Expect(modal.GetByRole(AriaRole.Textbox)).ToBeVisibleAsync();
        stops.Should().Be(0);
        var reason = "ST_AUTO_ ly do dung " + Next();
        await modal.GetByRole(AriaRole.Textbox).FillAsync(reason);
        var resp = await admin.RunAndWaitForResponseAsync(() => confirm.ClickAsync(), r => r.Url.EndsWith("/stop"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(Main(admin)).ToContainTextAsync("Dừng");
        await admin.ReloadAsync();
        await Expect(Main(admin)).ToContainTextAsync(reason);
    }

    [Fact]
    public async Task ST_M4_012()
    {
        CaseId = "ST_M4_012";
        var admin = await AsAsync("Admin");
        await CreateOrderAsync(admin);
        var start = Button(Main(admin), "Chuyển sang sản xuất");
        await Expect(start).ToBeDisabledAsync();
        var hint = "Chưa nhận NVL nào nên chưa thể chuyển sang sản xuất.";
        await SoftAsync("nút bị disable nhưng không thấy hint 'Chưa nhận NVL nào…'", async () =>
        {
            await start.HoverAsync(new() { Force = true });
            await Expect(admin.GetByText(hint).First).ToBeVisibleAsync();
        });
        await OpenOrderAsync(admin, StartOrder);
        await Expect(start).ToBeEnabledAsync();
        await start.ClickAsync();
        var resp = await admin.RunAndWaitForResponseAsync(async () =>
        {
            await admin.Locator(".ant-popconfirm-buttons, .ant-modal-confirm-btns").Last.GetByRole(AriaRole.Button).Last.ClickAsync();
        }, r => r.Url.EndsWith("/start-production"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(Toast(admin, "Đã chuyển đơn hàng sang sản xuất.")).ToBeVisibleAsync();
        await admin.ReloadAsync();
        await Expect(Main(admin)).ToContainTextAsync("Đang sản xuất");
        await admin.GotoAsync("/order-line-teams");
        await admin.GetByPlaceholder("Tìm đơn, mã hàng, màu, size").FillAsync(StartOrder);
        await admin.GetByPlaceholder("Tìm đơn, mã hàng, màu, size").PressAsync("Enter");
        await Expect(Row(admin, StartOrder).First).ToBeVisibleAsync();
        SoftVerify();
    }

    [Fact]
    public async Task ST_M4_013()
    {
        CaseId = "ST_M4_013";
        var admin = await AsAsync("Admin");
        var id = await OpenOrderAsync(admin, RunningOrder);
        var before = (await GetDataAsync(admin, $"/api/v1/admin/customer-orders/{id}")).GetRawText();
        await admin.GotoAsync($"/admin/orders/{id}/edit");
        var cell = Qty(admin, 0, 0);
        await Expect(cell).Not.ToHaveValueAsync("");
        await cell.FillAsync("");
        await SaveButton(admin).ClickAsync();
        await SoftAsync("xóa trống số lượng đã khai không báo 'Đơn đã vào sản xuất: không được để trống…'", () =>
            Expect(admin.GetByText("không được để trống số lượng đã khai").First).ToBeVisibleAsync());
        await admin.GotoAsync($"/admin/orders/{id}/edit");
        await Expect(cell).Not.ToHaveValueAsync("");
        await cell.FillAsync("1");
        var resp = await SubmitAsync(admin);
        await SoftAsync($"giảm SL xuống dưới số đã làm: server trả {resp.Status} thay vì từ chối", async () =>
        {
            resp.Status.Should().BeInRange(400, 499);
            await Expect(Errors(admin).First).ToBeVisibleAsync();
        });
        var after = (await GetDataAsync(admin, $"/api/v1/admin/customer-orders/{id}")).GetRawText();
        await SoftAsync("dữ liệu đơn bị thay đổi", () => { after.Should().Be(before); return Task.CompletedTask; });
        SoftVerify();
    }

    private static async Task<ILocator> OpenImportAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Nhập từ Excel" }).Last.ClickAsync();
        return Dialog(page, "Nhập dòng hàng từ Excel");
    }

    private static async Task UploadTemplateAsync(IPage page, ILocator dlg) =>
        await dlg.Locator("input[type=file]").SetInputFilesAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "order_template.xlsx"));

    [Fact]
    public async Task ST_M4_014()
    {
        CaseId = "ST_M4_014";
        var admin = await AsAsync("Admin");
        var code = "ST_AUTO_DH" + Next();
        await OpenNewOrderAsync(admin);
        await FillHeaderAsync(admin, code);
        var dlg = await OpenImportAsync(admin);
        await UploadTemplateAsync(admin, dlg);
        var review = Dialog(admin, "Kiểm tra dòng hàng từ Excel");
        await Expect(review).ToContainTextAsync("3 dòng hợp lệ, 0 dòng lỗi");
        var first = review.Locator("tbody tr.ant-table-row").First.GetByRole(AriaRole.Spinbutton).Nth(1);
        await first.FillAsync("81");
        await first.BlurAsync();
        await Button(review, "Áp dụng vào form").ClickAsync();
        await Expect(review).ToHaveCountAsync(0);
        await Expect(admin.Locator(".ant-message-loading")).ToHaveCountAsync(0);
        var resp = await SubmitAsync(admin);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(admin.Locator(".ant-message-success, .ant-notification-notice-success").First).ToBeVisibleAsync();
        await Expect(admin.Locator(".ant-message-loading")).ToHaveCountAsync(0);
        var body = JsonDocument.Parse(resp.Request.PostData!).RootElement;
        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().HaveCount(4 + 2 + 3, "mỗi ô size có số lượng là một dòng đơn");
        lines.Select(l => l.GetRawText()).Should().Contain(t => t.Contains("81"));
        await Expect(await OrderRowAsync(admin, code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M4_015()
    {
        CaseId = "ST_M4_015";
        var admin = await AsAsync("Admin");
        var creates = 0;
        admin.Request += (_, r) => { if (r.Url.EndsWith("/admin/customer-orders") && r.Method == "POST") creates++; };
        await OpenNewOrderAsync(admin);
        await admin.RouteAsync(new Regex(@"/customer-orders/import"), r => r.Request.Method == "POST"
            ? r.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"success\":false,\"message\":\"Loi gia lap\"}" })
            : r.ContinueAsync());
        var dlg = await OpenImportAsync(admin);
        await UploadTemplateAsync(admin, dlg);
        await Expect(Errors(admin).First).ToBeVisibleAsync();
        await Expect(admin.Locator(".ant-message-loading")).ToHaveCountAsync(0);
        await Expect(admin.GetByRole(AriaRole.Dialog).Locator(".ant-spin-spinning, .ant-btn-loading")).ToHaveCountAsync(0);
        creates.Should().Be(0);
    }

    [Fact]
    public async Task ST_M4_016()
    {
        CaseId = "ST_M4_016";
        var manager = await AsAsync("Manager");
        var id = await OpenOrderAsync(manager, RunningOrder, "/manager/orders");
        foreach (var name in new[] { "Sửa", "Hủy đơn hàng", "Dừng đơn hàng", "Chuyển sang sản xuất" })
            await Expect(Button(Main(manager), name)).ToHaveCountAsync(0);
        var headers = await AuthAsync(manager);
        var put = await manager.Context.APIRequest.PutAsync($"{BaseUrl}/api/v1/admin/customer-orders/{id}",
            new() { Headers = headers, DataObject = new { orderCode = "ST_AUTO_HACK" } });
        put.Status.Should().Be(403);
        foreach (var path in new[] { $"/admin/orders/{id}/edit", "/admin/orders/new" })
        {
            await manager.GotoAsync(path);
            await manager.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
        }
    }

    [Fact]
    public async Task ST_M4_017()
    {
        CaseId = "ST_M4_017";
        var manager = await AsAsync("Manager");
        await OpenOrderAsync(manager, "DH-2511-007", "/manager/orders");
        await manager.GetByRole(AriaRole.Tab, new() { Name = "Bìa cắt" }).ClickAsync();
        var panel = manager.GetByRole(AriaRole.Tabpanel);
        var rows = panel.Locator("tbody tr.ant-table-row");
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Thêm bìa" }).First).ToBeVisibleAsync();
        var before = await rows.CountAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Thêm bìa" }).First.ClickAsync();
        await Expect(rows).ToHaveCountAsync(before + 1);
        var row = rows.Last;
        var code = "ST_AUTO_B" + Next();
        var inputs = row.Locator("input:not([type=checkbox])");
        var total = await inputs.CountAsync();
        var values = new[] { "1", code, "3", "1,5" }.Concat(Enumerable.Repeat("1", Math.Max(0, total - 4))).ToArray();
        for (var i = 0; i < total; i++)
            if (await inputs.Nth(i).IsEditableAsync()) await inputs.Nth(i).FillAsync(values[i]);
        await inputs.Last.BlurAsync();
        var resp = await manager.RunAndWaitForResponseAsync(() => Button(panel, "Lưu").First.ClickAsync(),
            r => r.Url.Contains("/manager/order-markers/") && r.Request.Method == "PUT");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(manager.Locator(".ant-message-success").First).ToBeVisibleAsync();
        await manager.ReloadAsync();
        await manager.GetByRole(AriaRole.Tab, new() { Name = "Bìa cắt" }).ClickAsync();
        await Expect(panel.Locator($"input[value='{code}']").Or(panel.GetByText(code)).First).ToBeVisibleAsync();
    }

    private static async Task<ILocator> LineTeamsAsync(IPage page, string order)
    {
        await page.GotoAsync("/order-line-teams");
        var search = page.GetByPlaceholder("Tìm đơn, mã hàng, màu, size");
        await search.FillAsync(order);
        await search.PressAsync("Enter");
        var rows = page.GetByRole(AriaRole.Row).Filter(new() { HasText = order });
        await Assertions.Expect(rows.First).ToBeVisibleAsync();
        return rows;
    }

    private static async Task<IResponse> PickTeamAsync(IPage page, ILocator row, string team)
    {
        var box = row.GetByRole(AriaRole.Combobox);
        return await page.RunAndWaitForResponseAsync(async () =>
        {
            await box.ClickAsync();
            await Options(page).Filter(new() { HasTextRegex = new Regex($"^{team}\\b") }).First.ClickAsync();
            await page.Keyboard.PressAsync("Escape");
            await page.GetByPlaceholder("Tìm đơn, mã hàng, màu, size").ClickAsync();
        }, r => r.Url.Contains("/manager/order-line-teams") && r.Request.Method == "PUT");
    }

    private static async Task<List<string>> AssignableOrdersAsync(IPage page)
    {
        await page.GotoAsync("/operation-assignments");
        await page.Locator("main .ant-select").First.ClickAsync();
        await Assertions.Expect(Options(page).First).ToBeVisibleAsync();
        return (await Options(page).AllInnerTextsAsync()).ToList();
    }

    [Fact]
    public async Task ST_M4_018()
    {
        CaseId = "ST_M4_018";
        var manager = await AsAsync("Manager");
        var rows = await LineTeamsAsync(manager, StartOrder);
        var row = rows.First;
        var resp = await PickTeamAsync(manager, row, "MAY1");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(Toast(manager, "Đã lưu việc chia đơn.").First).ToBeVisibleAsync();
        rows = await LineTeamsAsync(manager, StartOrder);
        await Expect(rows.First).ToContainTextAsync("MAY1");
        var may1 = await AsAsync("TeamDeputy");
        (await AssignableOrdersAsync(may1)).Should().Contain(o => o.Contains(StartOrder), "tổ MAY1 phải thấy đơn vừa chia");
        var may2 = await AsAsync("TeamLeader");
        (await AssignableOrdersAsync(may2)).Should().NotContain(o => o.Contains(StartOrder), "tổ khác không được thấy đơn");
    }

    [Fact]
    public async Task ST_M4_019()
    {
        CaseId = "ST_M4_019";
        var team = await CreateTeamAsync(await AsAsync("Admin"));
        var manager = await AsAsync("Manager");
        await LineTeamsAsync(manager, StartOrder);
        await manager.GetByRole(AriaRole.Checkbox, new() { Name = "Chỉ dòng chưa chia" }).CheckAsync();
        var rows = manager.GetByRole(AriaRole.Row).Filter(new() { HasText = StartOrder });
        await Expect(rows.Nth(1)).ToBeVisibleAsync();
        var codes = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            codes.Add((await rows.Nth(i).GetByRole(AriaRole.Combobox).GetAttributeAsync("aria-label"))!);
            await rows.Nth(i).GetByRole(AriaRole.Checkbox).CheckAsync();
        }
        var bulk = manager.GetByRole(AriaRole.Combobox, new() { Name = "Chọn tổ may" });
        await bulk.ClickAsync();
        await bulk.FillAsync(team);
        await Options(manager).Filter(new() { HasText = team }).First.ClickAsync();
        await manager.Keyboard.PressAsync("Escape");
        var apply = manager.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Áp dụng|Chia|Thêm|Giao)") }).First;
        var resp = await manager.RunAndWaitForResponseAsync(() => apply.ClickAsync(),
            r => r.Url.Contains("/order-line-teams/bulk"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Expect(manager.Locator(".ant-message-success").First).ToBeVisibleAsync();
        await Expect(manager.Locator("tbody .ant-checkbox-checked")).ToHaveCountAsync(0);
        await LineTeamsAsync(manager, StartOrder);
        foreach (var label in codes)
            await Expect(manager.GetByRole(AriaRole.Row).Filter(new() { Has = manager.GetByLabel(label, new() { Exact = true }) })).ToContainTextAsync(team);
    }

    [Fact]
    public async Task ST_M4_020()
    {
        CaseId = "ST_M4_020";
        var manager = await AsAsync("Manager");
        var rows = await LineTeamsAsync(manager, StartOrder);
        var box = rows.First.GetByRole(AriaRole.Combobox);
        await box.ClickAsync();
        await Expect(Options(manager).First).ToBeVisibleAsync();
        var options = (await Options(manager).AllInnerTextsAsync()).Select(o => o.Trim()).ToList();
        options.Should().NotContain(o => o.StartsWith("CAT") || o.StartsWith("MAY0"));
        options.Should().Contain(o => o.StartsWith("MAY1"));
        await manager.Keyboard.PressAsync("Escape");
        var admin = await AsAsync("Admin");
        await GotoAndWaitAsync(admin, "/teams");
        var teams = await GetDataAsync(admin, "/api/v1/admin/teams?includeDiscontinued=true");
        var stopped = teams.EnumerateArray().First(t => t.GetProperty("teamCode").GetString() == "MAY0").GetProperty("teamId").GetInt32();
        var orders = await GetDataAsync(manager, $"/api/v1/manager/order-line-teams?page=1&pageSize=20&keyword={StartOrder}");
        var items = orders.ValueKind == JsonValueKind.Array ? orders : orders.GetProperty("items");
        var lineId = items.EnumerateArray().First().GetProperty("orderLineId").GetInt32();
        var resp = await manager.Context.APIRequest.PutAsync($"{BaseUrl}/api/v1/manager/order-line-teams",
            new() { Headers = await AuthAsync(manager), DataObject = new { orderLineId = lineId, teamIds = new[] { stopped } } });
        resp.Status.Should().BeInRange(400, 499, await resp.TextAsync());
    }

    [Fact]
    public async Task ST_M4_021()
    {
        CaseId = "ST_M4_021";
        var admin = await AsAsync("Admin");
        await GotoAndWaitAsync(admin, "/admin/orders/new");
        var customers = await GetDataAsync(admin, "/api/v1/admin/customers?page=1&pageSize=100");
        var stopped = customers.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("status").GetString()!.ToUpperInvariant() != "ACTIVE");
        var name = stopped.GetProperty("customerName").GetString()!;
        var box = admin.GetByRole(AriaRole.Combobox, new() { Name = "Khách hàng" });
        await box.ClickAsync();
        await box.FillAsync(name);
        await Expect(admin.Locator(".ant-select-dropdown:visible")).ToBeVisibleAsync();
        await Expect(Options(admin).Filter(new() { HasText = name })).ToHaveCountAsync(0);
        await admin.Keyboard.PressAsync("Escape");
        var picker = admin.Locator(".ant-select").Filter(new() { HasText = "Chọn mã hàng" }).First;
        await picker.ClickAsync();
        await picker.GetByRole(AriaRole.Combobox).FillAsync("VD-0101");
        await Expect(admin.Locator(".ant-select-dropdown:visible").Last).ToBeVisibleAsync();
        await Expect(Options(admin).Filter(new() { HasText = "VD-0101" })).ToHaveCountAsync(0);
        var products = await GetDataAsync(admin, "/api/v1/admin/products?page=1&pageSize=100&keyword=VD-0101");
        var productId = products.GetProperty("items").EnumerateArray().First().GetProperty("productId").GetInt32();
        // Lấy payload hợp lệ do form gửi (chặn lại, không tạo đơn) rồi thay id đã ngừng
        string? payload = null;
        await admin.RouteAsync(new Regex(@"/admin/customer-orders$"), async r =>
        {
            if (r.Request.Method != "POST") { await r.ContinueAsync(); return; }
            payload = r.Request.PostData;
            await r.FulfillAsync(new() { Status = 418, ContentType = "application/json", Body = "{}" });
        });
        await OpenNewOrderAsync(admin);
        await FillHeaderAsync(admin, "ST_AUTO_DH" + Next());
        await FillBlockAsync(admin);
        await SubmitAsync(admin);
        await admin.UnrouteAllAsync();
        payload.Should().NotBeNull();
        var customerId = stopped.GetProperty("customerId").GetInt32();
        foreach (var body in new[]
                 {
                     Regex.Replace(payload!, "\"customerId\":\\d+", $"\"customerId\":{customerId}"),
                     Regex.Replace(payload!, "\"productId\":\\d+", $"\"productId\":{productId}"),
                 })
        {
            var headers = await AuthAsync(admin);
            headers["Content-Type"] = "application/json";
            var resp = await admin.Context.APIRequest.PostAsync($"{BaseUrl}/api/v1/admin/customer-orders", new() { Headers = headers, Data = body });
            resp.Status.Should().BeInRange(400, 499, await resp.TextAsync());
            Regex.IsMatch(await resp.TextAsync(), "\"message\":\"[^\"]+").Should().BeTrue();
        }
    }

    [Fact]
    public async Task ST_M4_022()
    {
        CaseId = "ST_M4_022";
        var forbidden = new List<string>();
        foreach (var role in new[] { "Worker", "Accountant" })
        {
            var page = await AsAsync(role);
            await GotoAndWaitAsync(page, "/");
            page.Request += (_, r) => { if (Regex.IsMatch(r.Url, "/api/v1/(admin/customer-orders|manager/)")) forbidden.Add($"{role} {r.Url}"); };
            foreach (var path in new[] { "/admin/orders", "/order-line-teams", "/manager/orders" })
            {
                await page.GotoAsync(path);
                await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
            }
        }
        forbidden.Should().BeEmpty();
        var (u1, p1) = GetAccount("Admin");
        var admin = await NewAnonymousPageAsync();
        await LoginUiAsync(admin, u1, p1);
        InvalidateRole("Admin");
        await OpenOrderAsync(admin, RunningOrder);
        await admin.Locator("button.app-user").ClickAsync();
        await admin.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync();
        await admin.WaitForURLAsync(u => u.Contains("/login"));
        await admin.GoBackAsync();
        await admin.WaitForURLAsync(u => u.Contains("/login"));
        await Expect(admin.GetByText(RunningOrder)).ToHaveCountAsync(0);
    }
}
