using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>
/// IT_B2_032, 033, 039, 059, 091, 092, 118, 123, 153, 154, 176, 184, 200 — request song song trên connection DB độc lập
/// (dữ liệu S0 được commit, tự dọn sau test).
/// </summary>
public sealed class B2ConcurrencyTests(ITestOutputHelper output) : B2ConcurrentTestBase(output)
{
    // ------------------------------------------------------------------ builder

    private static object SupplierBody(string name) =>
        new { SupplierName = name, Phone = NextPhone(), Email = NextEmail(), Address = "Địa chỉ NCC IT B2", MaterialTypes = "Vải", Note = (string?)null };

    private static object FabricBody(string name, string color = "") =>
        new { MaterialCode = (string?)null, MaterialName = name, MaterialType = "FABRIC", Unit = "m", Color = color, WidthM = (decimal?)1.5m };

    private static object ProductBody(string code, IReadOnlyList<string>? extras) =>
        new { ProductCode = code, SampleImageUrl = (string?)null, Description = (string?)null, ExtraImages = extras };

    private static object Comp(string name, string type, int pieces, int? id = null) =>
        new { ProductComponentId = id, ComponentName = name, ComponentType = type, PiecesPerGarment = pieces };

    private object[] BaseComponents() =>
    [
        Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId),
        Comp("Túi", "PANEL", 2, B.Pr1Pocket.ProductComponentId),
        Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId),
    ];

    private object SpecBody(string extraComponent) => new
    {
        Components = (object[])[.. BaseComponents(), Comp(extraComponent, "PANEL", 1)],
        Variants = new[]
        {
            new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" },
            new { ProductVariantId = (int?)B.Pr1VarL.ProductVariantId, Color = "Đen", SizeCode = "L" },
        },
        Norms = new[]
        {
            new { ComponentName = "Thân trước", Color = "Đen", MaterialId = B.Ma1.MaterialId, SpecNote = (string?)null },
            new { ComponentName = extraComponent, Color = "Đen", MaterialId = B.Ma1.MaterialId, SpecNote = (string?)null },
        },
        MarkerComponents = new[] { new { MarkerNo = 1, ComponentName = "Thân trước" }, new { MarkerNo = 1, ComponentName = extraComponent } },
        MarkerColors = new[] { new { MarkerNo = 1, Color = "Đen" } },
    };

    private static object ImportWorkbook(string extraComponent) => new
    {
        HasComponents = true,
        Components = new[]
        {
            new { RowNumber = 1, Name = "Thân trước", ComponentType = (string?)"PANEL", PiecesPerGarment = (int?)1, ParseError = (string?)null },
            new { RowNumber = 2, Name = "Túi", ComponentType = (string?)"PANEL", PiecesPerGarment = (int?)2, ParseError = (string?)null },
            new { RowNumber = 3, Name = "Nhãn", ComponentType = (string?)"TRIM", PiecesPerGarment = (int?)1, ParseError = (string?)null },
            new { RowNumber = 4, Name = extraComponent, ComponentType = (string?)"PANEL", PiecesPerGarment = (int?)1, ParseError = (string?)null },
        },
        HasNorms = true,
        Norms = new[]
        {
            new { RowNumber = 1, Color = "Đen", ComponentName = extraComponent, MaterialCode = "V-0002", SpecNote = (string?)null, ParseError = (string?)null },
        },
        HasMarkerGroups = false, MarkerGroups = Array.Empty<object>(), HasOrderMarkers = false, OrderMarkers = Array.Empty<object>(),
        SizeColumns = Array.Empty<string>(),
    };

    private async Task<List<string>> ActiveComponentNamesAsync()
    {
        await using var db = NewDb();
        return await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId && c.Status == "ACTIVE")
            .Select(c => c.ComponentName).ToListAsync();
    }

    // ------------------------------------------------------------------ Customer

    [Fact]
    public async Task IT_B2_032_Two_Concurrent_Customer_Creates_With_Same_Name_Exactly_One_Wins()
    {
        var token = await TkAdm();
        var results = await RaceAsync(
            () => PostAsync(A + "customers", token, CustomerBody("KH Đồng Thời", NextPhone(), NextEmail())),
            () => PostAsync(A + "customers", token, CustomerBody("KH Đồng Thời", NextPhone(), NextEmail())));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, "lỗi unique từ DB phải được map thành 409. " + Summary(results));

        await using var db = NewDb();
        (await db.Customers.AsNoTracking().CountAsync(c => c.CustomerName == "KH Đồng Thời")).Should().Be(1);
    }

    [Fact]
    public async Task IT_B2_033_Two_Concurrent_Customer_Creates_With_Same_Phone_Exactly_One_Wins()
    {
        var token = await TkAdm();
        var results = await RaceAsync(
            () => PostAsync(A + "customers", token, CustomerBody("KH Phone A", "0909999999", NextEmail())),
            () => PostAsync(A + "customers", token, CustomerBody("KH Phone B", "0909999999", NextEmail())));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, Summary(results));

        await using var db = NewDb();
        (await db.Customers.AsNoTracking().CountAsync(c => c.Phone == "0909999999")).Should().Be(1, "chỉ có 1 dòng mang số điện thoại đó");
    }

    [Fact]
    public async Task IT_B2_039_Two_Concurrent_Customer_Updates_Never_Mix_Values()
    {
        var token = await TkAdm();
        var phone = NextPhone();
        var email = NextEmail();
        var url = A + $"customers/{B.Cu2.CustomerId}";
        var results = await RaceAsync(
            () => PutAsync(url, token, CustomerBody("A1", phone, email)),
            () => PutAsync(url, token, CustomerBody("A2", phone, email)));
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
        if (results.All(r => r.Code == 200)) Note("Last-write-wins: không có kiểm soát ghi đè (Customer không có RowVer).");

        await using var db = NewDb();
        var row = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == B.Cu2.CustomerId);
        row.CustomerName.Should().BeOneOf("A1", "A2");
        row.Status.Should().Be("ACTIVE");
    }

    private void Note(string text) => Out.WriteLine("[?] " + text);

    // ------------------------------------------------------------------ Supplier

    [Fact]
    public async Task IT_B2_059_Two_Concurrent_Supplier_Creates_With_Same_Name_Exactly_One_Wins()
    {
        var token = await TkAdm();
        var results = await RaceAsync(
            () => PostAsync(A + "suppliers", token, SupplierBody("NCC Đồng Thời")),
            () => PostAsync(A + "suppliers", token, SupplierBody("NCC Đồng Thời")));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
        await using var db = NewDb();
        (await db.Suppliers.AsNoTracking().CountAsync(s => s.SupplierName == "NCC Đồng Thời")).Should().Be(1);
    }

    // ------------------------------------------------------------------ Material

    [Fact]
    public async Task IT_B2_091_Concurrent_Auto_Code_Generation_Never_Duplicates_Codes()
    {
        var token = await TkAdm();
        var results = await RaceAsync(
            () => PostAsync(A + "materials", token, FabricBody("Jean A")),
            () => PostAsync(A + "materials", token, FabricBody("Jean B")));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1, Summary(results));
        results.Where(r => !IsSuccess(r)).All(r => r.Code == 409).Should().BeTrue("request thua chỉ được 409 (có thể retry). " + Summary(results));

        await using var db = NewDb();
        var created = await db.Materials.AsNoTracking().Where(m => m.MaterialName == "Jean A" || m.MaterialName == "Jean B").Select(m => m.MaterialCode).ToListAsync();
        created.Should().OnlyHaveUniqueItems();
        created.Count.Should().Be(results.Count(IsSuccess));
        var duplicates = (await db.Materials.AsNoTracking().Select(m => m.MaterialCode).ToListAsync()).GroupBy(c => c).Where(g => g.Count() > 1);
        duplicates.Should().BeEmpty("không có hai dòng trùng MaterialCode");
        Note("Mã đã sinh: " + string.Join(", ", created));
    }

    [Fact]
    public async Task IT_B2_092_Concurrent_Material_Creates_Via_Two_Prefixes_Exactly_One_Wins()
    {
        var adm = await TkAdm();
        var wk = await TkWk();
        var results = await RaceAsync(
            () => PostAsync(A + "materials", adm, FabricBody("Lanh", "Be")),
            () => PostAsync(WK + "materials", wk, FabricBody("Lanh", "Be")));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().CountAsync(m => m.MaterialName == "Lanh" && m.MaterialType == "FABRIC" && m.Color == "Be")).Should().Be(1);
    }

    // ------------------------------------------------------------------ Product

    [Fact]
    public async Task IT_B2_118_Concurrent_Extra_Image_Updates_Leave_One_Complete_Set()
    {
        var token = await TkAdm();
        var keys = new List<string>();
        for (var i = 0; i < 5; i++) keys.Add(await UploadKeyAsync(token, 4096 + i * 53));
        var url = A + $"products/{B.Pr1.ProductId}";
        var results = await RaceAsync(
            () => PutAsync(url, token, ProductBody("SP001", [keys[0], keys[1], keys[2]])),
            () => PutAsync(url, token, ProductBody("SP001", [keys[3], keys[4]])));
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));

        await using var db = NewDb();
        var images = await db.ProductImages.AsNoTracking().Where(i => i.ProductId == B.Pr1.ProductId).OrderBy(i => i.SortOrder).ToListAsync();
        var setA = new[] { keys[0], keys[1], keys[2] };
        var setB = new[] { keys[3], keys[4] };
        var actual = images.Select(i => i.ImageKey).ToArray();
        (actual.SequenceEqual(setA) || actual.SequenceEqual(setB)).Should().BeTrue($"kết quả cuối phải đúng một trong hai tập, không trộn: {string.Join(",", actual)}");
        images.Select(i => (int)i.SortOrder).Should().Equal(Enumerable.Range(1, images.Count));
    }

    [Fact]
    public async Task IT_B2_123_Concurrent_Product_Creates_With_Same_Code_Leave_No_Orphan_Images()
    {
        var token = await TkAdm();
        var keysA = new[] { await UploadKeyAsync(token, 5001), await UploadKeyAsync(token, 5002) };
        var keysB = new[] { await UploadKeyAsync(token, 5003), await UploadKeyAsync(token, 5004), await UploadKeyAsync(token, 5005) };
        var results = await RaceAsync(
            () => PostAsync(A + "products", token, ProductBody("SP200", keysA)),
            () => PostAsync(A + "products", token, ProductBody("SP200", keysB)));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, Summary(results));

        await using var db = NewDb();
        var products = await db.Products.AsNoTracking().Where(p => p.ProductCode == "SP200").ToListAsync();
        products.Should().ContainSingle();
        var images = await db.ProductImages.AsNoTracking().Where(i => i.ProductId == products[0].ProductId).OrderBy(i => i.SortOrder).Select(i => i.ImageKey).ToListAsync();
        var winner = results[0].Code is 200 or 201 ? keysA : keysB;
        var loser = winner == keysA ? keysB : keysA;
        images.Should().Equal(winner, "ảnh phụ chỉ thuộc bản ghi thắng");
        (await db.ProductImages.AsNoTracking().CountAsync(i => loser.Contains(i.ImageKey))).Should().Be(0, "không có ProductImage mồ côi của request thua");
    }

    [Fact]
    public async Task IT_B2_153_Spec_Save_Fails_Atomically_When_A_Child_Insert_Fails()
    {
        var token = await TkAdm();
        var variantsAndNorms = async () =>
        {
            await using var db = NewDb();
            return (await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).OrderBy(c => c.ProductComponentId)
                    .Select(c => c.ComponentName + "|" + c.Status).ToListAsync(),
                await db.ProductComponentMaterials.AsNoTracking().Where(n => n.ProductId == B.Pr1.ProductId).OrderBy(n => n.ProductComponentMaterialId)
                    .Select(n => n.ProductComponentMaterialId + "|" + n.MaterialId + "|" + n.Status).ToListAsync(),
                await db.ProductMarkerComponents.AsNoTracking().CountAsync(m => m.ProductId == B.Pr1.ProductId));
        };
        var before = await variantsAndNorms();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        async Task Exec(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        var triggers = new[] { ("trg_IT_B2_153_MarkerComponent", "ProductMarkerComponent"), ("trg_IT_B2_153_MarkerFabric", "ProductMarkerFabric") };
        try
        {
            foreach (var (name, table) in triggers)
                await Exec($"CREATE TRIGGER dbo.{name} ON dbo.{table} AFTER INSERT AS BEGIN THROW 50001, 'IT_B2_153 fault injection', 1; END");

            var r = await PutAsync(A + $"products/{B.Pr1.ProductId}/spec", token, SpecBody("Cổ"));
            Note($"PUT spec khi INSERT nhóm bìa lỗi → {r.Code}");
            r.Code.Should().BeOneOf(new[] { 400, 409, 500 }, "lỗi có kiểm soát. " + r);
            r.IsJson.Should().BeTrue("lỗi phải trả envelope JSON, không rò rỉ trang lỗi thô");
        }
        finally
        {
            foreach (var (name, _) in triggers) await Exec($"IF OBJECT_ID('dbo.{name}', 'TR') IS NOT NULL DROP TRIGGER dbo.{name}");
        }

        var after = await variantsAndNorms();
        after.Item1.Should().Equal(before.Item1, "\"Cổ\" không được thêm, Components không đổi");
        after.Item2.Should().Equal(before.Item2, "Norms cũ còn nguyên, không bị xóa nửa chừng");
        after.Item3.Should().Be(before.Item3);
    }

    [Fact]
    public async Task IT_B2_154_Concurrent_Spec_Saves_Leave_State_Of_Exactly_One_Request()
    {
        var token = await TkAdm();
        var url = A + $"products/{B.Pr1.ProductId}/spec";
        var results = await RaceAsync(
            () => PutAsync(url, token, SpecBody("Cổ")),
            () => PutAsync(url, token, SpecBody("Vai")));
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));

        var active = await ActiveComponentNamesAsync();
        var baseNames = new[] { "Thân trước", "Túi", "Nhãn" };
        var expectedA = baseNames.Append("Cổ").OrderBy(x => x);
        var expectedB = baseNames.Append("Vai").OrderBy(x => x);
        var sorted = active.OrderBy(x => x).ToList();
        (sorted.SequenceEqual(expectedA) || sorted.SequenceEqual(expectedB)).Should().BeTrue($"không trộn thành phần của cả hai request: {string.Join(",", sorted)}");

        await using var db = NewDb();
        var norms = await db.ProductComponentMaterials.AsNoTracking().Where(n => n.ProductId == B.Pr1.ProductId && n.Status == "ACTIVE")
            .Select(n => n.ProductComponent.ComponentName).ToListAsync();
        norms.Should().OnlyContain(n => active.Contains(n), "không có dòng Norm mồ côi trỏ tới thành phần đã bị bỏ");
        norms.Count(n => n is "Cổ" or "Vai").Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task IT_B2_176_Concurrent_Imports_Of_Same_File_Do_Not_Duplicate_Components()
    {
        var token = await TkAdm();
        var url = A + $"products/{B.Pr1.ProductId}/import-data";
        var results = await RaceAsync(
            () => PostAsync(url, token, ImportWorkbook("Cổ")),
            () => PostAsync(url, token, ImportWorkbook("Cổ")));
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, Summary(results));
        results.Count(r => r.Code == 200).Should().BeGreaterThanOrEqualTo(1, Summary(results));

        await using var db = NewDb();
        var names = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).Select(c => c.ComponentName).ToListAsync();
        names.Should().OnlyHaveUniqueItems("không có ProductComponent trùng tên trong PR1");
        names.Should().HaveCount(4, "số dòng bằng khi import 1 lần");
    }

    // ------------------------------------------------------------------ Vehicle

    [Fact]
    public async Task IT_B2_184_Two_Concurrent_Vehicle_Creates_With_Same_Plate_Exactly_One_Wins()
    {
        var token = await TkAcc();
        object Body() => new { PlateNumber = "51C-333.33", VehicleType = "Tải 1T", FuelNormPer100Km = 10m };
        var results = await RaceAsync(
            () => PostAsync(AC + "vehicles", token, Body()),
            () => PostAsync(AC + "vehicles", token, Body()));
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, Summary(results));
        results.Count(r => r.Code == 409).Should().Be(1, Summary(results));
        await using var db = NewDb();
        (await db.Vehicles.AsNoTracking().CountAsync(v => v.PlateNumber == "51C-333.33")).Should().Be(1);
    }

    // ------------------------------------------------------------------ lặp 10 vòng

    [Fact]
    public async Task IT_B2_200_Concurrency_Scenarios_Repeated_Ten_Rounds_Never_Return_500()
    {
        var adm = await TkAdm();
        var acc = await TkAcc();
        var keys = new List<string>();
        for (var i = 0; i < 5; i++) keys.Add(await UploadKeyAsync(adm, 6000 + i * 59));

        var run = Guid.NewGuid().ToString("N")[..6];
        var plateRun = Random.Shared.Next(100, 999);
        for (var round = 1; round <= 10; round++)
        {
            var tag = $"R{round}-{run}";

            // 032: khách trùng tên
            var customers = await RaceAsync(
                () => PostAsync(A + "customers", adm, CustomerBody("KH ĐT " + tag, NextPhone(), NextEmail())),
                () => PostAsync(A + "customers", adm, CustomerBody("KH ĐT " + tag, NextPhone(), NextEmail())));
            AssertRaceNoServerError(customers);
            customers.Count(IsSuccess).Should().Be(1, $"vòng {round} IT_032: {Summary(customers)}");
            customers.Count(r => r.Code == 409).Should().Be(1, $"vòng {round} IT_032: {Summary(customers)}");

            // 091: tự sinh mã NVL
            var materials = await RaceAsync(
                () => PostAsync(A + "materials", adm, FabricBody("Jean A " + tag)),
                () => PostAsync(A + "materials", adm, FabricBody("Jean B " + tag)));
            AssertRaceNoServerError(materials);
            materials.Where(r => !IsSuccess(r)).All(r => r.Code == 409).Should().BeTrue($"vòng {round} IT_091: {Summary(materials)}");

            // 118: ảnh phụ
            var url = A + $"products/{B.Pr1.ProductId}";
            var images = await RaceAsync(
                () => PutAsync(url, adm, ProductBody("SP001", [keys[0], keys[1], keys[2]])),
                () => PutAsync(url, adm, ProductBody("SP001", [keys[3], keys[4]])));
            AssertRaceNoServerError(images);
            images.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, $"vòng {round} IT_118: {Summary(images)}");

            // 154: định mức
            var specUrl = url + "/spec";
            var specs = await RaceAsync(
                () => PutAsync(specUrl, adm, SpecBody("Cổ" + tag)),
                () => PutAsync(specUrl, adm, SpecBody("Vai" + tag)));
            AssertRaceNoServerError(specs);
            specs.Should().OnlyContain(r => r.Code == 200 || r.Code == 409, $"vòng {round} IT_154: {Summary(specs)}");

            // 184: biển số xe
            var plate = $"51C-{plateRun}.{round:D2}";
            var vehicles = await RaceAsync(
                () => PostAsync(AC + "vehicles", acc, new { PlateNumber = plate, VehicleType = "Tải 1T", FuelNormPer100Km = 10m }),
                () => PostAsync(AC + "vehicles", acc, new { PlateNumber = plate, VehicleType = "Tải 1T", FuelNormPer100Km = 10m }));
            AssertRaceNoServerError(vehicles);
            vehicles.Count(IsSuccess).Should().Be(1, $"vòng {round} IT_184: {Summary(vehicles)}");
            vehicles.Count(r => r.Code == 409).Should().Be(1, $"vòng {round} IT_184: {Summary(vehicles)}");

            await using var db = NewDb();
            (await db.Customers.AsNoTracking().CountAsync(c => c.CustomerName == "KH ĐT " + tag)).Should().Be(1, $"vòng {round}");
            (await db.Vehicles.AsNoTracking().CountAsync(v => v.PlateNumber == plate)).Should().Be(1, $"vòng {round}");
            var codes = await db.Materials.AsNoTracking().Select(m => m.MaterialCode).ToListAsync();
            codes.GroupBy(c => c).Where(g => g.Count() > 1).Should().BeEmpty($"vòng {round}: không có MaterialCode trùng");
            var imageSet = await db.ProductImages.AsNoTracking().Where(i => i.ProductId == B.Pr1.ProductId).OrderBy(i => i.SortOrder).Select(i => i.ImageKey).ToListAsync();
            (imageSet.SequenceEqual(new[] { keys[0], keys[1], keys[2] }) || imageSet.SequenceEqual(new[] { keys[3], keys[4] }))
                .Should().BeTrue($"vòng {round}: ảnh phụ không trộn");
            var orphans = await db.ProductComponentMaterials.AsNoTracking()
                .Where(n => n.ProductId == B.Pr1.ProductId && !db.ProductComponents.Any(c => c.ProductComponentId == n.ProductComponentId)).CountAsync();
            orphans.Should().Be(0, $"vòng {round}: không có Norm mồ côi");
        }
    }
}
