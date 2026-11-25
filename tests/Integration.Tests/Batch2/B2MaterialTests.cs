using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_070–106 — Material qua 4 controller (A / M / AC / WK). Ca đồng thời 091, 092 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2MaterialTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string Url => A + "materials";

    private async Task<Material?> FindAsync(string name, string type, string color)
    {
        await using var db = NewDb();
        return await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.MaterialName == name && m.MaterialType == type && m.Color == color);
    }

    private async Task<Material?> FindByNameAsync(string name)
    {
        await using var db = NewDb();
        return await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.MaterialName == name);
    }

    [Fact]
    public async Task IT_B2_070_Create_Fabric_Auto_Generates_Next_V_Code()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url, token, MaterialBody(null, "Jean", "FABRIC", "m", "Xanh", 1.45m));
        ShouldBeCreated(r);
        B1Json.Str(r.Data, "materialCode").Should().Be("V-0004");
        var row = await FindAsync("Jean", "FABRIC", "Xanh");
        row.Should().NotBeNull();
        row!.MaterialCode.Should().Be("V-0004");
        row.Unit.Should().Be("m");
        row.WidthM.Should().Be(1.45m);
        row.Status.Should().Be("ACTIVE");
        AssertCreatedNow(row.CreatedAt);
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_071_Create_Trim_Via_Manager_Generates_Next_PL_Code()
    {
        var r = await PostAsync(M + "materials", await TkMgr(), MaterialBody("", "Khóa kéo", "TRIM", "cái"));
        ShouldBeCreated(r);
        B1Json.Str(r.Data, "materialCode").Should().Be("PL-0002");
        var row = (await FindByNameAsync("Khóa kéo"))!;
        row.MaterialCode.Should().Be("PL-0002");
        row.MaterialType.Should().Be("TRIM");
        row.Color.Should().Be("");
        row.WidthM.Should().BeNull();
    }

    [Fact]
    public async Task IT_B2_072_WarehouseKeeper_Create_Keeps_Custom_Code()
    {
        var r = await PostAsync(WK + "materials", await TkWk(), MaterialBody("V-CUSTOM-01", "Thun", "FABRIC", "m", null, 1.80m));
        ShouldBeCreated(r);
        var row = (await FindByNameAsync("Thun"))!;
        row.MaterialCode.Should().Be("V-CUSTOM-01");
        row.WidthM.Should().Be(1.80m);
    }

    [Fact]
    public async Task IT_B2_073_Accountant_Can_Create_Material()
    {
        var r = await PostAsync(AC + "materials", await TkAcc(), MaterialBody(null, "Chỉ may", "TRIM", "cuộn"));
        ShouldBeCreated(r);
        var row = (await FindByNameAsync("Chỉ may"))!;
        row.Status.Should().Be("ACTIVE");
        row.MaterialType.Should().Be("TRIM");
    }

    [Fact]
    public async Task IT_B2_074_Duplicate_Material_Code_Returns_409()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var exact = await PostAsync(Url, token, MaterialBody("V-0001", "Dup A", "TRIM", "cái"));
        exact.Code.Should().Be(409, exact.ToString());
        AssertNoSqlLeak(exact);
        await AssertB2UnchangedAsync(before);

        var lower = await PostAsync(Url, token, MaterialBody("v-0001", "Dup B", "TRIM", "cái"));
        AssertNoSqlLeak(lower);
        lower.Code.Should().BeOneOf(new[] { 200, 201, 409 }, lower.ToString());
        Note($"Mã chữ thường 'v-0001' → {lower.Code}");
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().CountAsync(m => m.MaterialCode == "V-0001")).Should().Be(lower.Code == 409 ? 1 : 2, "DB nhất quán với API (theo collation)");
    }

    [Fact]
    public async Task IT_B2_075_Material_Code_Length_Boundary_30_And_31()
    {
        var token = await TkAdm();
        var code30 = "V-" + new string('A', 28);
        var code31 = "V-" + new string('B', 29);
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(code30, "Mã dài 30", "TRIM", "cái")));
        var over = await PostAsync(Url, token, MaterialBody(code31, "Mã dài 31", "TRIM", "cái"));
        over.Code.Should().Be(400, over.ToString());
        (await FindByNameAsync("Mã dài 30"))!.MaterialCode.Should().Be(code30);
        (await FindByNameAsync("Mã dài 31")).Should().BeNull();
    }

    [Fact]
    public async Task IT_B2_076_Duplicate_Name_Type_Color_Returns_409()
    {
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Kaki", "FABRIC", "m", "Đen", 1.5m));
        r.Code.Should().Be(409, r.ToString());
        AssertNoSqlLeak(r);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_077_Null_Color_Defaults_To_Empty_And_Conflicts_With_Colorless_Material()
    {
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Kaki", "FABRIC", "m", null, 1.5m));
        r.Code.Should().Be(409, r.ToString());
        AssertNoSqlLeak(r);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_078_Different_Color_Or_Type_Is_A_Different_Material()
    {
        var token = await TkAdm();
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Kaki", "FABRIC", "m", "Đỏ", 1.5m)));
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Kaki", "TRIM", "cái")));
        await using var db = NewDb();
        var rows = await db.Materials.AsNoTracking().Where(m => m.MaterialName == "Kaki").Select(m => new { m.MaterialType, m.Color }).ToListAsync();
        rows.Should().HaveCount(4);
        rows.Select(x => (x.MaterialType, x.Color)).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public async Task IT_B2_079_Color_Case_Variant_Is_Handled_Consistently()
    {
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Kaki", "FABRIC", "m", "đen", 1.5m));
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 201, 409 }, r.ToString());
        Note($"Color 'đen' (khác hoa/thường) → {r.Code}");
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().CountAsync(m => m.MaterialName == "Kaki" && m.MaterialType == "FABRIC")).Should().Be(r.Code == 409 ? 2 : 3);
    }

    [Fact]
    public async Task IT_B2_080_Fabric_Without_Width_Returns_400()
    {
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Vải thiếu khổ", "FABRIC", "m", null, null));
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_081_Trim_Ignores_Width()
    {
        ShouldBeCreated(await PostAsync(Url, await TkAdm(), MaterialBody(null, "Phụ liệu có khổ", "TRIM", "cái", null, 1.20m)));
        (await FindByNameAsync("Phụ liệu có khổ"))!.WidthM.Should().BeNull();
    }

    [Fact]
    public async Task IT_B2_082_Fabric_Unit_Is_Always_Meter()
    {
        ShouldBeCreated(await PostAsync(Url, await TkAdm(), MaterialBody(null, "Vải đơn vị cuộn", "FABRIC", "cuộn", null, 1.5m)));
        (await FindByNameAsync("Vải đơn vị cuộn"))!.Unit.Should().Be("m");
    }

    [Fact]
    public async Task IT_B2_083_Fabric_Width_Boundaries()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Material");
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Khổ 99.99", "FABRIC", "m", null, 99.99m)));
        var over = await PostAsync(Url, token, MaterialBody(null, "Khổ 100", "FABRIC", "m", null, 100.00m));
        over.Code.Should().Be(400, over.ToString());
        AssertNoSqlLeak(over);
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Khổ 0.01", "FABRIC", "m", null, 0.01m)));
        foreach (var (name, width) in new[] { ("Khổ 0", 0m), ("Khổ âm", -1m) })
        {
            var r = await PostAsync(Url, token, MaterialBody(null, name, "FABRIC", "m", null, width));
            r.Code.Should().Be(400, $"{name}: {r}");
        }

        (await FindByNameAsync("Khổ 99.99"))!.WidthM.Should().Be(99.99m);
        (await FindByNameAsync("Khổ 0.01"))!.WidthM.Should().Be(0.01m);
        foreach (var rejected in new[] { "Khổ 100", "Khổ 0", "Khổ âm" }) (await FindByNameAsync(rejected)).Should().BeNull(rejected);
        (await CountAllAsync("Material")).Should().Be(before + 2);
    }

    [Fact]
    public async Task IT_B2_084_Width_With_Three_Decimals_Is_Rejected_Or_Rounded()
    {
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Khổ 1.555", "FABRIC", "m", null, 1.555m));
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 201, 400 }, r.ToString());
        var row = await FindByNameAsync("Khổ 1.555");
        if (r.Code == 400)
        {
            row.Should().BeNull();
            return;
        }

        row.Should().NotBeNull();
        Note($"WidthM 1.555 được lưu thành {row!.WidthM}");
        row.WidthM.Should().BeOneOf(1.56m, 1.55m);
    }

    [Fact]
    public async Task IT_B2_085_Invalid_Material_Type_Is_Rejected()
    {
        var token = await TkAdm();
        foreach (var type in new[] { "OTHER", "XYZ" })
        {
            var r = await PostAsync(Url, token, MaterialBody(null, "Loại " + type, type, "cái", null, 1.5m));
            r.Code.Should().Be(400, $"{type}: {r}");
        }

        var lower = await PostAsync(Url, token, MaterialBody(null, "Loại fabric", "fabric", "m", null, 1.5m));
        AssertNoSqlLeak(lower);
        lower.Code.Should().BeOneOf(new[] { 200, 201, 400 }, lower.ToString());
        Note($"MaterialType 'fabric' → {lower.Code}");
        await using var db = NewDb();
        var types = await db.Materials.AsNoTracking().Select(m => m.MaterialType).Distinct().ToListAsync();
        types.Should().OnlyContain(t => t == "FABRIC" || t == "TRIM" || t == "SCRAP", "không tồn tại MaterialType nào ngoài FABRIC / TRIM / SCRAP");
    }

    [Fact]
    public async Task IT_B2_086_Scrap_Type_Creation_Is_Recorded()
    {
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Vải vụn IT", "SCRAP", "kg"));
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 201, 400 }, r.ToString());
        Note($"Tạo SCRAP qua API → {r.Code}");
        var row = await FindByNameAsync("Vải vụn IT");
        if (r.Code == 400) row.Should().BeNull();
        else row!.MaterialType.Should().Be("SCRAP");
    }

    [Fact]
    public async Task IT_B2_087_Material_Type_Longer_Than_Column_Returns_400()
    {
        var before = await SnapshotB2Async();
        var r = await PostAsync(Url, await TkAdm(), MaterialBody(null, "Loại dài", "FABRICXXXXX", "m", null, 1.5m));
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_088_Name_Color_Unit_Length_Boundaries()
    {
        var token = await TkAdm();
        var before = await CountAllAsync("Material");
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, Viet(200), "TRIM", "cái")));
        (await PostAsync(Url, token, MaterialBody(null, Viet(201), "TRIM", "cái"))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Màu 50", "TRIM", "cái", Viet(50))));
        (await PostAsync(Url, token, MaterialBody(null, "Màu 51", "TRIM", "cái", Viet(51)))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, MaterialBody(null, "Đơn vị 20", "TRIM", Viet(20))));
        (await PostAsync(Url, token, MaterialBody(null, "Đơn vị 21", "TRIM", Viet(21)))).Code.Should().Be(400);

        (await FindByNameAsync(Viet(200)))!.MaterialName.Length.Should().Be(200);
        (await FindByNameAsync("Màu 50"))!.Color.Should().Be(Viet(50));
        (await FindByNameAsync("Đơn vị 20"))!.Unit.Should().Be(Viet(20));
        foreach (var rejected in new[] { Viet(201), "Màu 51", "Đơn vị 21" }) (await FindByNameAsync(rejected)).Should().BeNull();
        (await CountAllAsync("Material")).Should().Be(before + 3);
    }

    [Fact]
    public async Task IT_B2_089_Missing_Required_Fields_Return_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var bodies = new object[]
        {
            new { MaterialType = "TRIM", Unit = "cái" },
            new { MaterialName = "Thiếu loại", Unit = "cái" },
            new { MaterialName = "Thiếu đơn vị", MaterialType = "TRIM" },
        };
        foreach (var body in bodies)
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_090_Mass_Assignment_Fields_Are_Ignored()
    {
        var body = new
        {
            MaterialId = 1, Status = "INACTIVE", CreatedAt = "0001-01-01T00:00:00",
            MaterialCode = (string?)null, MaterialName = "NVL mass", MaterialType = "TRIM", Unit = "cái",
        };
        ShouldBeCreated(await PostAsync(Url, await TkAdm(), body));
        var row = (await FindByNameAsync("NVL mass"))!;
        row.Status.Should().Be("ACTIVE");
        row.MaterialId.Should().NotBe(1);
        AssertCreatedNow(row.CreatedAt);
    }

    [Fact]
    public async Task IT_B2_093_Update_Unused_Material_Color_Succeeds()
    {
        var lastAudit = await LastAuditIdAsync();
        var r = await PutAsync(Url + "/" + B.Ma2.MaterialId, await TkAdm(), UpdateMaterialBody("Kaki", "FABRIC", "m", "Nâu", 1.60m));
        r.Code.Should().Be(200, r.ToString());
        var row = (await MaterialAsync(B.Ma2.MaterialId))!;
        row.Color.Should().Be("Nâu");
        row.MaterialCode.Should().Be("V-0002");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_094_Update_Color_Of_Used_Material_Is_Rejected()
    {
        var before = await SnapshotB2Async();
        var r = await PutAsync(Url + "/" + B.Ma1.MaterialId, await TkAdm(), UpdateMaterialBody("Kaki", "FABRIC", "m", "Xám", 1.50m));
        r.Code.Should().BeOneOf(new[] { 400, 409 }, r.ToString());
        (await MaterialAsync(B.Ma1.MaterialId))!.Color.Should().Be("Đen");
        await AssertB2UnchangedAsync(before, "ProductComponentMaterial, OrderMaterial, StockBalance không đổi");
    }

    [Fact]
    public async Task IT_B2_095_Update_Name_And_Width_Of_Used_Material_Keeps_Color()
    {
        var r = await PutAsync(Url + "/" + B.Ma1.MaterialId, await TkAdm(), UpdateMaterialBody("Kaki cao cấp", "FABRIC", "m", "Đen", 1.52m));
        r.Code.Should().Be(200, r.ToString());
        var row = (await MaterialAsync(B.Ma1.MaterialId))!;
        row.MaterialName.Should().Be("Kaki cao cấp");
        row.WidthM.Should().Be(1.52m);
        row.Color.Should().Be("Đen");
    }

    [Fact]
    public async Task IT_B2_096_Update_Into_Existing_Name_Type_Color_Returns_409()
    {
        var before = await SnapshotB2Async();
        var r = await PutAsync(Url + "/" + B.Ma2.MaterialId, await TkAdm(), UpdateMaterialBody("Kaki", "FABRIC", "m", "Đen", 1.60m));
        r.Code.Should().Be(409, r.ToString());
        AssertNoSqlLeak(r);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_097_Update_Cannot_Change_Material_Code()
    {
        var body = new
        {
            MaterialCode = "V-9000", MaterialName = "Kaki", MaterialType = "FABRIC", Unit = "m", Color = (string?)null, WidthM = (decimal?)1.60m,
        };
        var r = await PutAsync(Url + "/" + B.Ma2.MaterialId, await TkAdm(), body);
        r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        Note($"PUT kèm MaterialCode → {r.Code}");
        (await MaterialAsync(B.Ma2.MaterialId))!.MaterialCode.Should().Be("V-0002");
    }

    [Fact]
    public async Task IT_B2_098_Change_Trim_To_Fabric_Without_Width_Returns_400()
    {
        var before = await SnapshotB2Async();
        var r = await PutAsync(Url + "/" + B.Ma3.MaterialId, await TkAdm(), UpdateMaterialBody("Nút 4 lỗ", "FABRIC", "m", null, null));
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_099_Missing_Material_Returns_404()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var missing = await MissingIdAsync();
        (await PutAsync(Url + "/" + missing, token, UpdateMaterialBody("Kaki", "FABRIC", "m", null, 1.5m))).Code.Should().Be(404);
        (await GetAsync(Url + "/" + missing, token)).Code.Should().Be(404);
        (await GetAsync(Url + "/abc", token)).Code.Should().Be(404);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_100_Change_Status_To_Discontinued_Succeeds_And_Is_Audited()
    {
        var lastAudit = await LastAuditIdAsync();
        var r = await PatchAsync(Url + $"/{B.Ma2.MaterialId}/status", await TkAdm(), new { Status = "DISCONTINUED" });
        r.Code.Should().Be(200, r.ToString());
        (await MaterialAsync(B.Ma2.MaterialId))!.Status.Should().Be("DISCONTINUED");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_101_Invalid_Status_Value_Returns_400()
    {
        var token = await TkWk();
        var before = await SnapshotB2Async();
        var url = WK + $"materials/{B.Ma2.MaterialId}/status";
        foreach (var body in new object[] { new { Status = "Xyz" }, new { Status = "" }, "{}" })
        {
            var r = await PatchAsync(url, token, body);
            r.Code.Should().Be(400, r.ToString());
        }

        (await MaterialAsync(B.Ma2.MaterialId))!.Status.Should().Be("ACTIVE");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_102_Discontinue_Material_With_Stock_Keeps_Stock_Intact()
    {
        var r = await PatchAsync(Url + $"/{B.Ma1.MaterialId}/status", await TkAdm(), new { Status = "DISCONTINUED" });
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 409 }, r.ToString());
        Note($"Ngừng NVL đang có tồn kho → {r.Code}");
        await using var db = NewDb();
        var stock = await db.StockBalances.AsNoTracking().SingleAsync(s => s.StockBalanceId == B.Ma1Stock.StockBalanceId);
        stock.OnHandQty.Should().Be(B.Ma1Stock.OnHandQty);
        stock.MaterialId.Should().Be(B.Ma1.MaterialId);
    }

    [Fact]
    public async Task IT_B2_103_Reopen_Discontinued_Material_By_Code()
    {
        var lastAudit = await LastAuditIdAsync();
        var count = await CountAllAsync("Material");
        var r = await PostAsync(Url + "/reopen", await TkAdm(), new { MaterialCode = "V-0003" });
        r.Code.Should().Be(200, r.ToString());
        (await MaterialAsync(B.Ma4.MaterialId))!.Status.Should().Be("ACTIVE");
        (await CountAllAsync("Material")).Should().Be(count, "không có dòng Material mới");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_104_Reopen_Invalid_Codes_Are_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var active = await PostAsync(Url + "/reopen", token, new { MaterialCode = "V-0001" });
        active.Code.Should().BeOneOf(new[] { 400, 409 }, active.ToString());
        var missing = await PostAsync(Url + "/reopen", token, new { MaterialCode = "V-9999" });
        missing.Code.Should().Be(404, missing.ToString());
        var empty = await PostAsync(Url + "/reopen", token, new { MaterialCode = "" });
        empty.Code.Should().Be(400, empty.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_105_Manager_List_Filters_By_Type_And_Status()
    {
        var r = await GetAsync(M + "materials?MaterialType=FABRIC&Status=Active&PageSize=100", await TkMgr());
        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "materialId");
        ids.Should().Contain(new[] { B.Ma1.MaterialId, B.Ma2.MaterialId }).And.NotContain(B.Ma4.MaterialId);
        await using var db = NewDb();
        ids.Should().BeEquivalentTo(await db.Materials.AsNoTracking()
            .Where(m => m.MaterialType == "FABRIC" && m.Status == "ACTIVE").Select(m => m.MaterialId).ToListAsync());
    }

    [Fact]
    public async Task IT_B2_106_WarehouseKeeper_Get_Material_Returns_Exact_Decimal_Width()
    {
        var before = await SnapshotB2Async();
        var r = await GetAsync(WK + "materials/" + B.Ma1.MaterialId, await TkWk());
        r.Code.Should().Be(200, r.ToString());
        B1Json.Dec(r.Data, "widthM").Should().Be(1.50m);
        B1Json.Str(r.Data, "materialCode").Should().Be("V-0001");
        await AssertB2UnchangedAsync(before);
    }
}
