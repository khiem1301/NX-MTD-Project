using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_195–199 — kiểm tra chéo: trạng thái viết HOA, nhật ký, dữ liệu đang được tham chiếu. Ca 200 (lặp đồng thời) nằm ở B2ConcurrencyTests.</summary>
public sealed class B2CrossCheckTests(ITestOutputHelper output) : B2TestBase(output)
{
    /// <summary>Ảnh chụp các bảng con bị tham chiếu bởi Customer / Supplier / Material / Product / Vehicle.</summary>
    private async Task<string> ChildrenAsync()
    {
        await using var db = NewDb();
        return JsonSerializer.Serialize(new
        {
            orders = await db.CustomerOrders.AsNoTracking().OrderBy(x => x.OrderId).Select(x => new { x.OrderId, x.CustomerId, x.Status }).ToListAsync(),
            purchaseOrders = await db.PurchaseOrders.AsNoTracking().OrderBy(x => x.PurchaseOrderId).Select(x => new { x.PurchaseOrderId, x.SupplierId, x.GoodsStatus }).ToListAsync(),
            stocks = await db.StockBalances.AsNoTracking().OrderBy(x => x.StockBalanceId).Select(x => new { x.StockBalanceId, x.MaterialId, x.OnHandQty }).ToListAsync(),
            norms = await db.ProductComponentMaterials.AsNoTracking().OrderBy(x => x.ProductComponentMaterialId).Select(x => new { x.ProductComponentMaterialId, x.MaterialId, x.Status }).ToListAsync(),
            orderMaterials = await db.OrderMaterials.AsNoTracking().OrderBy(x => x.OrderMaterialId).Select(x => new { x.OrderMaterialId, x.MaterialId, x.RequiredQty }).ToListAsync(),
            trips = await db.VehicleTrips.AsNoTracking().OrderBy(x => x.VehicleTripId).Select(x => new { x.VehicleTripId, x.VehicleId, x.Status }).ToListAsync(),
        });
    }

    [Fact]
    public async Task IT_B2_195_All_Status_Columns_Are_Upper_Case_After_Transitions()
    {
        var adm = await TkAdm();
        var acc = await TkAcc();
        (await PostAsync(A + $"customers/{B.Cu2.CustomerId}/discontinue", adm, new { Reason = "Ngừng đặt hàng" })).Code.Should().Be(200);
        (await PostAsync(A + $"customers/{B.Cu3.CustomerId}/reactivate", adm)).Code.Should().Be(200);
        (await PostAsync(A + $"suppliers/{B.Su2.SupplierId}/discontinue", adm, new { Reason = "Giá cao" })).Code.Should().Be(200);
        (await PostAsync(A + $"suppliers/{B.Su3.SupplierId}/reactivate", adm)).Code.Should().Be(200);
        (await PatchAsync(A + $"materials/{B.Ma2.MaterialId}/status", adm, new { Status = "DISCONTINUED" })).Code.Should().Be(200);
        (await PostAsync(A + "materials/reopen", adm, new { MaterialCode = "V-0003" })).Code.Should().Be(200);
        (await PostAsync(A + $"products/{B.Pr2.ProductId}/discontinue", adm)).Code.Should().Be(200);
        (await PostAsync(A + $"products/{B.Pr3.ProductId}/reactivate", adm)).Code.Should().Be(200);
        (await PostAsync(AC + $"vehicles/{B.Ve2.VehicleId}/discontinue", acc)).Code.Should().Be(200);
        (await PostAsync(AC + $"vehicles/{B.Ve3.VehicleId}/reactivate", acc)).Code.Should().Be(200);

        await using var db = NewDb();
        var all = new Dictionary<string, List<string>>
        {
            ["Customer"] = await db.Customers.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync(),
            ["Supplier"] = await db.Suppliers.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync(),
            ["Material"] = await db.Materials.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync(),
            ["Product"] = await db.Products.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync(),
            ["Vehicle"] = await db.Vehicles.AsNoTracking().Select(x => x.Status).Distinct().ToListAsync(),
        };
        foreach (var (table, values) in all)
        {
            values.Should().OnlyContain(v => v == "ACTIVE" || v == "DISCONTINUED", $"{table}: mọi Status phải viết HOA, không có Active / Discontinued dạng PascalCase");
            values.Should().NotContain(new[] { "Active", "Discontinued" });
        }
    }

    [Fact]
    public async Task IT_B2_196_Status_Is_Consistent_Between_List_And_Options()
    {
        var token = await TkAdm();
        (await PostAsync(A + $"customers/{B.Cu2.CustomerId}/discontinue", token, new { Reason = "Ngừng đặt hàng" })).Code.Should().Be(200);
        var list = await GetAsync(A + "customers?Status=Discontinued&PageSize=100", token);
        var options = await GetAsync(A + "customers/options", token);
        list.Code.Should().Be(200, list.ToString());
        options.Code.Should().Be(200, options.ToString());
        var fromList = list.Items().Single(i => B1Json.Int(i, "customerId") == B.Cu2.CustomerId);
        var fromOptions = options.Items().Single(i => B1Json.Int(i, "customerId") == B.Cu2.CustomerId);
        Note($"Status trong list = '{B1Json.Str(fromList, "status")}', trong options = '{B1Json.Str(fromOptions, "status")}'");
        B1Json.Str(fromOptions, "status").Should().Be(B1Json.Str(fromList, "status"), "cùng một dạng viết (UPPER hoặc Pascal) ở mọi endpoint");
        (await CustomerAsync(B.Cu2.CustomerId))!.Status.Should().Be("DISCONTINUED", "DB viết HOA");
    }

