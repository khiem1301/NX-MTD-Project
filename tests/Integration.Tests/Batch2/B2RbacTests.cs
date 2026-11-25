using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_001–019 — ma trận phân quyền 4 prefix (admin / manager / accountant / warehouse-keeper).</summary>
public sealed class B2RbacTests(ITestOutputHelper output) : B2TestBase(output)
{
    private object SpecBodyOfPr1() => new
    {
        Components = new[] { new { ProductComponentId = (int?)null, ComponentName = "Thân trước", ComponentType = "PANEL", PiecesPerGarment = (short)1 } },
        Variants = new[] { new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" } },
        Norms = Array.Empty<object>(), MarkerComponents = Array.Empty<object>(), MarkerColors = Array.Empty<object>(),
    };

    [Fact]
    public async Task IT_B2_001_No_Token_Returns_401_On_All_Four_Prefixes()
    {
        var before = await SnapshotB2Async();
        var urls = new[] { A + "customers", M + "suppliers", AC + "vehicles", WK + "vehicles/available" };
        foreach (var url in urls)
        {
            var r = await GetAsync(url);
            r.Code.Should().Be(401, $"{url}: {r}");
            r.Body.Should().NotContainAny("Công ty May Việt", "Vải Thành Công", "51C-123.45", "Kaki");
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_002_Expired_And_Forged_Tokens_Return_401()
    {
        var before = await SnapshotB2Async();
        var vehicleCount = await CountAllAsync("Vehicle");
        foreach (var token in new[] { await TkExpired(), await TkForged() })
        {
            (await GetAsync(A + "products", token)).Code.Should().Be(401);
            (await PostAsync(AC + "vehicles", token, VehicleBody("51C-222.22", "Tải 1T", 10m))).Code.Should().Be(401);
        }

        (await CountAllAsync("Vehicle")).Should().Be(vehicleCount, "không có Vehicle mới");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_003_Admin_Cannot_Call_Accountant_Read_Endpoints()
    {
        var token = await TkAdm();
        var key = await UploadKeyAsync(token);
        var before = await SnapshotB2Async();
        foreach (var url in new[] { AC + "vehicles", AC + "products", AC + "products/sample-image?key=" + Uri.EscapeDataString(key) })
        {
            var r = await GetAsync(url, token);
            r.Code.Should().Be(403, $"{url}: {r}");
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_004_Admin_Cannot_Write_Via_Accountant_Prefix()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var materials = await CountAllAsync("Material");

        (await PostAsync(AC + "vehicles", token, VehicleBody("51C-111.11", "Tải 1.5T", 12.5m))).Code.Should().Be(403);
        (await PostAsync(AC + "materials", token, MaterialBody(null, "Chỉ thun", "TRIM", "cuộn"))).Code.Should().Be(403);

        await using var db = NewDb();
        (await db.Vehicles.AsNoTracking().AnyAsync(v => v.PlateNumber == "51C-111.11")).Should().BeFalse();
        (await CountAllAsync("Material")).Should().Be(materials);
        await AssertB2UnchangedAsync(before, "gồm AuditLogs");
    }

    [Fact]
    public async Task IT_B2_005_Admin_Cannot_Call_WarehouseKeeper_Prefix()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        (await GetAsync(WK + "vehicles/available", token)).Code.Should().Be(403);
        (await GetAsync(WK + "materials", token)).Code.Should().Be(403);
        (await PostAsync(WK + "materials", token, MaterialBody(null, "Khuy", "TRIM", "cái"))).Code.Should().Be(403);
        (await PatchAsync(WK + $"materials/{B.Ma2.MaterialId}/status", token, new { Status = "DISCONTINUED" })).Code.Should().Be(403);

        (await MaterialAsync(B.Ma2.MaterialId))!.Status.Should().Be("ACTIVE");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_006_Admin_Can_Call_Manager_Prefix()
    {
        var token = await TkAdm();
        (await GetAsync(M + "suppliers", token)).Code.Should().Be(200);
        (await GetAsync(M + "materials", token)).Code.Should().Be(200);
        var created = await PostAsync(M + "materials", token, MaterialBody(null, "Chun", "TRIM", "cuộn"));
        ShouldBeCreated(created);

        await using var db = NewDb();
        var rows = await db.Materials.AsNoTracking().Where(m => m.MaterialName == "Chun").ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Status.Should().Be("ACTIVE");
        rows[0].MaterialType.Should().Be("TRIM");
    }

    [Fact]
    public async Task IT_B2_007_Manager_Cannot_Call_Admin_Prefix()
    {
        var token = await TkMgr();
        var before = await SnapshotB2Async();
        foreach (var url in new[] { A + "customers", A + "suppliers", A + "materials", A + "products", A + "vehicles" })
            (await GetAsync(url, token)).Code.Should().Be(403, url);

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_008_Manager_Products_Route_Does_Not_Exist()
    {
        var token = await TkMgr();
        var before = await SnapshotB2Async();
        (await GetAsync(M + "products", token)).Code.Should().Be(404);
        var import = await PostAsync(M + $"products/{B.Pr1.ProductId}/import-data", token, new { });
        import.Code.Should().Be(404, import.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_009_Manager_Has_No_Reopen_Route()
    {
        var token = await TkMgr();
        var before = await SnapshotB2Async();
        AssertNotFoundOrNoRoute(await PostAsync(M + "materials/reopen", token, new { MaterialCode = "V-0003" }));
        (await MaterialAsync(B.Ma4.MaterialId))!.Status.Should().Be("DISCONTINUED");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_010_Manager_Cannot_Call_Accountant_Or_WarehouseKeeper_Prefix()
    {
        var token = await TkMgr();
        var before = await SnapshotB2Async();
        (await GetAsync(AC + "vehicles", token)).Code.Should().Be(403);
        (await GetAsync(WK + "vehicles/available", token)).Code.Should().Be(403);
        (await PostAsync(WK + "materials", token, MaterialBody(null, "Khuy", "TRIM", "cái"))).Code.Should().Be(403);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_011_Accountant_Cannot_Call_Admin_Manager_Or_WarehouseKeeper_Prefix()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        foreach (var url in new[] { A + "materials", M + "materials", M + "suppliers", WK + "materials" })
            (await GetAsync(url, token)).Code.Should().Be(403, url);

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_012_Accountant_Material_Has_Only_Create()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        var id = B.Ma2.MaterialId;
        AssertNotFoundOrNoRoute(await GetAsync(AC + "materials", token));
        AssertNotFoundOrNoRoute(await PutAsync(AC + $"materials/{id}", token, UpdateMaterialBody("Kaki", "FABRIC", "m", "Nâu", 1.6m)));
        AssertNotFoundOrNoRoute(await PatchAsync(AC + $"materials/{id}/status", token, new { Status = "DISCONTINUED" }));

        var ma2 = await MaterialAsync(id);
        ma2!.Color.Should().Be("");
        ma2.Status.Should().Be("ACTIVE");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_013_Accountant_Product_Is_Read_Only()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        AssertNotFoundOrNoRoute(await PostAsync(AC + "products", token, ProductBody("SP900")));
        AssertNotFoundOrNoRoute(await PutAsync(AC + $"products/{B.Pr2.ProductId}", token, ProductBody("SP002", null, "sửa")));
        AssertNotFoundOrNoRoute(await PutAsync(AC + $"products/{B.Pr1.ProductId}/spec", token, SpecBodyOfPr1()));
        AssertNotFoundOrNoRoute(await PostAsync(AC + $"products/{B.Pr1.ProductId}/import-data", token, new { }));

        await using var db = NewDb();
        (await db.Products.AsNoTracking().AnyAsync(p => p.ProductCode == "SP900")).Should().BeFalse();
        await AssertB2UnchangedAsync(before, "spec của PR1 không đổi");
    }

    [Fact]
    public async Task IT_B2_014_WarehouseKeeper_Cannot_Call_Other_Prefixes()
    {
        var token = await TkWk();
        var before = await SnapshotB2Async();
        (await GetAsync(A + "products", token)).Code.Should().Be(403);
        (await GetAsync(M + "materials", token)).Code.Should().Be(403);
        (await GetAsync(AC + "vehicles", token)).Code.Should().Be(403);
        (await PostAsync(AC + "materials", token, MaterialBody(null, "Khuy", "TRIM", "cái"))).Code.Should().Be(403);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_015_WarehouseKeeper_Has_No_Reopen_Or_Vehicle_Create()
    {
        var token = await TkWk();
        var before = await SnapshotB2Async();
        AssertNotFoundOrNoRoute(await PostAsync(WK + "materials/reopen", token, new { MaterialCode = "V-0003" }));
        AssertNotFoundOrNoRoute(await PostAsync(WK + "vehicles", token, VehicleBody("51C-444.44", "Tải 1T", 10m)));

        (await MaterialAsync(B.Ma4.MaterialId))!.Status.Should().Be("DISCONTINUED");
        await using var db = NewDb();
        (await db.Vehicles.AsNoTracking().AnyAsync(v => v.PlateNumber == "51C-444.44")).Should().BeFalse();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_016_Other_Roles_Get_403_On_All_Prefixes()
    {
        var before = await SnapshotB2Async();
        var tokens = new[] { await TkTl(), await TkQc(), await TkWrk() };
        var urls = new[] { A + "customers", M + "suppliers", AC + "products", WK + "materials" };
        var count = 0;
        foreach (var token in tokens)
            foreach (var url in urls)
            {
                (await GetAsync(url, token)).Code.Should().Be(403, url);
                count++;
            }

        count.Should().Be(12);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_017_Admin_Vehicles_Is_Read_Only()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        AssertNotFoundOrNoRoute(await PostAsync(A + "vehicles", token, VehicleBody("51C-555.55", "Tải 1T", 10m)));
        AssertNotFoundOrNoRoute(await PutAsync(A + $"vehicles/{B.Ve2.VehicleId}", token, VehicleBody("51C-678.90", "Tải 3.5T", 16m)));

        var ve2 = await VehicleAsync(B.Ve2.VehicleId);
        ve2!.VehicleType.Should().Be("Tải 2.5T");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_018_No_Delete_Api_Exists_For_Any_Master_Entity()
    {
        var adm = await TkAdm();
        var acc = await TkAcc();
        var before = await SnapshotB2Async();
        AssertNotFoundOrNoRoute(await DeleteAsync(A + $"customers/{B.Cu1.CustomerId}", adm));
        AssertNotFoundOrNoRoute(await DeleteAsync(A + $"suppliers/{B.Su1.SupplierId}", adm));
        AssertNotFoundOrNoRoute(await DeleteAsync(A + $"materials/{B.Ma1.MaterialId}", adm));
        AssertNotFoundOrNoRoute(await DeleteAsync(A + $"products/{B.Pr1.ProductId}", adm));
        AssertNotFoundOrNoRoute(await DeleteAsync(AC + $"vehicles/{B.Ve1.VehicleId}", acc));

        (await CustomerAsync(B.Cu1.CustomerId)).Should().NotBeNull();
        (await SupplierAsync(B.Su1.SupplierId)).Should().NotBeNull();
        (await MaterialAsync(B.Ma1.MaterialId)).Should().NotBeNull();
        (await ProductAsync(B.Pr1.ProductId)).Should().NotBeNull();
        (await VehicleAsync(B.Ve1.VehicleId)).Should().NotBeNull();
        await AssertB2UnchangedAsync(before, "CustomerOrder, PurchaseOrder, ProductComponentMaterial, ProductImage, VehicleTrip không đổi");
    }

    [Fact]
    public async Task IT_B2_019_Route_Is_Case_Insensitive_And_Accepts_Trailing_Slash()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var upper = await GetAsync("/api/v1/ADMIN/Customers", token);
        upper.Code.Should().Be(200, upper.ToString());
        var slash = await GetAsync(A + "customers/", token);
        slash.Code.Should().Be(200, slash.ToString());
        await AssertB2UnchangedAsync(before);
    }
}
