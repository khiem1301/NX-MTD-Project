using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>Kết quả gọi HTTP dạng nhị phân (file ảnh / Excel): status, Content-Type, Cache-Control và byte thô.</summary>
public sealed record RawResponse(int Code, string ContentType, string CacheControl, byte[] Bytes)
{
    public string Text => Encoding.UTF8.GetString(Bytes);
}

/// <summary>
/// Nền chung cho IT_B2 (Master Catalog): kế thừa B1TestBase (BaseIntegrationTest + token/HTTP helper), seed S0 của Batch 1
/// (tài khoản các vai trò) rồi seed S0 của Batch 2 (<see cref="B2Context"/>). Mỗi test chạy trong ambient transaction và Rollback.
/// </summary>
public abstract class B2TestBase : B1TestBase
{
    protected const string AC = "/api/v1/accountant/";
    protected const string WK = "/api/v1/warehouse-keeper/";

    private TestDataSeeder _seeder = null!;
    protected B2Context B { get; private set; } = null!;

    protected B2TestBase(ITestOutputHelper output) : base(output) { }

    protected override Task<TestContext> SeedBaseAsync(TestDataSeeder seeder) => seeder.SeedBatch2BaseAsync();

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        _seeder = seeder;
        B = await seeder.SeedB2Async();
    }

    protected Task<CustomerOrder> SeedOd2Async() => _seeder.SeedB2OrderForPr2Async();

    protected Task<VehicleTrip> SeedVe4SecondOpenTripAsync() => _seeder.SeedB2SecondOpenTripForVe4Async();

    // ------------------------------------------------------------------ token (TK_*)

    protected Task<string> TkAdm() => TokenAsync(S0.Adm);
    protected Task<string> TkMgr() => TokenAsync(S0.Mgr);
    protected Task<string> TkAcc() => TokenAsync(S0.Acc);
    protected Task<string> TkWk() => TokenAsync(S0.Wh);
    protected Task<string> TkTl() => TokenAsync(S0.Tl1);
    protected Task<string> TkWrk() => TokenAsync(S0.W1);

    /// <summary>TK_QC: S0 chỉ có QA/QC dùng mật khẩu tạm nên dựng JWT hợp lệ ký đúng khóa hệ thống (role QaQc).</summary>
    protected Task<string> TkQc() => CraftJwtAsync(S0.Tmp, "access", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1));

    /// <summary>TK_EXP: token đúng chữ ký nhưng đã hết hạn.</summary>
    protected Task<string> TkExpired() => CraftJwtAsync(S0.Adm, "access", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

    /// <summary>TK_FORGED: sửa claim role thành Admin, giữ chữ ký cũ.</summary>
    protected async Task<string> TkForged() => ForgeRole(await TokenAsync(S0.Mgr), "Admin");

    // ------------------------------------------------------------------ HTTP bổ sung

    protected Task<Api> PatchAsync(string url, string? token = null, object? body = null) => SendAsync(HttpMethod.Patch, url, token, body);

    protected async Task<Api> UploadAsync(string url, string? token, byte[]? content, string fileName, string contentType, string partName = "file")
    {
        using var form = new MultipartFormDataContent();
        if (content is not null)
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, partName, fileName);
        }
        else
        {
            form.Add(new StringContent("none"), "unused");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonElement root = default;
        var isJson = false;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { root = JsonDocument.Parse(text).RootElement.Clone(); isJson = true; }
            catch (JsonException) { /* 413 / body không phải JSON */ }
        }

        return new Api { Status = res.StatusCode, Body = text, Root = root, IsJson = isJson, CacheControl = res.Headers.CacheControl?.ToString() ?? "" };
    }

    protected Task<Api> UploadXlsxAsync(string url, string? token, byte[]? bytes, string fileName = "spec.xlsx") =>
        UploadAsync(url, token, bytes, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    protected async Task<RawResponse> RawGetAsync(string url, string? token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Client.SendAsync(req);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return new RawResponse((int)res.StatusCode, res.Content.Headers.ContentType?.MediaType ?? "", res.Headers.CacheControl?.ToString() ?? "", bytes);
    }

    // ------------------------------------------------------------------ file mẫu

    private static readonly byte[] TinyJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

    /// <summary>JPEG hợp lệ (SOI + đoạn COM đệm + phần còn lại) đúng <paramref name="size"/> byte.</summary>
    protected static byte[] Jpeg(int size)
    {
        var rest = TinyJpeg[2..];
        var padding = size - 2 - rest.Length;
        padding.Should().BeGreaterThanOrEqualTo(0);
        using var ms = new MemoryStream(size);
        ms.Write(TinyJpeg, 0, 2);
        while (padding >= 4)
        {
            var len = Math.Min(padding - 2, 65_535);
            ms.WriteByte(0xFF); ms.WriteByte(0xFE);
            ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)(len & 0xFF));
            ms.Write(new byte[len - 2]);
            padding -= 2 + len;
        }

        ms.Write(rest);
        if (padding > 0) ms.Write(new byte[padding]);
        return ms.ToArray();
    }

    protected static byte[] FakeExe() => Encoding.ASCII.GetBytes("MZ\u0090\u0000\u0003\u0000\u0000\u0000\u0004\u0000 This program cannot be run in DOS mode.").Concat(new byte[2048]).ToArray();
    protected static byte[] NoteTxt() => Encoding.UTF8.GetBytes("ghi chú không phải ảnh");
    protected static byte[] PdfBytes() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF").Concat(new byte[512]).ToArray();
    protected static byte[] CorruptXlsx() => new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xDE, 0xAD, 0xBE, 0xEF }.Concat(new byte[256]).ToArray();

    /// <summary>Tải file mẫu nhập Excel do chính hệ thống sinh (đã có dữ liệu thật của mã hàng ⇒ hợp lệ cho mã hàng đó).</summary>
    protected async Task<byte[]> TemplateAsync(string kind, int? productId, string token, int? orderId = null)
    {
        var url = C + "import-templates/" + kind + "?" + (productId is null ? "" : $"productId={productId}&") + (orderId is null ? "" : $"orderId={orderId}");
        var res = await RawGetAsync(url.TrimEnd('?', '&'), token);
        res.Code.Should().Be(200, $"seed S0: tải file mẫu '{kind}' phải thành công. {res.Text}");
        return res.Bytes;
    }

    /// <summary>
    /// Mở file mẫu định mức của PR1 do hệ thống sinh, bỏ các dòng ví dụ (bảng chưa có dữ liệu thì mẫu ghi dòng ví dụ chữ xám) ở "Nhóm bìa" và
    /// "Bìa của đơn", rồi đổi tên thành phần "Túi" thành "Túi mới" để file có đúng một dòng hợp lệ mới (CanImport = true).
    /// <paramref name="breakType"/> = sửa loại thành phần của "Thân trước" thành giá trị sai (dòng có ParseError).
    /// </summary>
    private async Task<byte[]> SpecWorkbookAsync(bool breakType)
    {
        using var input = new MemoryStream(await TemplateAsync("spec", B.Pr1.ProductId, await TkAdm()));
        using var wb = new XLWorkbook(input);
        foreach (var ws in wb.Worksheets)
        {
            var name = ws.Name.Normalize();
            if (!name.StartsWith("Nhóm bìa".Normalize()) && !name.StartsWith("Bìa của đơn".Normalize())) continue;
            for (var row = ws.LastRowUsed()?.RowNumber() ?? 0; row >= 4; row--) ws.Row(row).Delete();
        }

        var renamed = false;
        foreach (var ws in wb.Worksheets)
        {
            var pocket = ws.CellsUsed().FirstOrDefault(c => c.GetString() == "Túi");
            if (pocket is null) continue;
            pocket.Value = "Túi mới";
            renamed = true;
            break;
        }

        renamed.Should().BeTrue("seed S0: file mẫu phải có dòng thành phần \"Túi\"");
        if (breakType)
        {
            var broken = false;
            foreach (var ws in wb.Worksheets)
            {
                var typeCell = ws.CellsUsed().FirstOrDefault(c => c.GetString() == "PANEL"
                    && ws.Row(c.Address.RowNumber).CellsUsed().Any(x => x.GetString() == "Thân trước"));
                if (typeCell is null) continue;
                typeCell.Value = "PANEL_SAI";
                broken = true;
                break;
            }

            broken.Should().BeTrue("seed S0: file mẫu phải có dòng \"Thân trước\" kèm loại PANEL để dựng spec_err.xlsx");
        }

        using var output = new MemoryStream();
        wb.SaveAs(output);
        return output.ToArray();
    }

    /// <summary>spec_ok.xlsx — hợp lệ cho PR1: các dòng hiện có giữ nguyên (Unchanged) + thành phần "Túi mới" (New).</summary>
    protected Task<byte[]> SpecOkAsync() => SpecWorkbookAsync(breakType: false);

    /// <summary>spec_err.xlsx — một dòng lỗi ("Thân trước" sai loại ⇒ ParseError) lẫn một dòng hợp lệ mới ("Túi mới").</summary>
    protected Task<byte[]> SpecErrAsync() => SpecWorkbookAsync(breakType: true);

    // ------------------------------------------------------------------ id không tồn tại (tính động)

    protected async Task<int> MissingIdAsync()
    {
        await using var db = NewDb();
        var max = new[]
        {
            await db.Customers.AsNoTracking().MaxAsync(x => x.CustomerId),
            await db.Suppliers.AsNoTracking().MaxAsync(x => x.SupplierId),
            await db.Materials.AsNoTracking().MaxAsync(x => x.MaterialId),
            await db.Products.AsNoTracking().MaxAsync(x => x.ProductId),
            await db.Vehicles.AsNoTracking().MaxAsync(x => x.VehicleId),
            await db.CustomerOrders.AsNoTracking().MaxAsync(x => x.OrderId),
        }.Max();
        return max + 1_000_000;
    }

    // ------------------------------------------------------------------ snapshot DB

    /// <summary>Ảnh chụp mọi cột nghiệp vụ của các bảng danh mục, spec, đơn / PO / chuyến xe liên quan và id AuditLogs.</summary>
    protected async Task<string> SnapshotB2Async()
    {
        await using var db = NewDb();
        var customers = await db.Customers.AsNoTracking().OrderBy(x => x.CustomerId)
            .Select(x => new { x.CustomerId, x.CustomerName, x.Phone, x.Email, x.DefaultAddress, x.Status, x.DiscontinueReason }).ToListAsync();
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(x => x.SupplierId)
            .Select(x => new { x.SupplierId, x.SupplierName, x.Phone, x.Email, x.Address, x.MaterialTypes, x.Note, x.Status, x.DiscontinueReason, x.CreatedAt }).ToListAsync();
        var materials = await db.Materials.AsNoTracking().OrderBy(x => x.MaterialId)
            .Select(x => new { x.MaterialId, x.MaterialCode, x.MaterialName, x.Color, x.MaterialType, x.Unit, x.WidthM, x.Status, x.CreatedAt }).ToListAsync();
        var products = await db.Products.AsNoTracking().OrderBy(x => x.ProductId)
            .Select(x => new { x.ProductId, x.ProductCode, x.SampleImageUrl, x.Description, x.Status, x.CreatedAt }).ToListAsync();
        var images = await db.ProductImages.AsNoTracking().OrderBy(x => x.ProductImageId)
            .Select(x => new { x.ProductImageId, x.ProductId, x.ImageKey, x.SortOrder }).ToListAsync();
        var colors = await db.ProductColors.AsNoTracking().OrderBy(x => x.ProductColorId)
            .Select(x => new { x.ProductColorId, x.ProductId, x.Color }).ToListAsync();
        var variants = await db.ProductVariants.AsNoTracking().OrderBy(x => x.ProductVariantId)
            .Select(x => new { x.ProductVariantId, x.ProductId, x.ProductColorId, x.SizeCode, x.Status }).ToListAsync();
        var components = await db.ProductComponents.AsNoTracking().OrderBy(x => x.ProductComponentId)
            .Select(x => new { x.ProductComponentId, x.ProductId, x.ComponentName, x.ComponentType, x.PiecesPerGarment, x.Status }).ToListAsync();
        var norms = await db.ProductComponentMaterials.AsNoTracking().OrderBy(x => x.ProductComponentMaterialId)
            .Select(x => new { x.ProductComponentMaterialId, x.ProductId, x.ProductColorId, x.ProductComponentId, x.MaterialId, x.SpecNote, x.Status }).ToListAsync();
        var markerComponents = await db.ProductMarkerComponents.AsNoTracking().OrderBy(x => x.ProductId).ThenBy(x => x.MarkerNo).ThenBy(x => x.ProductComponentId)
            .Select(x => new { x.ProductId, x.MarkerNo, x.ProductComponentId }).ToListAsync();
        var markerFabrics = await db.ProductMarkerFabrics.AsNoTracking().OrderBy(x => x.ProductMarkerFabricId)
            .Select(x => new { x.ProductMarkerFabricId, x.ProductId, x.MarkerNo, x.ProductColorId, x.MaterialId }).ToListAsync();
        var markers = await db.ProductMarkers.AsNoTracking().OrderBy(x => x.ProductMarkerId)
            .Select(x => new { x.ProductMarkerId, x.OrderId, x.ProductId, x.MarkerNo, x.MarkerCode, x.LengthM, x.WidthM, x.Status }).ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking().OrderBy(x => x.VehicleId)
            .Select(x => new { x.VehicleId, x.PlateNumber, x.VehicleType, x.FuelNormPer100Km, x.Status }).ToListAsync();
        var trips = await db.VehicleTrips.AsNoTracking().OrderBy(x => x.VehicleTripId)
            .Select(x => new { x.VehicleTripId, x.VehicleId, x.Status, x.OdometerStart, x.OdometerEnd }).ToListAsync();
        var orders = await db.CustomerOrders.AsNoTracking().OrderBy(x => x.OrderId)
            .Select(x => new { x.OrderId, x.OrderCode, x.CustomerId, x.Status }).ToListAsync();
        var lines = await db.OrderLines.AsNoTracking().OrderBy(x => x.OrderLineId)
            .Select(x => new { x.OrderLineId, x.OrderId, x.ProductId, x.ProductVariantId, x.OrderedQty }).ToListAsync();
        var orderMaterials = await db.OrderMaterials.AsNoTracking().OrderBy(x => x.OrderMaterialId)
            .Select(x => new { x.OrderMaterialId, x.OrderId, x.MaterialId, x.RequiredQty, x.CustomerQty, x.PurchaseQty }).ToListAsync();
        var stocks = await db.StockBalances.AsNoTracking().OrderBy(x => x.StockBalanceId)
            .Select(x => new { x.StockBalanceId, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId, x.OnHandQty }).ToListAsync();
        var purchaseOrders = await db.PurchaseOrders.AsNoTracking().OrderBy(x => x.PurchaseOrderId)
            .Select(x => new { x.PurchaseOrderId, x.PoCode, x.SupplierId, x.OrderId, x.GoodsStatus }).ToListAsync();
        var audits = await db.AuditLogs.AsNoTracking().OrderBy(x => x.AuditLogId).Select(x => x.AuditLogId).ToListAsync();
        return JsonSerializer.Serialize(new
        {
            customers, suppliers, materials, products, images, colors, variants, components, norms, markerComponents, markerFabrics,
            markers, vehicles, trips, orders, lines, orderMaterials, stocks, purchaseOrders, audits,
        });
    }

    protected async Task AssertB2UnchangedAsync(string before, string because = "")
    {
        var after = await SnapshotB2Async();
        (after == before).Should().BeTrue($"[DB] không được thay đổi. {because}");
    }

    // ------------------------------------------------------------------ truy vấn DB nhanh

    protected async Task<Customer?> CustomerAsync(int id)
    {
        await using var db = NewDb();
        return await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == id);
    }

    protected async Task<Supplier?> SupplierAsync(int id)
    {
        await using var db = NewDb();
        return await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.SupplierId == id);
    }

    protected async Task<Material?> MaterialAsync(int id)
    {
        await using var db = NewDb();
        return await db.Materials.AsNoTracking().SingleOrDefaultAsync(x => x.MaterialId == id);
    }

    protected async Task<Product?> ProductAsync(int id)
    {
        await using var db = NewDb();
        return await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == id);
    }

    protected async Task<Vehicle?> VehicleAsync(int id)
    {
        await using var db = NewDb();
        return await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.VehicleId == id);
    }

    protected async Task<List<ProductImage>> ImagesAsync(int productId)
    {
        await using var db = NewDb();
        return await db.ProductImages.AsNoTracking().Where(x => x.ProductId == productId).OrderBy(x => x.SortOrder).ToListAsync();
    }

    protected async Task<int> CountCustomersAsync(string name)
    {
        await using var db = NewDb();
        return await db.Customers.AsNoTracking().CountAsync(x => x.CustomerName == name);
    }

    protected async Task<int> CountAllAsync(string table)
    {
        await using var db = NewDb();
        return table switch
        {
            "Customer" => await db.Customers.AsNoTracking().CountAsync(),
            "Supplier" => await db.Suppliers.AsNoTracking().CountAsync(),
            "Material" => await db.Materials.AsNoTracking().CountAsync(),
            "Product" => await db.Products.AsNoTracking().CountAsync(),
            "ProductImage" => await db.ProductImages.AsNoTracking().CountAsync(),
            "Vehicle" => await db.Vehicles.AsNoTracking().CountAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
    }

    // ------------------------------------------------------------------ body mẫu

    /// <summary>Giá trị "spec không nêu": helper tự điền giá trị hợp lệ và duy nhất để request vượt qua validate các trường không liên quan tới ca test.</summary>
    protected const string Auto = "auto";

    /// <summary>Ngẫu nhiên theo lần chạy để không trùng với dữ liệu còn sót trong Test DB (khóa UNIQUE dùng chung Customer / Supplier).</summary>
    protected static string AutoPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    protected static string AutoEmail() => $"auto{Guid.NewGuid():N}"[..20] + "@it-b2.vn";

    protected static object CustomerBody(string name, string? phone = Auto, string? email = Auto, string? address = Auto) =>
        new
        {
            CustomerName = name,
            Phone = phone == Auto ? AutoPhone() : phone,
            Email = email == Auto ? AutoEmail() : email,
            DefaultAddress = address == Auto ? "Địa chỉ IT B2" : address,
        };

    protected static object SupplierBody(string name, string? phone = Auto, string? email = Auto, string? address = Auto,
        string? materialTypes = Auto, string? note = Auto) =>
        new
        {
            SupplierName = name,
            Phone = phone == Auto ? AutoPhone() : phone,
            Email = email == Auto ? AutoEmail() : email,
            Address = address == Auto ? "Địa chỉ NCC IT B2" : address,
            MaterialTypes = materialTypes == Auto ? "Vải" : materialTypes,
            Note = note == Auto ? null : note,
        };

    protected static object MaterialBody(string? code, string name, string type, string unit, string? color = null, decimal? width = null) =>
        new { MaterialCode = code, MaterialName = name, MaterialType = type, Unit = unit, Color = color, WidthM = width };

    protected static object UpdateMaterialBody(string name, string type, string unit, string? color = null, decimal? width = null) =>
        new { MaterialName = name, MaterialType = type, Unit = unit, Color = color, WidthM = width };

    protected static object ProductBody(string code, string? sample = null, string? description = null, IReadOnlyList<string>? extras = null) =>
        new { ProductCode = code, SampleImageUrl = sample, Description = description, ExtraImages = extras };

    protected static object VehicleBody(string plate, string type, decimal fuel) =>
        new { PlateNumber = plate, VehicleType = type, FuelNormPer100Km = fuel };

    /// <summary>Upload một ảnh hợp lệ qua P7, trả về khóa ảnh.</summary>
    protected async Task<string> UploadKeyAsync(string token, int size = 4096)
    {
        var res = await UploadAsync(A + "products/sample-image", token, Jpeg(size), "ảnh.jpg", "image/jpeg");
        res.Code.Should().Be(200, $"seed S0: upload ảnh mẫu phải thành công. {res}");
        var data = res.Data;
        var key = data.ValueKind == JsonValueKind.String ? data.GetString() : B1Json.Str(data, "key") ?? B1Json.Str(data, "imageKey") ?? B1Json.Str(data, "sampleImageUrl");
        key.Should().NotBeNullOrWhiteSpace($"khóa ảnh phải có trong data: {res.Body}");
        return key!;
    }

    // ------------------------------------------------------------------ assert dùng chung

    /// <summary>Không 5xx, không lộ chi tiết SQL / constraint trong body (mục 0 của tài liệu test).</summary>
    protected static void AssertNoSqlLeak(Api r)
    {
        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny("UQ_", "FK_", "SqlException", "Cannot insert duplicate", "duplicate key", "IX_", "StackTrace", "   at ");
    }

    protected static void AssertCode(Api r, params int[] codes) => r.Code.Should().BeOneOf(codes, r.ToString());

    protected static void AssertNotFoundOrNoRoute(Api r) => r.Code.Should().BeOneOf(new[] { 404, 405 }, r.ToString());

    /// <summary>CreatedAt ≈ thời điểm gọi theo giờ VN (fn_VnNow hoặc đồng hồ cố định của test), không phải 0001-01-01 hay giờ UTC.</summary>
    protected void AssertCreatedNow(DateTime createdAt)
    {
        createdAt.Year.Should().BeGreaterThan(2000, "CreatedAt không được là giá trị mặc định 0001-01-01");
        var nearReal = Math.Abs((createdAt - VnNow()).TotalMinutes) <= 10;
        var nearFixed = Math.Abs((createdAt - Clock.Now).TotalMinutes) <= 10;
        (nearReal || nearFixed).Should().BeTrue($"CreatedAt {createdAt:O} phải ≈ giờ VN {VnNow():O} hoặc đồng hồ test {Clock.Now:O}");
    }

    protected async Task AssertNoNewAuditAsync(long lastId, string because = "")
    {
        var rows = await AuditRowsAfterAsync(lastId);
        rows.Should().BeEmpty($"thao tác thất bại không được sinh nhật ký. {because}");
    }

    /// <summary>"AuditLogs +1 [?]": ghi lại hành vi thực tế (bảng / hành động), kiểm tra có đúng 1 dòng.</summary>
    protected async Task AssertAuditAddedAsync(long lastId, int expected = 1)
    {
        var rows = await AuditRowsAfterAsync(lastId);
        Note("Audit rows: " + string.Join("; ", rows.Select(a => $"{a.TableName}#{a.RecordId} {a.Action}")));
        rows.Should().HaveCount(expected, $"AuditLogs +{expected}");
    }

    protected static string Viet(int length)
    {
        var sb = new StringBuilder();
        while (sb.Length < length + 8) sb.Append("Việt Nam ");
        var s = sb.ToString(0, length).TrimEnd();
        while (s.Length < length) s += 'ệ';
        return s;
    }

    protected static string PhoneOf(int length) => "09" + new string('1', length - 2);

    protected static string EmailOf(int length)
    {
        const string suffix = "@mail.vn";
        return new string('a', length - suffix.Length) + suffix;
    }

    protected static string Statuses(IEnumerable<string?> values) => string.Join(",", values.Distinct().OrderBy(x => x));

    protected static List<JsonElement> ItemsOf(Api r) => r.Items();

    protected static HashSet<int> Ids(Api r, string name) => IdSet(r, name);
}