    [Fact]
    public async Task IT_B2_197_Failed_Operations_Never_Write_Audit_Logs()
    {
        var adm = await TkAdm();
        var keys = new List<string>();
        for (var i = 0; i < 6; i++) keys.Add(await UploadKeyAsync(adm, 4096 + i * 41));
        var lastAudit = await LastAuditIdAsync();

        var cu = await PostAsync(A + "customers", adm, CustomerBody("Công ty May Việt"));
        cu.Code.Should().Be(409, "IT_B2_021: " + cu);
        var ma = await PostAsync(A + "materials", adm, MaterialBody(null, "Kaki", "FABRIC", "m", "Đen", 1.5m));
        ma.Code.Should().Be(409, "IT_B2_076: " + ma);
        var pr = await PostAsync(A + "products", adm, ProductBody("SP101", null, null, keys));
        pr.Code.Should().Be(400, "IT_B2_108: " + pr);
        var spec = await PutAsync(A + $"products/{B.Pr1.ProductId}/spec", adm, new
        {
            Components = new[] { new { ProductComponentId = (int?)B.Pr1Front.ProductComponentId, ComponentName = "Thân trước", ComponentType = "PANEL", PiecesPerGarment = 1 } },
            Variants = new[] { new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" } },
            Norms = new[] { new { ComponentName = "Thân trước", Color = "Trắng", MaterialId = B.Ma1.MaterialId, SpecNote = (string?)null } },
            MarkerComponents = Array.Empty<object>(), MarkerColors = Array.Empty<object>(),
        });
        spec.Code.Should().Be(400, "IT_B2_144: " + spec);
        var import = await UploadXlsxAsync(A + $"products/{B.Pr1.ProductId}/import", adm, await SpecErrAsync(), "spec_err.xlsx");
        import.Code.Should().BeOneOf(new[] { 400, 409 }, "IT_B2_165: " + import);

        await AssertNoNewAuditAsync(lastAudit, "MAX(AuditLogs.Id) không đổi sau các thao tác thất bại");
    }

    [Fact]
    public async Task IT_B2_198_Supplier_History_Records_Update_Discontinue_And_Reactivate_In_Order()
    {
        var token = await TkAdm();
        var lastAudit = await LastAuditIdAsync();
        var id = B.Su2.SupplierId;
        (await PutAsync(A + $"suppliers/{id}", token, SupplierBody("Phụ Liệu Minh 2"))).Code.Should().Be(200);
        (await PostAsync(A + $"suppliers/{id}/discontinue", token, new { Reason = "Giá cao" })).Code.Should().Be(200);
        (await PostAsync(A + $"suppliers/{id}/reactivate", token)).Code.Should().Be(200);

        var detail = await GetAsync(A + $"suppliers/{id}", token);
        detail.Code.Should().Be(200, detail.ToString());
        var history = B1Json.Prop(detail.Data, "history")!.Value.EnumerateArray().ToList();
        history.Should().HaveCount(3, "History[] có 3 mục mới");
        var stamps = history.Select(h => DateTime.Parse(B1Json.Str(h, "changedAt")!)).ToList();
        (stamps.SequenceEqual(stamps.OrderBy(x => x)) || stamps.SequenceEqual(stamps.OrderByDescending(x => x))).Should().BeTrue("theo đúng thứ tự thời gian");
        history.Any(h => h.GetRawText().Contains("Giá cao")).Should().BeTrue("mục discontinue chứa Reason");
        await AssertAuditAddedAsync(lastAudit, 3);
    }

    [Fact]
    public async Task IT_B2_199_Discontinuing_Referenced_Records_Never_Cascades_Or_Returns_500()
    {
        var adm = await TkAdm();
        var acc = await TkAcc();
        var children = await ChildrenAsync();
        var results = new[]
        {
            await PostAsync(A + $"customers/{B.Cu1.CustomerId}/discontinue", adm, new { Reason = "Tạm dừng" }),
            await PostAsync(A + $"suppliers/{B.Su1.SupplierId}/discontinue", adm, new { Reason = "Giá cao" }),
            await PatchAsync(A + $"materials/{B.Ma1.MaterialId}/status", adm, new { Status = "DISCONTINUED" }),
            await PostAsync(A + $"products/{B.Pr1.ProductId}/discontinue", adm),
            await PostAsync(AC + $"vehicles/{B.Ve1.VehicleId}/discontinue", acc),
        };
        foreach (var r in results)
        {
            r.ShouldNotBeServerError();
            r.Code.Should().BeOneOf(new[] { 200, 409 }, r.ToString());
        }

        Note("Mã trả về: " + string.Join(", ", results.Select(r => r.Code)));
        (await ChildrenAsync()).Should().Be(children, "CustomerOrder, PurchaseOrder, StockBalance, ProductComponentMaterial, OrderMaterial, VehicleTrip giữ nguyên");
    }
}
