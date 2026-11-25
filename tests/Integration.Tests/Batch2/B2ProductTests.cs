using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_107–139 — Product: thông tin chung, ảnh phụ (ProductImage không có CHECK), upload / đọc ảnh mẫu. Ca đồng thời 118, 123 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2ProductTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string Url => A + "products";

    private async Task<Product?> FindAsync(string code)
    {
        await using var db = NewDb();
        return await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductCode == code);
    }

    private async Task<string[]> UploadKeysAsync(string token, int count, int baseSize = 4096)
    {
        var keys = new List<string>();
        for (var i = 0; i < count; i++) keys.Add(await UploadKeyAsync(token, baseSize + i * 37));
        return keys.ToArray();
    }

    /// <summary>Ảnh chụp các bảng con của mã hàng (đơn, dòng đơn, biến thể, thành phần, định mức) để kiểm tra "không đổi".</summary>
    private async Task<string> ChildrenSnapshotAsync()
    {
        await using var db = NewDb();
        return JsonSerializer.Serialize(new
        {
            orders = await db.CustomerOrders.AsNoTracking().OrderBy(x => x.OrderId).Select(x => new { x.OrderId, x.CustomerId, x.Status }).ToListAsync(),
            variants = await db.ProductVariants.AsNoTracking().OrderBy(x => x.ProductVariantId).Select(x => new { x.ProductVariantId, x.Status }).ToListAsync(),
            components = await db.ProductComponents.AsNoTracking().OrderBy(x => x.ProductComponentId).Select(x => new { x.ProductComponentId, x.ComponentName, x.Status }).ToListAsync(),
            norms = await db.ProductComponentMaterials.AsNoTracking().OrderBy(x => x.ProductComponentMaterialId).Select(x => new { x.ProductComponentMaterialId, x.Status }).ToListAsync(),
        });
    }

    // ------------------------------------------------------------------ thông tin chung + ảnh phụ

    [Fact]
    public async Task IT_B2_107_Create_Product_With_Five_Extra_Images_Keeps_Order()
    {
        var token = await TkAdm();
        var keys = await UploadKeysAsync(token, 6);
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url, token, ProductBody("SP100", keys[0], "Áo sơ mi nam", keys[1..]));
        ShouldBeCreated(r);

        var product = (await FindAsync("SP100"))!;
        product.Status.Should().Be("ACTIVE");
        product.SampleImageUrl.Should().Be(keys[0]);
        var images = await ImagesAsync(product.ProductId);
        images.Should().HaveCount(5);
        images.Select(i => (int)i.SortOrder).Should().Equal(1, 2, 3, 4, 5);
        images.Select(i => i.ImageKey).Should().Equal(keys[1..], "đúng thứ tự gửi lên");
        images.Should().OnlyContain(i => i.ProductId == product.ProductId);
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_108_Create_Product_With_Six_Extra_Images_Is_Rejected_And_Rolled_Back()
    {
        var token = await TkAdm();
        var keys = await UploadKeysAsync(token, 6);
        var before = await SnapshotB2Async();
        var images = await CountAllAsync("ProductImage");
        var r = await PostAsync(Url, token, ProductBody("SP101", null, null, keys));
        r.Code.Should().Be(400, r.ToString());
        (await FindAsync("SP101")).Should().BeNull();
        (await CountAllAsync("ProductImage")).Should().Be(images, "không có ProductImage mồ côi");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_109_Empty_Or_Missing_Extra_Images_Create_No_Image_Rows()
    {
        var token = await TkAdm();
        var images = await CountAllAsync("ProductImage");
        ShouldBeCreated(await PostAsync(Url, token, ProductBody("SP102", null, null, Array.Empty<string>())));
        ShouldBeCreated(await PostAsync(Url, token, "{\"ProductCode\":\"SP103\"}"));
        foreach (var code in new[] { "SP102", "SP103" })
            (await ImagesAsync((await FindAsync(code))!.ProductId)).Should().BeEmpty(code);
        (await CountAllAsync("ProductImage")).Should().Be(images);
    }

    [Fact]
    public async Task IT_B2_110_Update_With_Null_Extra_Images_Keeps_Existing()
    {
        var r = await PutAsync(Url + "/" + B.Pr1.ProductId, await TkAdm(), ProductBody("SP001", null, "Áo sơ mi IT", null));
        r.Code.Should().Be(200, r.ToString());
        var images = await ImagesAsync(B.Pr1.ProductId);
        images.Select(i => (i.ImageKey, (int)i.SortOrder)).Should().Equal((B.Pr1Img1.ImageKey, 1), (B.Pr1Img2.ImageKey, 2));
    }

    [Fact]
    public async Task IT_B2_111_Update_With_Empty_Extra_Images_Removes_All()
    {
        var r = await PutAsync(Url + "/" + B.Pr1.ProductId, await TkAdm(), ProductBody("SP001", null, "Áo sơ mi IT", Array.Empty<string>()));
        r.Code.Should().Be(200, r.ToString());
        (await ImagesAsync(B.Pr1.ProductId)).Should().BeEmpty();
    }

    [Fact]
    public async Task IT_B2_112_Reordering_Extra_Images_Does_Not_Violate_Unique_Sort()
    {
        var token = await TkAdm();
        var keys = await UploadKeysAsync(token, 5);
        var r = await PutAsync(Url + "/" + B.Pr1.ProductId, token, ProductBody("SP001", null, "Áo sơ mi IT", new[] { keys[2], keys[0], keys[3] }));
        r.Code.Should().Be(200, r.ToString());
        AssertNoSqlLeak(r);
        var images = await ImagesAsync(B.Pr1.ProductId);
        images.Select(i => (int)i.SortOrder).Should().Equal(1, 2, 3);
        images.Select(i => i.ImageKey).Should().Equal(keys[2], keys[0], keys[3]);
    }

    [Fact]
    public async Task IT_B2_113_Update_With_Six_Extra_Images_Rolls_Back_Everything()
    {
        var token = await TkAdm();
        var keys = await UploadKeysAsync(token, 6);
        var before = await SnapshotB2Async();
        var r = await PutAsync(Url + "/" + B.Pr1.ProductId, token, ProductBody("SP001", null, "Mô tả mới không được lưu", keys));
        r.Code.Should().Be(400, r.ToString());
        (await ImagesAsync(B.Pr1.ProductId)).Select(i => i.ImageKey).Should().Equal(B.Pr1Img1.ImageKey, B.Pr1Img2.ImageKey);
        (await ProductAsync(B.Pr1.ProductId))!.Description.Should().Be("Áo sơ mi IT");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_114_Invalid_Sort_Order_Is_Rejected_By_Api_Not_Db()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var url = Url + "/" + B.Pr1.ProductId;
        var bodies = new object[]
        {
            new { ProductCode = "SP001", ExtraImages = new[] { new { ImageKey = B.Pr1Img1.ImageKey, SortOrder = 0 } } },
            new { ProductCode = "SP001", ExtraImages = new[] { new { ImageKey = B.Pr1Img1.ImageKey, SortOrder = 6 } } },
            new
            {
                ProductCode = "SP001",
                ExtraImages = new[] { new { ImageKey = B.Pr1Img1.ImageKey, SortOrder = 2 }, new { ImageKey = B.Pr1Img2.ImageKey, SortOrder = 2 } },
            },
        };
        foreach (var body in bodies)
        {
            var r = await PutAsync(url, token, body);
            r.Code.Should().Be(400, r.ToString());
            r.ShouldNotBeServerError();
        }

        await using var db = NewDb();
        (await db.ProductImages.AsNoTracking().AnyAsync(i => i.SortOrder < 1 || i.SortOrder > 5)).Should().BeFalse("không có SortOrder ngoài 1..5");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_115_Dirty_Image_Rows_Inserted_By_Sql_Do_Not_Break_Get_And_Put()
    {
        Db.ProductImages.AddRange(
            new ProductImage { ProductId = B.Pr2.ProductId, ImageKey = "it-b2/dirty-0.jpg", SortOrder = 0, CreatedAt = Clock.Now },
            new ProductImage { ProductId = B.Pr2.ProductId, ImageKey = "it-b2/dirty-6.jpg", SortOrder = 6, CreatedAt = Clock.Now });
        await Db.SaveChangesAsync();
        Note("FINDING: DB cho phép INSERT ProductImage.SortOrder = 0 và 6 (thiếu CHECK 1..5).");

        var token = await TkAdm();
        var key = await UploadKeyAsync(token);
        var get = await GetAsync(Url + "/" + B.Pr2.ProductId, token);
        get.Code.Should().Be(200, get.ToString());
        var put = await PutAsync(Url + "/" + B.Pr2.ProductId, token, ProductBody("SP002", null, "Quần IT", new[] { key }));
        put.Code.Should().Be(200, put.ToString());
        var images = await ImagesAsync(B.Pr2.ProductId);
        images.Should().ContainSingle();
        images[0].SortOrder.Should().Be(1);
        images[0].ImageKey.Should().Be(key);
    }

    [Fact]
    public async Task IT_B2_116_Image_Key_Boundaries_And_Invalid_Keys_Are_Rejected_Cleanly()
    {
        var token = await TkAdm();
        var products = await CountAllAsync("Product");
        var images = await CountAllAsync("ProductImage");

        var key500 = await PostAsync(Url, token, ProductBody("SP116A", null, null, new[] { new string('k', 500) }));
        AssertNoSqlLeak(key500);
        key500.Code.Should().BeOneOf(new[] { 200, 201, 400 }, key500.ToString());
        Note($"Key 500 ký tự (không có trong storage) → {key500.Code}");

        var rejected = new[]
        {
            ("SP116B", new[] { new string('k', 501) }),
            ("SP116C", new[] { "khong-ton-tai-trong-storage.jpg" }),
            ("SP116D", new string?[] { null }!),
        };
        foreach (var (code, extra) in rejected)
        {
            var r = await PostAsync(Url, token, ProductBody(code, null, null, extra!));
            r.Code.Should().Be(400, $"{code}: {r}");
            (await FindAsync(code)).Should().BeNull($"{code}: Rollback, không có Product mới");
        }

        var created = key500.Code == 400 ? 0 : 1;
        (await CountAllAsync("Product")).Should().Be(products + created);
        (await CountAllAsync("ProductImage")).Should().Be(images + created);
    }

    [Fact]
    public async Task IT_B2_117_Duplicate_Image_Keys_Are_Handled_Consistently()
    {
        var token = await TkAdm();
        var key = await UploadKeyAsync(token);
        var r = await PostAsync(Url, token, ProductBody("SP117", null, null, new[] { key, key }));
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 201, 400 }, r.ToString());
        Note($"ExtraImages [K1, K1] → {r.Code}");
        var product = await FindAsync("SP117");
        if (r.Code == 400)
        {
            product.Should().BeNull();
            return;
        }

        (await ImagesAsync(product!.ProductId)).Select(i => (int)i.SortOrder).Should().Equal(1, 2);
    }

    [Fact]
    public async Task IT_B2_119_Duplicate_Product_Code_Returns_409()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var exact = await PostAsync(Url, token, ProductBody("SP001"));
        exact.Code.Should().Be(409, exact.ToString());
        AssertNoSqlLeak(exact);
        var viaPut = await PutAsync(Url + "/" + B.Pr2.ProductId, token, ProductBody("SP001", null, "Quần IT"));
        viaPut.Code.Should().Be(409, viaPut.ToString());
        AssertNoSqlLeak(viaPut);
        await AssertB2UnchangedAsync(before);

        var lower = await PostAsync(Url, token, ProductBody("sp001"));
        AssertNoSqlLeak(lower);
        lower.Code.Should().BeOneOf(new[] { 200, 201, 409 }, lower.ToString());
        Note($"Mã 'sp001' → {lower.Code}");
    }

    [Fact]
    public async Task IT_B2_120_Product_Field_Length_Boundaries()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Product");
        var url500 = "https://cdn.x.vn/" + new string('a', 500 - 17);
        ShouldBeCreated(await PostAsync(Url, token, ProductBody(new string('P', 30))));
        (await PostAsync(Url, token, ProductBody(new string('Q', 31)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, ProductBody("SP-D1000", null, Viet(1000))));
        (await PostAsync(Url, token, ProductBody("SP-D1001", null, Viet(1001)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, ProductBody("SP-U500", url500)));
        (await PostAsync(Url, token, ProductBody("SP-U501", url500 + "a"))).Code.Should().Be(400);

        (await FindAsync(new string('P', 30)))!.ProductCode.Length.Should().Be(30);
        (await FindAsync("SP-D1000"))!.Description!.Length.Should().Be(1000);
        (await FindAsync("SP-U500"))!.SampleImageUrl!.Length.Should().Be(500);
        foreach (var rejected in new[] { new string('Q', 31), "SP-D1001", "SP-U501" }) (await FindAsync(rejected)).Should().BeNull();
        (await CountAllAsync("Product")).Should().Be(before + 3);
    }

    [Fact]
    public async Task IT_B2_121_Missing_Product_Code_Returns_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        foreach (var body in new object[] { ProductBody(null!), ProductBody("") })
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_122_Sample_Image_Url_Scheme_Is_Validated()
    {
        var token = await TkAdm();
        ShouldBeCreated(await PostAsync(Url, token, ProductBody("SP122-OK", "https://cdn.x.vn/a.jpg")));
        foreach (var (code, url) in new[] { ("SP122-JS", "javascript:alert(1)"), ("SP122-FILE", "file:///etc/passwd") })
        {
            var r = await PostAsync(Url, token, ProductBody(code, url));
            r.Code.Should().Be(400, $"{url}: {r}");
            (await FindAsync(code)).Should().BeNull();
        }

        await using var db = NewDb();
        (await db.Products.AsNoTracking().AnyAsync(p => p.SampleImageUrl != null && (p.SampleImageUrl.StartsWith("javascript:") || p.SampleImageUrl.StartsWith("file:"))))
            .Should().BeFalse("không lưu scheme nguy hiểm");
        (await FindAsync("SP122-OK"))!.SampleImageUrl.Should().Be("https://cdn.x.vn/a.jpg");
    }

    [Fact]
    public async Task IT_B2_124_Discontinue_Product_Keeps_Images_And_Is_Audited()
    {
        var lastAudit = await LastAuditIdAsync();
        var imagesBefore = await ImagesAsync(B.Pr1.ProductId);
        var r = await PostAsync(Url + $"/{B.Pr2.ProductId}/discontinue", await TkAdm());
        r.Code.Should().Be(200, r.ToString());
        (await ProductAsync(B.Pr2.ProductId))!.Status.Should().Be("DISCONTINUED");
        (await ImagesAsync(B.Pr1.ProductId)).Select(i => i.ImageKey).Should().Equal(imagesBefore.Select(i => i.ImageKey));
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_125_Discontinue_Product_In_Use_Keeps_Order_And_Spec_Intact()
    {
        var before = await ChildrenSnapshotAsync();
        var r = await PostAsync(Url + $"/{B.Pr1.ProductId}/discontinue", await TkAdm());
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 409 }, r.ToString());
        Note($"Ngừng mã hàng đang có đơn → {r.Code}");
        (await ChildrenSnapshotAsync()).Should().Be(before, "CustomerOrder OD1, ProductVariant, ProductComponent không đổi");
        (await ProductAsync(B.Pr1.ProductId))!.Status.Should().Be(r.Code == 200 ? "DISCONTINUED" : "ACTIVE");
    }

    [Fact]
    public async Task IT_B2_126_Invalid_Status_Transitions_Are_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var again = await PostAsync(Url + $"/{B.Pr3.ProductId}/discontinue", token);
        again.Code.Should().BeOneOf(new[] { 400, 409 }, again.ToString());
        var active = await PostAsync(Url + $"/{B.Pr1.ProductId}/reactivate", token);
        active.Code.Should().BeOneOf(new[] { 400, 409 }, active.ToString());
        var missing = await PostAsync(Url + $"/{await MissingIdAsync()}/reactivate", token);
        missing.Code.Should().Be(404, missing.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_127_Reactivate_Discontinued_Product()
    {
        var r = await PostAsync(Url + $"/{B.Pr3.ProductId}/reactivate", await TkAdm());
        r.Code.Should().Be(200, r.ToString());
        (await ProductAsync(B.Pr3.ProductId))!.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B2_128_List_Products_Reports_Is_Used_And_Filters_By_Status()
    {
        var token = await TkAdm();
        var all = await GetAsync(Url + "?PageSize=100", token);
        var active = await GetAsync(Url + "?Status=Active&PageSize=100", token);
        var stopped = await GetAsync(Url + "?Status=Discontinued&PageSize=100", token);
        foreach (var r in new[] { all, active, stopped }) r.Code.Should().Be(200, r.ToString());

        await using var db = NewDb();
        Ids(all, "productId").Should().Contain(new[] { B.Pr1.ProductId, B.Pr2.ProductId, B.Pr3.ProductId })
            .And.BeEquivalentTo(await db.Products.AsNoTracking().Select(p => p.ProductId).ToListAsync());
        var byId = all.Items().ToDictionary(i => B1Json.Int(i, "productId")!.Value);
        B1Json.Bool(byId[B.Pr1.ProductId], "isUsed").Should().BeTrue();
        B1Json.Bool(byId[B.Pr2.ProductId], "isUsed").Should().BeFalse();
        var hasOrders = await db.OrderLines.AsNoTracking().Select(l => l.ProductId).Distinct().ToListAsync();
        foreach (var (id, item) in byId.Where(x => x.Key == B.Pr1.ProductId || x.Key == B.Pr2.ProductId))
            B1Json.Bool(item, "isUsed").Should().Be(hasOrders.Contains(id), "IsUsed khớp việc có tham chiếu từ đơn hàng");
        Ids(active, "productId").Should().BeEquivalentTo(await db.Products.AsNoTracking().Where(p => p.Status == "ACTIVE").Select(p => p.ProductId).ToListAsync());
        Ids(stopped, "productId").Should().BeEquivalentTo(await db.Products.AsNoTracking().Where(p => p.Status == "DISCONTINUED").Select(p => p.ProductId).ToListAsync());
    }

    [Fact]
    public async Task IT_B2_129_Accountant_Reads_Same_Data_As_Admin()
    {
        var adm = await TkAdm();
        var acc = await TkAcc();
        var before = await SnapshotB2Async();
        var adminList = await GetAsync(Url + "?PageSize=100", adm);
        var accList = await GetAsync(AC + "products?PageSize=100", acc);
        accList.Code.Should().Be(200, accList.ToString());
        Ids(accList, "productId").Should().BeEquivalentTo(Ids(adminList, "productId"));

        var adminSpec = await GetAsync(Url + $"/{B.Pr1.ProductId}/spec", adm);
        var accSpec = await GetAsync(AC + $"products/{B.Pr1.ProductId}/spec", acc);
        accSpec.Code.Should().Be(200, accSpec.ToString());
        accSpec.Data.GetRawText().Should().Be(adminSpec.Data.GetRawText(), "dữ liệu giống endpoint của Admin");
        await AssertB2UnchangedAsync(before);
    }

    // ------------------------------------------------------------------ upload / đọc ảnh mẫu (P7, P8, P19)

    [Fact]
    public async Task IT_B2_130_Upload_Sample_Image_Returns_Key_Without_Touching_Product_Tables()
    {
        var before = await SnapshotB2Async();
        var r = await UploadAsync(Url + "/sample-image", await TkAdm(), Jpeg(1_048_576), "img_ok.jpg", "image/jpeg");
        r.Code.Should().Be(200, r.ToString());
        var key = r.Data.ValueKind == JsonValueKind.String ? r.Data.GetString() : B1Json.Str(r.Data, "key") ?? B1Json.Str(r.Data, "imageKey");
        key.Should().NotBeNullOrWhiteSpace();
        await AssertB2UnchangedAsync(before, "không INSERT vào Product / ProductImage (chỉ ghi vào storage)");
    }

    [Fact]
    public async Task IT_B2_131_Upload_Without_File_Returns_400_Not_500()
    {
        var before = await SnapshotB2Async();
        var r = await UploadAsync(Url + "/sample-image", await TkAdm(), null, "", "image/jpeg");
        r.Code.Should().Be(400, r.ToString());
        r.ShouldNotBeServerError();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_132_Upload_Empty_File_Returns_400()
    {
        var before = await SnapshotB2Async();
        var r = await UploadAsync(Url + "/sample-image", await TkAdm(), Array.Empty<byte>(), "empty.jpg", "image/jpeg");
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_133_Upload_Just_Under_Size_Limit_Succeeds()
    {
        var r = await UploadAsync(Url + "/sample-image", await TkAdm(), Jpeg(6_291_456 - 2_048), "img_limit.jpg", "image/jpeg");
        r.Code.Should().Be(200, r.ToString());
    }

    [Fact]
    public async Task IT_B2_134_Upload_Over_Size_Limit_Is_Rejected_Without_500()
    {
        var before = await SnapshotB2Async();
        var r = await UploadAsync(Url + "/sample-image", await TkAdm(), Jpeg(6_291_457), "img_over.jpg", "image/jpeg");
        r.Code.Should().BeOneOf(new[] { 400, 413 }, r.ToString());
        r.ShouldNotBeServerError();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_135_Upload_Non_Image_Content_Is_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var fake = await UploadAsync(Url + "/sample-image", token, FakeExe(), "fake.jpg", "image/jpeg");
        fake.Code.Should().Be(400, fake.ToString());
        var note = await UploadAsync(Url + "/sample-image", token, NoteTxt(), "note.txt", "text/plain");
        note.Code.Should().Be(400, note.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_136_Read_Sample_Image_Returns_File_With_Cache_Header()
    {
        var token = await TkAdm();
        var key = await UploadKeyAsync(token);
        var r = await RawGetAsync(Url + "/sample-image?key=" + Uri.EscapeDataString(key), token);
        r.Code.Should().Be(200, r.Text);
        r.ContentType.Should().StartWith("image/");
        r.CacheControl.Should().Contain("private").And.Contain("max-age=3600");
        r.Bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IT_B2_137_Read_Sample_Image_With_Bad_Key_Returns_Json_Error()
    {
        var token = await TkAdm();
        var missing = await GetAsync(Url + "/sample-image", token);
        missing.Code.Should().Be(400, missing.ToString());
        var empty = await GetAsync(Url + "/sample-image?key=", token);
        empty.Code.Should().Be(400, empty.ToString());
        var unknown = await GetAsync(Url + "/sample-image?key=khong-ton-tai", token);
        unknown.Code.Should().Be(404, unknown.ToString());
        foreach (var r in new[] { missing, empty, unknown }) r.IsJson.Should().BeTrue("lỗi trả JSON qua ToResponse, không trả file");
    }

    [Fact]
    public async Task IT_B2_138_Path_Traversal_Keys_Do_Not_Expose_System_Files()
    {
        var keys = new[] { "../../appsettings.json", "..%2F..%2Fweb.config", "/etc/passwd", @"C:\Windows\win.ini" };
        foreach (var token in new[] { await TkAdm(), await TkAcc() })
        {
            var prefix = token == await TkAdm() ? Url : AC + "products";
            foreach (var key in keys)
            {
                var r = await RawGetAsync(prefix + "/sample-image?key=" + key, token);
                r.Code.Should().BeOneOf(new[] { 400, 404 }, $"{prefix} key={key}: {r.Text}");
                r.Text.Should().NotContainAny("ConnectionStrings", "Jwt", "[fonts]", "root:x:", "<configuration");
            }
        }
    }

    [Fact]
    public async Task IT_B2_139_Accountant_Reads_Image_But_Manager_Is_Forbidden()
    {
        var key = await UploadKeyAsync(await TkAdm());
        var ok = await RawGetAsync(AC + "products/sample-image?key=" + Uri.EscapeDataString(key), await TkAcc());
        ok.Code.Should().Be(200, ok.Text);
        ok.ContentType.Should().StartWith("image/");
        var denied = await RawGetAsync(AC + "products/sample-image?key=" + Uri.EscapeDataString(key), await TkMgr());
        denied.Code.Should().Be(403);
    }
}
