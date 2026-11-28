using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using static System.Tests.UiKit;

namespace System.Tests.Module5;

[TestCaseOrderer("System.Tests.AlphabeticalOrderer", "System.Tests")]
public class Module5Tests : BaseSystemTest
{
    private const int MaterialId = 26;            // NVL seed "Kim máy may số 14" dùng cho đơn mua dự trữ
    private const int OldPaymentOrderId = 7;      // đơn seed có lần thanh toán ghi từ lâu
    private const string NoSupplier = "Chưa có nhà cung cấp nào đang hợp tác. Nhờ Admin thêm trước.";
    private const string Place = "Chọn NCC và đặt hàng";
    private const string Pay = "Xác nhận thanh toán";
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhWMjWwAAAABJRU5ErkJggg==");

    private async Task<IPage> AsAsync(string role)
    {
        var page = await NewPageAsRoleAsync(role);
        page.SetDefaultTimeout(15000);
        await page.GotoAsync("/");
        return page;
    }

    // ---------------------------------------------------------------- API công khai để bơm / dọn dữ liệu

    private static async Task<Dictionary<string, string>> AuthAsync(IPage page) => new()
    {
        ["Authorization"] = "Bearer " + await page.EvaluateAsync<string>("localStorage.getItem('nxmtd.accessToken')"),
    };

    private static async Task<(int Status, JsonElement Body, string Text)> CallAsync(IPage page, string method, string path,
        object? data = null, IFormData? form = null)
    {
        var r = await page.Context.APIRequest.FetchAsync($"{BaseUrl}/api/v1{path}",
            new() { Method = method, Headers = await AuthAsync(page), DataObject = data, Multipart = form });
        var text = await r.TextAsync();
        var body = text.StartsWith('{') ? JsonDocument.Parse(text).RootElement.Clone() : default;
        return (r.Status, body, text);
    }

    private static async Task<JsonElement> ApiAsync(IPage page, string method, string path, object? data = null, IFormData? form = null)
    {
        var (status, body, text) = await CallAsync(page, method, path, data, form);
        status.Should().BeInRange(200, 299, $"dữ liệu mồi {method} {path}: {text}");
        return body.TryGetProperty("data", out var d) ? d : default;
    }

    private static string IsoToday => DateTime.Now.ToString("yyyy-MM-dd");

    private async Task<(int Id, string Name)> SeedSupplierAsync(IPage admin)
    {
        var name = "ST_AUTO_NCC " + Next();
        var data = await ApiAsync(admin, "POST", "/admin/suppliers", new
        {
            supplierName = name, phone = UniquePhone(), email = $"st_auto{Next()}@example.com",
            address = "ST_AUTO_ dia chi", materialTypes = "ST_AUTO_ kim may", note = "ST_AUTO_ du lieu moi",
        });
        return (data.GetInt32(), name);
    }

    private static Task<(int, JsonElement, string)> DiscontinueAsync(IPage admin, int id) =>
        CallAsync(admin, "POST", $"/admin/suppliers/{id}/discontinue", new { reason = "ST_AUTO_ don dep" });

    private sealed record Po(int Id, string Code, int SupplierId, string SupplierName);

    /// <summary>Đơn mua dự trữ đã đặt hàng, giá trị mặc định 100 × 100.000 = 10.000.000.</summary>
    private async Task<Po> SeedPoAsync()
    {
        var admin = await AsAsync("Admin");
        var supplier = await SeedSupplierAsync(admin);
        var manager = await AsAsync("Manager");
        var data = await ApiAsync(manager, "POST", "/manager/purchase-orders", new
        {
            supplierId = supplier.Id, orderDate = IsoToday, expectedDeliveryCount = 1,
            expectedDeliveryDates = new[] { IsoToday }, note = "ST_AUTO_ don mua kiem thu",
            lines = new[] { new { materialId = MaterialId, orderedQty = 100, unitPrice = 100000 } },
        });
        await DiscontinueAsync(admin, supplier.Id);
        return new Po(data.GetProperty("purchaseOrderId").GetInt32(), data.GetProperty("poCode").GetString()!, supplier.Id, supplier.Name);
    }

    private static Task<JsonElement> PaymentDetailAsync(IPage accountant, int id) =>
        ApiAsync(accountant, "GET", $"/accountant/purchase-payments/{id}");

    private static async Task<string> RowVerAsync(IPage accountant, int id) =>
        (await PaymentDetailAsync(accountant, id)).GetProperty("rowVer").GetString()!;

    private static async Task<JsonElement> UploadAsync(IPage page, string ownerType, int ownerId, string purpose)
    {
        var form = page.Context.APIRequest.CreateFormData();
        form.Set("ownerType", ownerType);
        form.Set("ownerId", ownerId.ToString());
        form.Set("purpose", purpose);
        form.Set("file", new FilePayload { Name = "st_auto.png", MimeType = "image/png", Buffer = Png });
        return await ApiAsync(page, "POST", "/common/attachments", form: form);
    }

    private static async Task SeedPaymentAsync(IPage accountant, int id, decimal amount)
    {
        await UploadAsync(accountant, "PurchaseOrder", id, "PaymentProof");
        await ApiAsync(accountant, "POST", $"/accountant/purchase-payments/{id}/confirm", new
        {
            paymentDate = IsoToday, paidAmount = amount, paymentMethod = "BankTransfer",
            paymentNote = "ST_AUTO_ thanh toan moi", rowVer = await RowVerAsync(accountant, id),
        });
        await WaitNextServerSecondAsync(accountant, id);
    }

    /// <summary>Server lưu thời gian tới giây, ảnh chỉ tính là mới khi tải sau giây của lần thanh toán: thử tải rồi gỡ đến khi sang giây mới.</summary>
    private static async Task WaitNextServerSecondAsync(IPage accountant, int id)
    {
        var paidAt = (await PaymentDetailAsync(accountant, id)).GetProperty("payments").EnumerateArray()
            .Max(p => p.GetProperty("createdAt").GetDateTimeOffset());
        var limit = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limit)
        {
            var probe = await UploadAsync(accountant, "PurchaseOrder", id, "PaymentProof");
            var newer = probe.GetProperty("uploadedAt").GetDateTimeOffset() > paidAt;
            await ApiAsync(accountant, "DELETE", $"/common/attachments/{probe.GetProperty("attachmentId").GetInt32()}");
            if (newer) return;
        }
    }

    /// <summary>Thủ kho nhập thiếu một nửa, Quản đốc xác nhận rồi chốt không bù.</summary>
    private async Task CloseShortAsync(Po po)
    {
        var manager = await AsAsync("Manager");
        var keeper = await AsAsync("WarehouseKeeper");
        var detail = await ApiAsync(manager, "GET", $"/manager/purchase-order-tracking/{po.Id}");
        var lineId = detail.GetProperty("lines")[0].GetProperty("purchaseOrderLineId").GetInt32();
        var receipt = await ApiAsync(keeper, "POST", "/warehouse-keeper/material-receipts", new
        {
            purchaseOrderId = po.Id, source = "SUPPLIER", receiptDate = IsoToday, note = "ST_AUTO_ nhap thieu",
            lines = new[] { new { purchaseOrderLineId = lineId, acceptedQty = 50 } },
        });
        var receiptId = receipt.GetProperty("materialReceiptId").GetInt32();
        await UploadAsync(keeper, "MaterialReceipt", receiptId, "DocumentPhoto");
        await ApiAsync(manager, "POST", $"/manager/material-receipts/{receiptId}/confirm");
        var shortId = (await ApiAsync(manager, "GET", $"/manager/material-receipts/{receiptId}"))
            .GetProperty("shortDeliveryRequestId").GetInt32();
        await ApiAsync(manager, "POST", $"/manager/short-deliveries/{shortId}/resolve",
            new { resolution = "NO_REPLENISH", note = "ST_AUTO_ chot khong bu" });
    }

    private static async Task<List<JsonElement>> PendingOrdersAsync(IPage manager)
    {
        var data = await ApiAsync(manager, "GET", "/manager/purchase-order-tracking?page=1&pageSize=100&goodsStatus=PendingFinalisation");
        return data.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("goodsStatus").GetString() == "PendingFinalisation").ToList();
    }

    private static async Task<int> PendingOrderIdAsync(IPage manager) =>
        (await PendingOrdersAsync(manager)).OrderBy(i => i.GetProperty("orderCode").ValueKind == JsonValueKind.Null ? 0 : 1)
        .ThenBy(i => i.GetProperty("purchaseOrderId").GetInt32()).First().GetProperty("purchaseOrderId").GetInt32();

    // ---------------------------------------------------------------- UI

    private static ILocator Detail(IPage page) => page.GetByRole(AriaRole.Dialog).First;
    private static ILocator Modal(IPage page, string title) => Dialog(page, title);
    private static ILocator ConfirmBox(IPage page) => page.Locator(".ant-modal-confirm").Last;
    private static ILocator FieldError(ILocator input) =>
        input.Locator("xpath=ancestor::div[contains(concat(' ',@class,' '),' ant-form-item ')][1]").Locator(".ant-form-item-explain-error").First;

    /// <summary>Ô báo lỗi ở trạng thái ổn định: chờ hiệu ứng ẩn/hiện thông báo chạy xong để không bắt nhầm lỗi thoáng qua lúc gõ.</summary>
    private static async Task ExpectInvalidAsync(IPage page, ILocator input)
    {
        await Assertions.Expect(page.Locator("[class*='show-help-appear'], [class*='show-help-leave'], [class*='show-help-enter']")).ToHaveCountAsync(0);
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-invalid", "true", new() { Timeout = 3000 });
        await Assertions.Expect(FieldError(input)).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    private static async Task<ILocator> OpenPoAsync(IPage page, int id, string path)
    {
        await page.GotoAsync($"{path}?id={id}");
        var dlg = Detail(page);
        await Assertions.Expect(dlg.GetByText("Lịch sử thay đổi")).ToBeVisibleAsync();
        return dlg;
    }

    private static async Task<ILocator> ListRowAsync(IPage page, string path, string code)
    {
        await page.GotoAsync(path);
        var search = page.GetByPlaceholder("Tìm theo mã đơn mua, mã đơn hàng, nhà cung cấp");
        await search.FillAsync(code);
        await search.PressAsync("Enter");
        return page.GetByRole(AriaRole.Row).Filter(new() { HasText = code });
    }

    private static async Task<ILocator> SupplierRowAsync(IPage page, string name)
    {
        await page.GotoAsync("/suppliers");
        var search = page.GetByPlaceholder("Tìm theo tên, số điện thoại");
        await search.FillAsync(name);
        await search.PressAsync("Enter");
        return page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = name, Exact = true }) });
    }

    private static async Task<ILocator> OpenSupplierFormAsync(IPage page)
    {
        await page.GotoAsync("/suppliers");
        await Button(page.GetByRole(AriaRole.Main), "plus Thêm nhà cung cấp")
            .Or(page.GetByRole(AriaRole.Button, new() { Name = "Thêm nhà cung cấp" })).First.ClickAsync();
        return Modal(page, "Thêm nhà cung cấp");
    }

    private static async Task FillSupplierAsync(ILocator form, string name, string phone, string email)
    {
        await form.GetByLabel("Tên nhà cung cấp").FillAsync(name);
        await form.GetByLabel("Số điện thoại").FillAsync(phone);
        await form.GetByLabel("Email").FillAsync(email);
        await form.GetByLabel("Địa chỉ").FillAsync("ST_AUTO_ 12 Tan Phu, TP.HCM");
        await form.GetByLabel("Loại NVL cung cấp").FillAsync("ST_AUTO_ vai, chi");
        await form.GetByLabel("Ghi chú").FillAsync("ST_AUTO_ ghi chu");
    }

    private static async Task<IResponse> SaveSupplierAsync(IPage page, ILocator form) =>
        await page.RunAndWaitForResponseAsync(() => Button(form, "Lưu").ClickAsync(),
            r => r.Url.Contains("/admin/suppliers") && r.Request.Method is "POST" or "PUT");

    private static string UniquePhone() => "09" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("00000000");

    private static async Task<ILocator> OpenPlaceAsync(IPage page, int id)
    {
        var dlg = await OpenPoAsync(page, id, "/purchase-orders");
        await Button(dlg, Place).ClickAsync();
        return Modal(page, "Chọn nhà cung cấp và đặt hàng");
    }

    private static ILocator SupplierBox(ILocator form) =>
        form.GetByRole(AriaRole.Combobox, new() { NameRegex = new Regex(@"^(\* )?Nhà cung cấp$") });

    private static async Task ExpectSupplierOptionAsync(IPage page, ILocator form, string name)
    {
        var box = SupplierBox(form);
        await box.FocusAsync();
        await box.FillAsync(name);
        await Assertions.Expect(Options(page).Filter(new() { HasText = name }).First).ToBeVisibleAsync();
    }

    /// <summary>Ô chọn đã có giá trị thì chữ đang chọn che ô nhập, nên bấm thẳng vào ô.</summary>
    private static async Task PickAsync(IPage page, ILocator combobox, string text)
    {
        await combobox.FocusAsync();
        await combobox.PressAsync("ArrowDown");
        if (await combobox.IsEditableAsync()) await combobox.FillAsync(text);
        await Options(page).Filter(new() { HasText = text }).First.ClickAsync();
    }

    /// <summary>Gõ từng phím như người dùng để các ô số chặn / sửa phím đúng như thực tế.</summary>
    private static async Task TypeAsync(ILocator input, string value)
    {
        await input.ClickAsync();
        await input.PressAsync("Control+a");
        await input.PressAsync("Delete");
        if (value.Length > 0) await input.PressSequentiallyAsync(value);
        await input.PressAsync("Tab");
    }

    private static async Task<ILocator> OpenPayAsync(IPage accountant, int id)
    {
        var dlg = await OpenPoAsync(accountant, id, "/purchase-payments");
        await Button(dlg, Pay).ClickAsync();
        return Modal(accountant, "Thêm lần thanh toán đơn mua");
    }

    private static async Task UploadInUiAsync(IPage page, ILocator scope, FilePayload file) =>
        await scope.Locator("input[type=file]").SetInputFilesAsync(file);

    private static async Task AddProofAsync(IPage page, ILocator scope)
    {
        var resp = await page.RunAndWaitForResponseAsync(
            () => UploadInUiAsync(page, scope, new FilePayload { Name = "st_auto.png", MimeType = "image/png", Buffer = Png }),
            r => r.Url.Contains("/common/attachments") && r.Request.Method == "POST");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await Assertions.Expect(scope.GetByText(new Regex("Ảnh mới cho lần này"))).ToBeVisibleAsync();
    }

    private static async Task<IResponse> SubmitPayAsync(IPage page, ILocator modal)
    {
        await Button(modal, Pay).ClickAsync();
        return await page.RunAndWaitForResponseAsync(() => Button(ConfirmBox(page), "Xác nhận").ClickAsync(),
            r => r.Url.EndsWith("/confirm") && r.Request.Method == "POST");
    }

    // ---------------------------------------------------------------- Nhà cung cấp

    [Fact]
    public async Task ST_M5_001()
    {
        CaseId = "ST_M5_001";
        var admin = await AsAsync("Admin");
        var name = "ST_AUTO_NCC " + Next();
        var id = 0;
        try
        {
            var form = await OpenSupplierFormAsync(admin);
            await FillSupplierAsync(form, name, UniquePhone(), $"st_auto{Next()}@example.com");
            var resp = await SaveSupplierAsync(admin, form);
            resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
            id = JsonDocument.Parse(await resp.TextAsync()).RootElement.GetProperty("data").GetInt32();
            await Expect(admin.Locator(".ant-message-success").First).ToBeVisibleAsync();
            await Expect(await SupplierRowAsync(admin, name)).ToHaveCountAsync(1);
            await admin.ReloadAsync();
            await Expect(await SupplierRowAsync(admin, name)).ToHaveCountAsync(1);
            var manager = await AsAsync("Manager");
            (await ApiAsync(manager, "GET", "/manager/suppliers")).GetRawText().Should().Contain(id.ToString());
            var place = await OpenPlaceAsync(manager, await PendingOrderIdAsync(manager));
            await ExpectSupplierOptionAsync(manager, place, name);
        }
        finally { if (id > 0) await DiscontinueAsync(admin, id); }
    }

    [Fact]
    public async Task ST_M5_002()
    {
        CaseId = "ST_M5_002";
        var admin = await AsAsync("Admin");
        var id = 0;
        try
        {
            var form = await OpenSupplierFormAsync(admin);
            await Button(form, "Lưu").ClickAsync();
            await SoftAsync("bỏ trống tên không báo 'Vui lòng nhập tên nhà cung cấp.'",
                () => Expect(form.GetByText("Vui lòng nhập tên nhà cung cấp.")).ToBeVisibleAsync());
            var name = ("ST_AUTO_" + Next() + new string('N', 200))[..200];
            await FillSupplierAsync(form, name + "Z", UniquePhone(), "ncc@");
            var typed = await form.GetByLabel("Tên nhà cung cấp").InputValueAsync();
            await SoftAsync("tên 201 ký tự không bị chặn", () => { typed.Length.Should().BeLessThanOrEqualTo(200); return Task.CompletedTask; });
            await Button(form, "Lưu").ClickAsync();
            await SoftAsync("email 'ncc@' không báo 'Email không hợp lệ.'",
                () => Expect(form.GetByText("Email không hợp lệ.")).ToBeVisibleAsync());
            var tail = Next();
            var email = "st_auto" + tail + new string('a', 48) + "@" + new string('b', 35) + ".com";
            email.Length.Should().Be(100);
            await form.GetByLabel("Email").FillAsync(email + "m");
            var typedMail = await form.GetByLabel("Email").InputValueAsync();
            await SoftAsync("email 101 ký tự không bị chặn", () => { typedMail.Length.Should().BeLessThanOrEqualTo(100); return Task.CompletedTask; });
            await form.GetByLabel("Email").FillAsync(email);
            var resp = await SaveSupplierAsync(admin, form);
            resp.Status.Should().BeInRange(200, 299, "tên 200 và email 100 ký tự phải lưu được: " + await resp.TextAsync());
            id = JsonDocument.Parse(await resp.TextAsync()).RootElement.GetProperty("data").GetInt32();
            var saved = (await ApiAsync(admin, "GET", $"/admin/suppliers/{id}")).GetProperty("supplier");
            saved.GetProperty("supplierName").GetString().Should().Be(name);
            saved.GetProperty("email").GetString().Should().Be(email);
            SoftVerify();
        }
        finally { if (id > 0) await DiscontinueAsync(admin, id); }
    }

    [Fact]
    public async Task ST_M5_003()
    {
        CaseId = "ST_M5_003";
        var admin = await AsAsync("Admin");
        var name = "ST_AUTO_NCC Vai Thanh Cong " + Next();
        var phone = UniquePhone();
        var ids = new List<int>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var form = await OpenSupplierFormAsync(admin);
                await FillSupplierAsync(form, name, phone, $"st_auto{Next()}@example.com");
                var resp = await SaveSupplierAsync(admin, form);
                resp.Status.Should().BeLessThan(500, await resp.TextAsync());
                if (resp.Status < 300)
                {
                    ids.Add(JsonDocument.Parse(await resp.TextAsync()).RootElement.GetProperty("data").GetInt32());
                    await Expect(form).ToHaveCountAsync(0);
                }
                else
                {
                    i.Should().Be(1, "lần tạo đầu phải thành công");
                    await Expect(Errors(admin).First).ToBeVisibleAsync();
                    await Expect(form).ToBeVisibleAsync();
                }
            }
            await Expect(await SupplierRowAsync(admin, name)).ToHaveCountAsync(ids.Count);
        }
        finally { foreach (var id in ids) await DiscontinueAsync(admin, id); }
    }

    [Fact]
    public async Task ST_M5_004()
    {
        CaseId = "ST_M5_004";
        var admin = await AsAsync("Admin");
        var mine = await SeedSupplierAsync(admin);
        var manager = await AsAsync("Manager");
        var pending = await PendingOrderIdAsync(manager);
        var others = (await ApiAsync(manager, "GET", "/manager/suppliers")).EnumerateArray()
            .Select(s => s.GetProperty("supplierId").GetInt32()).Where(i => i != mine.Id).ToList();
        try
        {
            foreach (var id in others) (await DiscontinueAsync(admin, id)).Item1.Should().BeInRange(200, 299);
            var row = await SupplierRowAsync(admin, mine.Name);
            await Button(row, "Ngừng hợp tác").ClickAsync();
            var note = admin.GetByRole(AriaRole.Dialog).Last;
            var resp = await admin.RunAndWaitForResponseAsync(() => Button(note, "Xác nhận").ClickAsync(),
                r => r.Url.EndsWith("/discontinue"));
            resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
            await admin.ReloadAsync();
            row = await SupplierRowAsync(admin, mine.Name);
            await Expect(row).ToContainTextAsync("Ngừng hợp tác");
            var place = await OpenPlaceAsync(manager, pending);
            await Expect(place.GetByText(NoSupplier)).ToBeVisibleAsync();
            var again = await admin.RunAndWaitForResponseAsync(() => Button(row, "Hợp tác lại").ClickAsync(),
                r => r.Url.EndsWith("/reactivate"));
            again.Status.Should().BeInRange(200, 299, await again.TextAsync());
            place = await OpenPlaceAsync(manager, pending);
            await ExpectSupplierOptionAsync(manager, place, mine.Name);
        }
        finally
        {
            foreach (var id in others) await CallAsync(admin, "POST", $"/admin/suppliers/{id}/reactivate");
            await DiscontinueAsync(admin, mine.Id);
        }
    }

    // ---------------------------------------------------------------- Đặt hàng / hủy

    [Fact]
    public async Task ST_M5_005()
    {
        CaseId = "ST_M5_005";
        var manager = await AsAsync("Manager");
        var id = await PendingOrderIdAsync(manager);
        var before = await ApiAsync(manager, "GET", $"/manager/purchase-order-tracking/{id}");
        var code = before.GetProperty("poCode").GetString()!;
        var qty = before.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("orderedQty").GetDecimal()).ToList();
        var supplier = (await ApiAsync(manager, "GET", "/manager/suppliers")).EnumerateArray().First().GetProperty("supplierName").GetString()!;
        var form = await OpenPlaceAsync(manager, id);
        await PickAsync(manager, SupplierBox(form), supplier);
        await TypeAsync(form.GetByLabel("Số lần nhà cung cấp dự kiến giao"), "2");
        await DateAsync(form.GetByLabel("Ngày giao lần 1"), DateTime.Now.AddDays(2).ToString("dd/MM/yyyy"));
        await DateAsync(form.GetByLabel("Ngày giao lần 2"), DateTime.Now.AddDays(5).ToString("dd/MM/yyyy"));
        var prices = form.GetByPlaceholder("Đơn giá");
        await Expect(prices).ToHaveCountAsync(qty.Count);
        for (var i = 0; i < qty.Count; i++) await TypeAsync(prices.Nth(i), "10000");
        var total = (qty.Sum() * 10000).ToString("#,0", new System.Globalization.CultureInfo("vi-VN"));
        await Expect(form).ToContainTextAsync(total);
        var resp = await manager.RunAndWaitForResponseAsync(async () =>
        {
            await Button(form, Place).ClickAsync();
            await Expect(form.Or(ConfirmBox(manager)).First).ToBeVisibleAsync();
            if (await ConfirmBox(manager).IsVisibleAsync()) await Button(ConfirmBox(manager), "Xác nhận").ClickAsync();
        }, r => r.Url.EndsWith("/place"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await manager.ReloadAsync();
        var detail = Detail(manager);
        await Expect(detail).ToContainTextAsync("Đã đặt hàng");
        await Expect(detail).ToContainTextAsync(supplier);
        await Expect(detail).ToContainTextAsync(total);
        await Expect(Button(detail, Place)).ToHaveCountAsync(0);
        var accountant = await AsAsync("Accountant");
        await Expect(await ListRowAsync(accountant, "/purchase-payments", code)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ST_M5_006()
    {
        CaseId = "ST_M5_006";
        var manager = await AsAsync("Manager");
        var pending = (await PendingOrdersAsync(manager)).First(i => i.GetProperty("supplierName").ValueKind != JsonValueKind.Null)
            .GetProperty("purchaseOrderId").GetInt32();
        var posts = 0;
        await manager.RouteAsync(new Regex(@"/purchase-order-tracking/\d+/place$"), r =>
        {
            posts++;
            return r.FulfillAsync(new() { Status = 418, ContentType = "application/json", Body = "{}" });
        });
        var form = await OpenPlaceAsync(manager, pending);
        var count = form.GetByLabel("Số lần nhà cung cấp dự kiến giao");
        const string countError = "Nhập số lần giao dự kiến";
        foreach (var good in new[] { "1", "99" })
        {
            await TypeAsync(count, good);
            await SoftAsync($"số lần giao {good} không được chấp nhận", async () =>
            {
                await Expect(count).ToHaveValueAsync(good);
                await Expect(form.GetByText(countError)).ToHaveCountAsync(0);
            });
        }
        foreach (var bad in new[] { "0", "100" })
        {
            await TypeAsync(count, bad);
            var shown = await count.InputValueAsync();
            await SoftAsync($"số lần giao '{bad}' không báo lỗi mà ô tự đổi thành '{shown}'",
                () => ExpectInvalidAsync(manager, count));
        }
        await TypeAsync(count, "1");
        var price = form.GetByPlaceholder("Đơn giá").First;
        var qty = form.GetByRole(AriaRole.Spinbutton).Nth(1); // sau ô "Số lần giao" là ô số lượng của dòng đầu
        var original = await qty.InputValueAsync();
        await TypeAsync(qty, "0");
        await SoftAsync("số lượng dòng 0 không báo 'Nhập số lượng lớn hơn 0.'",
            () => Expect(form.GetByText("Nhập số lượng lớn hơn 0.")).ToBeVisibleAsync(new() { Timeout = 3000 }));
        await TypeAsync(qty, original);
        await TypeAsync(price, "0");
        await SoftAsync("đơn giá 0 không báo 'Nhập đơn giá lớn hơn 0.'",
            () => Expect(form.GetByText("Nhập đơn giá lớn hơn 0.").First).ToBeVisibleAsync(new() { Timeout = 3000 }));
        await Button(form, Place).ClickAsync();
        await SoftAsync("còn ô đỏ nhưng vẫn gửi API đặt hàng", () => { posts.Should().Be(0); return Task.CompletedTask; });
        await TypeAsync(price, "0,5");
        var half = await price.InputValueAsync();
        await SoftAsync($"đơn giá gõ '0,5' thành '{half}': không làm tròn đúng và không báo lỗi", async () =>
        {
            if (half is "0" or "1" or "0,5") return;
            await ExpectInvalidAsync(manager, price);
        });
        var note = form.GetByLabel("Ghi chú");
        await note.FillAsync(new string('n', 1000) + "Z");
        var noteLength = (await note.InputValueAsync()).Length;
        await SoftAsync("ghi chú 1001 ký tự không bị chặn", () => { noteLength.Should().Be(1000); return Task.CompletedTask; });

        var chosen = (await ApiAsync(manager, "GET", $"/manager/purchase-order-tracking/{pending}")).GetProperty("supplierId").GetInt32();
        await manager.RouteAsync(new Regex(@"/api/v1/manager/suppliers$"), async r =>
        {
            var response = await r.FetchAsync();
            var json = System.Text.Json.Nodes.JsonNode.Parse(await response.TextAsync())!;
            var list = json["data"]!.AsArray();
            foreach (var item in list.Where(x => x!["supplierId"]!.GetValue<int>() == chosen).ToList()) list.Remove(item);
            await r.FulfillAsync(new() { Response = response, Body = json.ToJsonString() });
        });
        form = await OpenPlaceAsync(manager, pending);
        posts = 0;
        await TypeAsync(form.GetByPlaceholder("Đơn giá").First, "1000");
        await Button(form, Place).ClickAsync();
        await SoftAsync("thiếu nhà cung cấp không báo 'Vui lòng chọn nhà cung cấp.'",
            () => Expect(form.GetByText("Vui lòng chọn nhà cung cấp.")).ToBeVisibleAsync());
        await SoftAsync("thiếu nhà cung cấp nhưng vẫn gửi API", () => { posts.Should().Be(0); return Task.CompletedTask; });
        await manager.UnrouteAllAsync();
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_007()
    {
        CaseId = "ST_M5_007";
        var po = await SeedPoAsync();
        var manager = await AsAsync("Manager");
        var dlg = await OpenPoAsync(manager, po.Id, "/purchase-orders");
        var cancel = Button(dlg, "Hủy đơn mua");
        await Expect(cancel).ToBeVisibleAsync(new() { Timeout = 5000 });
        await cancel.ClickAsync();
        var reason = manager.GetByRole(AriaRole.Dialog).Last;
        await reason.GetByRole(AriaRole.Textbox).FillAsync("ST_AUTO_ ly do huy");
        var resp = await manager.RunAndWaitForResponseAsync(() => Button(reason, "Xác nhận").ClickAsync(),
            r => r.Url.EndsWith("/cancel"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await manager.ReloadAsync();
        await Expect(Detail(manager)).ToContainTextAsync("Đã hủy");
        await Expect(Button(Detail(manager), "Hủy đơn mua")).ToHaveCountAsync(0);
        var accountant = await AsAsync("Accountant");
        await Expect(await ListRowAsync(accountant, "/purchase-payments", po.Code)).ToHaveCountAsync(0);
    }

    // ---------------------------------------------------------------- Thanh toán

    [Fact]
    public async Task ST_M5_008()
    {
        CaseId = "ST_M5_008";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        var modal = await OpenPayAsync(accountant, po.Id);
        await AddProofAsync(accountant, modal);
        await DateAsync(modal.GetByLabel("Ngày thanh toán"), Today);
        await PickAsync(accountant, modal.GetByRole(AriaRole.Combobox, new() { Name = "Hình thức" }), "Chuyển khoản");
        await TypeAsync(modal.GetByLabel("Số tiền thanh toán"), "4000000");
        var resp = await SubmitPayAsync(accountant, modal);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await accountant.ReloadAsync();
        await Expect(Detail(accountant)).ToContainTextAsync("Thanh toán một phần");
        await Expect(Detail(accountant)).ToContainTextAsync("4.000.000");
        var row = await ListRowAsync(accountant, "/purchase-payments", po.Code);
        await Expect(row).ToContainTextAsync("Thanh toán một phần");
        await Expect(row).ToContainTextAsync("4.000.000");
        var manager = await AsAsync("Manager");
        await Expect(await ListRowAsync(manager, "/purchase-orders", po.Code)).ToContainTextAsync("4.000.000");
        var detail = await PaymentDetailAsync(accountant, po.Id);
        detail.GetProperty("payments").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ST_M5_009()
    {
        CaseId = "ST_M5_009";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        await SeedPaymentAsync(accountant, po.Id, 4_000_000);
        var modal = await OpenPayAsync(accountant, po.Id);
        await AddProofAsync(accountant, modal);
        await TypeAsync(modal.GetByLabel("Số tiền thanh toán"), "6000000");
        var resp = await SubmitPayAsync(accountant, modal);
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await accountant.ReloadAsync();
        await Expect(Detail(accountant)).ToContainTextAsync("Đã thanh toán");
        await Expect(Button(Detail(accountant), Pay)).ToHaveCountAsync(0);
        var detail = await PaymentDetailAsync(accountant, po.Id);
        detail.GetProperty("paymentStatus").GetString().Should().Be("Paid");
        detail.GetProperty("paidAmount").GetDecimal().Should().Be(detail.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task ST_M5_010()
    {
        CaseId = "ST_M5_010";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        await SeedPaymentAsync(accountant, po.Id, 4_000_000);
        var posts = 0;
        accountant.Request += (_, r) => { if (r.Url.EndsWith("/confirm") && r.Method == "POST") posts++; };
        var modal = await OpenPayAsync(accountant, po.Id);
        await AddProofAsync(accountant, modal);
        var amount = modal.GetByLabel("Số tiền thanh toán");

        async Task InvalidAsync(string what, string value)
        {
            await TypeAsync(amount, value);
            await Button(modal, Pay).ClickAsync();
            await SoftAsync(what, () => ExpectInvalidAsync(accountant, amount));
            await CloseConfirmAsync(accountant);
        }

        await InvalidAsync("số tiền 0 không báo lỗi", "0");
        await InvalidAsync("bỏ trống số tiền không báo lỗi", "");
        await InvalidAsync("số tiền vượt còn phải trả (gõ 6000000,01) không báo lỗi", "6000000,01");

        await TypeAsync(amount, "0,01");
        var tiny = await amount.InputValueAsync();
        await SoftAsync($"số tiền 0,01 không nhập được: ô tự đổi thành '{tiny}'", () => { tiny.Should().Be("0,01"); return Task.CompletedTask; });

        await TypeAsync(amount, "6000000");
        await Button(modal, Pay).ClickAsync();
        await SoftAsync("số tiền 6.000.000 không được chấp nhận", () => Expect(ConfirmBox(accountant)).ToBeVisibleAsync(new() { Timeout = 5000 }));
        await CloseConfirmAsync(accountant);

        var date = modal.GetByLabel("Ngày thanh toán");
        await date.HoverAsync();
        var clear = modal.Locator(".ant-picker-clear");
        if (await clear.CountAsync() > 0)
        {
            await clear.First.ClickAsync();
            await date.PressAsync("Tab");
        }
        else
        {
            await date.FillAsync("");
            await date.PressAsync("Tab");
        }
        if (await date.InputValueAsync() == "")
        {
            await Button(modal, Pay).ClickAsync();
            await SoftAsync("bỏ trống ngày thanh toán không báo lỗi",
                () => Expect(modal.GetByText("Vui lòng chọn ngày thanh toán.")).ToBeVisibleAsync(new() { Timeout = 3000 }));
            await CloseConfirmAsync(accountant);
        }
        await SoftAsync("form đang lỗi nhưng vẫn gửi API", () => { posts.Should().Be(0); return Task.CompletedTask; });
        SoftVerify();
    }

    private static async Task CloseConfirmAsync(IPage page)
    {
        var box = ConfirmBox(page);
        if (await box.IsVisibleAsync())
        {
            await Button(box, "Hủy").ClickAsync();
            await Assertions.Expect(box).ToBeHiddenAsync();
        }
    }

    [Fact]
    public async Task ST_M5_011()
    {
        CaseId = "ST_M5_011";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        var modal = await OpenPayAsync(accountant, po.Id);
        var images = modal.Locator("img");
        for (var i = 1; i <= 5; i++)
        {
            await AddProofAsync(accountant, modal);
            await Expect(images).ToHaveCountAsync(i);
        }
        async Task<int> CountSavedAsync() =>
            (await ApiAsync(accountant, "GET", $"/common/attachments?ownerType=PurchaseOrder&ownerId={po.Id}")).GetArrayLength();
        (await CountSavedAsync()).Should().Be(5);

        await SoftAsync("ảnh thứ 6 không bị chặn (giao diện cho tải tối đa 20 ảnh)",
            () => Expect(modal.GetByRole(AriaRole.Button, new() { Name = "Thêm ảnh" })).ToBeDisabledAsync(new() { Timeout = 3000 }));

        var before = await CountSavedAsync();
        var fake = new byte[2048];
        fake[0] = (byte)'M';
        fake[1] = (byte)'Z';
        var response = await accountant.RunAndWaitForResponseAsync(
            () => UploadInUiAsync(accountant, modal, new FilePayload { Name = "st_auto_virus.jpg", MimeType = "image/jpeg", Buffer = fake }),
            r => r.Url.Contains("/common/attachments") && r.Request.Method == "POST");
        await SoftAsync($"file .exe đổi đuôi .jpg không bị từ chối rõ ràng (HTTP {response.Status})", async () =>
        {
            response.Status.Should().BeInRange(400, 499);
            await Expect(accountant.Locator(".ant-message-error").First).ToBeVisibleAsync();
        });

        var big = new byte[25 * 1024 * 1024];
        Png.CopyTo(big, 0);
        await UploadInUiAsync(accountant, modal, new FilePayload { Name = "st_auto_25mb.png", MimeType = "image/png", Buffer = big });
        await SoftAsync("ảnh 25MB không báo lỗi rõ ràng", () => Expect(accountant.GetByText("Ảnh vượt quá 5 MB.").First).ToBeVisibleAsync());
        var after = await CountSavedAsync();
        await SoftAsync($"file giả / quá lớn vẫn được lưu ({before} → {after} ảnh)", () => { after.Should().Be(before); return Task.CompletedTask; });

        var note = modal.GetByLabel("Ghi chú");
        await note.FillAsync(new string('n', 500) + "Z");
        var length = (await note.InputValueAsync()).Length;
        await SoftAsync("ghi chú 501 ký tự không bị chặn", () => { length.Should().Be(500); return Task.CompletedTask; });

        await accountant.ReloadAsync();
        await SoftAsync("ảnh đã lưu không xem lại được sau F5", async () =>
        {
            var first = Detail(accountant).Locator("img").First;
            await Expect(first).ToBeVisibleAsync();
            (await first.EvaluateAsync<int>("e => e.naturalWidth")).Should().BeGreaterThan(0);
        });
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_012()
    {
        CaseId = "ST_M5_012";
        var po = await SeedPoAsync();
        await CloseShortAsync(po);
        var accountant = await AsAsync("Accountant");
        var dlg = await OpenPoAsync(accountant, po.Id, "/purchase-payments");
        await Button(dlg, "Nhập giá trị thực tế").ClickAsync();
        var modal = Modal(accountant, "Nhập giá trị thực tế");
        var amount = modal.GetByLabel("Giá trị thực tế");
        await TypeAsync(amount, "10000001");
        await Button(modal, "Lưu").ClickAsync();
        await SoftAsync("giá trị lớn hơn tổng đơn không báo lỗi",
            () => Expect(modal.GetByText("Giá trị thực tế không được lớn hơn tổng giá trị đơn mua.")).ToBeVisibleAsync());
        await CloseConfirmAsync(accountant);
        await TypeAsync(amount, "");
        await Button(modal, "Lưu").ClickAsync();
        await SoftAsync("bỏ trống giá trị thực tế không báo lỗi",
            () => Expect(modal.GetByText("Nhập giá trị thực tế lớn hơn 0.")).ToBeVisibleAsync());
        await CloseConfirmAsync(accountant);
        await TypeAsync(amount, "10000000");
        await Button(modal, "Lưu").ClickAsync();
        var resp = await accountant.RunAndWaitForResponseAsync(() => Button(ConfirmBox(accountant), "Xác nhận").ClickAsync(),
            r => r.Url.EndsWith("/actual-amount"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await accountant.ReloadAsync();
        var detail = await PaymentDetailAsync(accountant, po.Id);
        detail.GetProperty("actualAmount").GetDecimal().Should().Be(10_000_000);
        await Button(Detail(accountant), Pay).ClickAsync();
        await Expect(Modal(accountant, "Thêm lần thanh toán đơn mua").GetByLabel("Số tiền thanh toán")).ToHaveValueAsync("10.000.000");
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_013()
    {
        CaseId = "ST_M5_013";
        var po = await SeedPoAsync();
        await CloseShortAsync(po);
        var accountant = await AsAsync("Accountant");
        await ApiAsync(accountant, "PUT", $"/accountant/purchase-payments/{po.Id}/actual-amount",
            new { amount = 10_000_000, rowVer = await RowVerAsync(accountant, po.Id) });
        await SeedPaymentAsync(accountant, po.Id, 4_000_000);
        var dlg = await OpenPoAsync(accountant, po.Id, "/purchase-payments");
        await Button(dlg, "Sửa giá trị thực tế").ClickAsync();
        var modal = Modal(accountant, "Sửa giá trị thực tế");
        var amount = modal.GetByLabel("Giá trị thực tế");
        var reason = modal.GetByLabel("Lý do sửa");
        await reason.FillAsync("ST_AUTO_ ly do sua");
        await TypeAsync(amount, "3999999");
        await Button(modal, "Lưu").ClickAsync();
        await SoftAsync("giá trị nhỏ hơn đã thanh toán không báo lỗi",
            () => Expect(modal.GetByText("Giá trị thực tế không được nhỏ hơn số đã thanh toán.")).ToBeVisibleAsync());
        await CloseConfirmAsync(accountant);
        await TypeAsync(amount, "4000000");
        await reason.FillAsync("");
        await Button(modal, "Lưu").ClickAsync();
        await SoftAsync("thiếu lý do sửa không báo lỗi", () => Expect(modal.GetByText("Vui lòng nhập lý do sửa.")).ToBeVisibleAsync());
        await CloseConfirmAsync(accountant);
        await reason.FillAsync("ST_AUTO_ ly do sua gia tri");
        await Button(modal, "Lưu").ClickAsync();
        var resp = await accountant.RunAndWaitForResponseAsync(() => Button(ConfirmBox(accountant), "Xác nhận").ClickAsync(),
            r => r.Url.EndsWith("/actual-amount"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await accountant.ReloadAsync();
        var detail = await PaymentDetailAsync(accountant, po.Id);
        var status = detail.GetProperty("paymentStatus").GetString();
        await SoftAsync($"sau khi giá trị thực tế bằng số đã trả, trạng thái là '{status}' chứ không phải Đã thanh toán", async () =>
        {
            status.Should().Be("Paid");
            await Expect(Detail(accountant)).ToContainTextAsync("Đã thanh toán");
            await Expect(Button(Detail(accountant), Pay)).ToHaveCountAsync(0);
        });
        SoftVerify();
    }

    // ---------------------------------------------------------------- Trả lại / sửa giá

    [Fact]
    public async Task ST_M5_014()
    {
        CaseId = "ST_M5_014";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        var calls = new List<string>();
        var dlg = await OpenPoAsync(accountant, po.Id, "/purchase-payments");
        await Button(dlg, "Trả lại Quản đốc").ClickAsync();
        var box = accountant.GetByRole(AriaRole.Dialog).Last;
        var reason = "ST_AUTO_ don gia sai " + Next();
        await box.GetByRole(AriaRole.Textbox).FillAsync(reason);
        var returned = await accountant.RunAndWaitForResponseAsync(() => Button(box, "Xác nhận").ClickAsync(),
            r => r.Url.EndsWith("/return") && r.Request.Method == "POST");
        returned.Status.Should().BeInRange(200, 299, await returned.TextAsync());
        var row = await ListRowAsync(accountant, "/purchase-payments", po.Code);
        await SoftAsync("đơn đã trả lại vẫn ở trạng thái chờ thanh toán phía Kế toán", async () =>
        {
            if (await row.CountAsync() > 0) await Expect(row).ToContainTextAsync("Bị trả lại");
            (await PaymentDetailAsync(accountant, po.Id)).GetProperty("permissions").GetProperty("canConfirmPayment").GetBoolean().Should().BeFalse();
        });
        var manager = await AsAsync("Manager");
        var detail = await OpenPoAsync(manager, po.Id, "/purchase-orders");
        await Expect(detail).ToContainTextAsync(reason);
        await Button(detail, "Sửa đơn giá và gửi lại").ClickAsync();
        var modal = Modal(manager, "Sửa đơn giá và gửi lại Kế toán");
        await Expect(modal).ToContainTextAsync("Lý do Kế toán trả lại: " + reason);
        await TypeAsync(modal.GetByRole(AriaRole.Spinbutton).First, "110000");
        var repriced = await manager.RunAndWaitForResponseAsync(() => Button(modal, "Sửa đơn giá và gửi lại").ClickAsync(),
            r => r.Url.EndsWith("/reprice") && r.Request.Method == "POST");
        repriced.Status.Should().BeInRange(200, 299, await repriced.TextAsync());
        row = await ListRowAsync(accountant, "/purchase-payments", po.Code);
        await Expect(row).ToContainTextAsync("Chưa thanh toán");
        await Expect(row).ToContainTextAsync("11.000.000");
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_015()
    {
        CaseId = "ST_M5_015";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        await ApiAsync(accountant, "POST", $"/accountant/purchase-payments/{po.Id}/return",
            new { reason = "ST_AUTO_ tra lai", rowVer = await RowVerAsync(accountant, po.Id) });
        var manager = await AsAsync("Manager");
        var posts = 0;
        manager.Request += (_, r) => { if (r.Url.EndsWith("/reprice") && r.Method == "POST") posts++; };
        var detail = await OpenPoAsync(manager, po.Id, "/purchase-orders");
        await Button(detail, "Sửa đơn giá và gửi lại").ClickAsync();
        var modal = Modal(manager, "Sửa đơn giá và gửi lại Kế toán");
        var price = modal.GetByRole(AriaRole.Spinbutton).First;
        await TypeAsync(price, "0,01");
        var tiny = await price.InputValueAsync();
        await SoftAsync($"đơn giá 0,01 không nhập được: ô tự đổi thành '{tiny}'", () => { tiny.Should().Be("0,01"); return Task.CompletedTask; });
        await TypeAsync(price, "0");
        await Button(modal, "Sửa đơn giá và gửi lại").ClickAsync();
        await SoftAsync("đơn giá 0 không báo lỗi", () => Expect(modal.GetByText("Nhập đơn giá lớn hơn 0.")).ToBeVisibleAsync());
        await SoftAsync("đơn giá 0 nhưng vẫn gửi API", () => { posts.Should().Be(0); return Task.CompletedTask; });
        var note = modal.GetByLabel("Ghi chú");
        await note.FillAsync(new string('n', 1001));
        var length = (await note.InputValueAsync()).Length;
        await SoftAsync("ghi chú 1001 ký tự không bị chặn", () => { length.Should().Be(1000); return Task.CompletedTask; });
        await note.FillAsync("ST_AUTO_ gia moi");
        await TypeAsync(price, "90000");
        var resp = await manager.RunAndWaitForResponseAsync(() => Button(modal, "Sửa đơn giá và gửi lại").ClickAsync(),
            r => r.Url.EndsWith("/reprice"));
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        var after = await PaymentDetailAsync(accountant, po.Id);
        after.GetProperty("supplierId").GetInt32().Should().Be(po.SupplierId);
        after.GetProperty("lines")[0].GetProperty("orderedQty").GetDecimal().Should().Be(100);
        after.GetProperty("lines")[0].GetProperty("unitPrice").GetDecimal().Should().Be(90000);
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_016()
    {
        CaseId = "ST_M5_016";
        var po = await SeedPoAsync();
        var accountant = await AsAsync("Accountant");
        await SeedPaymentAsync(accountant, po.Id, 4_000_000);
        var puts = 0;
        accountant.Request += (_, r) => { if (r.Url.Contains("/payments/") && r.Method == "PUT") puts++; };
        var dlg = await OpenPoAsync(accountant, po.Id, "/purchase-payments");
        await Button(dlg, "Sửa lần thanh toán").ClickAsync();
        var modal = Modal(accountant, "Sửa lần thanh toán");
        await TypeAsync(modal.GetByLabel("Số tiền thanh toán"), "5000000");
        await modal.GetByLabel("Lý do sửa").FillAsync("    ");
        await Button(modal, "Lưu").ClickAsync();
        await SoftAsync("lý do chỉ có khoảng trắng không báo lỗi required",
            () => Expect(modal.GetByText("Vui lòng nhập lý do sửa.")).ToBeVisibleAsync());
        await CloseConfirmAsync(accountant);
        await SoftAsync("lý do trắng nhưng vẫn gửi API", () => { puts.Should().Be(0); return Task.CompletedTask; });
        var reason = "ST_AUTO_ nhap nham so tien " + Next();
        await modal.GetByLabel("Lý do sửa").FillAsync(reason);
        await Button(modal, "Lưu").ClickAsync();
        var resp = await accountant.RunAndWaitForResponseAsync(() => Button(ConfirmBox(accountant), "Xác nhận").ClickAsync(),
            r => r.Url.Contains("/payments/") && r.Request.Method == "PUT");
        resp.Status.Should().BeInRange(200, 299, await resp.TextAsync());
        await accountant.ReloadAsync();
        await Expect(Detail(accountant)).ToContainTextAsync("Đã sửa");
        await Expect(Detail(accountant)).ToContainTextAsync(reason);
        await Expect(Detail(accountant)).ToContainTextAsync("5.000.000");

        var old = await PaymentDetailAsync(accountant, OldPaymentOrderId);
        var payment = old.GetProperty("payments")[0];
        var oldDialog = await OpenPoAsync(accountant, OldPaymentOrderId, "/purchase-payments");
        await Expect(Button(oldDialog, "Sửa lần thanh toán")).ToHaveCountAsync(0);
        // Giao diện ẩn nút nên kiểm lỗi BE bằng API công khai, gửi nguyên giá trị cũ
        var (status, _, text) = await CallAsync(accountant, "PUT",
            $"/accountant/purchase-payments/{OldPaymentOrderId}/payments/{payment.GetProperty("purchasePaymentId").GetInt32()}", new
            {
                paymentDate = payment.GetProperty("paymentDate").GetString()![..10],
                amount = payment.GetProperty("amount").GetDecimal(),
                paymentMethod = payment.GetProperty("paymentMethod").GetString(),
                paymentNote = payment.GetProperty("paymentNote").GetString(),
                reason = "ST_AUTO_ thu sua lan cu",
                rowVer = old.GetProperty("rowVer").GetString(),
            });
        status.Should().BeInRange(400, 499, text);
        Regex.IsMatch(text, "\"message\":\"[^\"]+").Should().BeTrue();
        (await PaymentDetailAsync(accountant, OldPaymentOrderId)).GetProperty("payments")[0].GetRawText()
            .Should().Be(payment.GetRawText());
        SoftVerify();
    }

    [Fact]
    public async Task ST_M5_017()
    {
        CaseId = "ST_M5_017";
        var po = await SeedPoAsync();
        var tab1 = await AsAsync("Accountant");
        await SeedPaymentAsync(tab1, po.Id, 4_000_000);
        await UploadAsync(tab1, "PurchaseOrder", po.Id, "PaymentProof");
        var tab2 = await AsAsync("Accountant");
        var first = await OpenPayAsync(tab1, po.Id);
        var second = await OpenPayAsync(tab2, po.Id);
        foreach (var modal in new[] { first, second })
            await Expect(modal.GetByLabel("Số tiền thanh toán")).ToHaveValueAsync("6.000.000");
        (await SubmitPayAsync(tab1, first)).Status.Should().BeInRange(200, 299);
        var resp = await SubmitPayAsync(tab2, second);
        resp.Status.Should().BeInRange(400, 499, await resp.TextAsync());
        await Expect(Errors(tab2).First).ToBeVisibleAsync();
        await Expect(second.Or(Detail(tab2)).First).ToBeVisibleAsync();
        var detail = await PaymentDetailAsync(tab1, po.Id);
        detail.GetProperty("paidAmount").GetDecimal().Should().Be(10_000_000);
        detail.GetProperty("payments").GetArrayLength().Should().Be(2);
    }

    // ---------------------------------------------------------------- Phân quyền

    [Fact]
    public async Task ST_M5_018()
    {
        CaseId = "ST_M5_018";
        var admin = await AsAsync("Admin");
        var manager = await AsAsync("Manager");
        var id = await PendingOrderIdAsync(manager);
        var errors = new List<string>();
        admin.PageError += (_, e) => errors.Add(e);
        await admin.GotoAsync($"/purchase-orders?id={id}");
        await Expect(admin.GetByRole(AriaRole.Main)).ToContainTextAsync("Đơn mua");
        var dlg = Detail(admin);
        await Expect(dlg.GetByText("Lịch sử thay đổi").Or(Errors(admin).First).First).ToBeVisibleAsync();
        var place = Button(dlg, Place);
        var canPlace = await place.CountAsync() > 0;
        var (apiStatus, _, apiText) = await CallAsync(admin, "GET", $"/manager/purchase-order-tracking/{id}");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "m5_018_observed.txt"),
            $"Admin thấy nút đặt hàng: {canPlace}; GET chi tiết HTTP {apiStatus}; {Regex.Match(apiText, "\"permissions\":\\{[^}]*\\}").Value}");
        if (canPlace)
        {
            await place.ClickAsync();
            await Expect(Modal(admin, "Chọn nhà cung cấp và đặt hàng")).ToBeVisibleAsync();
        }
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ST_M5_019()
    {
        CaseId = "ST_M5_019";
        var forbidden = new List<string>();
        var plan = new (string Role, string[] Paths)[]
        {
            ("Accountant", new[] { "/suppliers", "/purchase-orders" }),
            ("Manager", new[] { "/purchase-payments" }),
            ("Worker", new[] { "/suppliers", "/purchase-orders", "/purchase-payments" }),
        };
        foreach (var (role, paths) in plan)
        {
            var page = await AsAsync(role);
            await GotoAndWaitAsync(page, "/");
            var pattern = role switch
            {
                "Accountant" => "/api/v1/(admin/suppliers|manager/purchase-order-tracking)",
                "Manager" => "/api/v1/accountant/purchase-payments",
                _ => "/api/v1/(admin/suppliers|manager/purchase-order-tracking|accountant/purchase-payments)",
            };
            page.Request += (_, r) => { if (Regex.IsMatch(r.Url, pattern)) forbidden.Add($"{role} {r.Url}"); };
            foreach (var path in paths)
            {
                await page.GotoAsync(path);
                await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/");
            }
        }
        forbidden.Should().BeEmpty();
        var manager = await AsAsync("Manager");
        (await CallAsync(manager, "GET", "/accountant/purchase-payments?page=1&pageSize=10")).Status.Should().Be(403);

        var (user, pass) = GetAccount("Accountant");
        var accountant = await NewAnonymousPageAsync();
        await LoginUiAsync(accountant, user, pass);
        InvalidateRole("Accountant");
        await accountant.GotoAsync("/purchase-payments");
        await Expect(accountant.GetByText(new Regex("DM-\\d{8}-\\d{3}")).First).ToBeVisibleAsync();
        await accountant.Locator("button.app-user").ClickAsync();
        await accountant.GetByRole(AriaRole.Menuitem, new() { Name = "Đăng xuất" }).ClickAsync();
        await accountant.WaitForURLAsync(u => u.Contains("/login"));
        await accountant.GoBackAsync();
        await accountant.WaitForURLAsync(u => u.Contains("/login"));
        await Expect(accountant.GetByText(new Regex("DM-\\d{8}-\\d{3}"))).ToHaveCountAsync(0);
    }
}
