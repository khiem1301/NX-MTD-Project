using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_140–152, 155 — định mức mã hàng (P9, P10): toàn vẹn transaction. Ca 153, 154 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2ProductSpecTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string SpecUrl(int productId) => A + $"products/{productId}/spec";

    // ------------------------------------------------------------------ builder body

    private static object Comp(string name, string type, int pieces, int? id = null) =>
        new { ProductComponentId = id, ComponentName = name, ComponentType = type, PiecesPerGarment = pieces };

    private static object Var(string color, string size, int? id = null) => new { ProductVariantId = id, Color = color, SizeCode = size };

    private static object Norm(string component, string color, int materialId, string? note = null) =>
        new { ComponentName = component, Color = color, MaterialId = materialId, SpecNote = note };

    private static object Marker(int no, string component) => new { MarkerNo = no, ComponentName = component };

    private static object MarkerColor(int no, string color) => new { MarkerNo = no, Color = color };

    private static object Spec(object[] components, object[] variants, object[] norms, object[] markers, object[] markerColors) =>
        new { Components = components, Variants = variants, Norms = norms, MarkerComponents = markers, MarkerColors = markerColors };

    private object[] BaseComponents() =>
    [
        Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId),
        Comp("Túi", "PANEL", 2, B.Pr1Pocket.ProductComponentId),
        Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId),
    ];

    private object[] BaseVariants() =>
    [
        Var("Đen", "M", B.Pr1VarM.ProductVariantId),
        Var("Đen", "L", B.Pr1VarL.ProductVariantId),
    ];

    private object[] BaseNorms() => [Norm("Thân trước", "Đen", B.Ma1.MaterialId)];

    private object ValidSpecWithNeck() => Spec(
        [.. BaseComponents(), Comp("Cổ", "PANEL", 1)], BaseVariants(),
        [Norm("Thân trước", "Đen", B.Ma1.MaterialId), Norm("Cổ", "Đen", B.Ma2.MaterialId)],
        [Marker(1, "Thân trước"), Marker(1, "Cổ")], [MarkerColor(1, "Đen")]);

    private async Task<string> VariantsSnapshotAsync()
    {
        await using var db = NewDb();
        return JsonSerializer.Serialize(await db.ProductVariants.AsNoTracking().OrderBy(v => v.ProductVariantId)
            .Select(v => new { v.ProductVariantId, v.ProductId, v.ProductColorId, v.SizeCode, v.Status }).ToListAsync());
    }

    // ------------------------------------------------------------------ ca

    [Fact]
    public async Task IT_B2_140_Get_Spec_Returns_Components_Variants_And_Norms()
    {
        var before = await SnapshotB2Async();
        var r = await GetAsync(SpecUrl(B.Pr1.ProductId), await TkAdm());
        r.Code.Should().Be(200, r.ToString());
        var components = B1Json.Prop(r.Data, "components")!.Value;
        components.GetArrayLength().Should().Be(3);
        foreach (var c in components.EnumerateArray()) B1Json.Str(c, "status").Should().NotBeNullOrEmpty("mỗi thành phần kèm Status");
        var variants = B1Json.Prop(r.Data, "variants")!.Value.EnumerateArray()
            .Select(v => (B1Json.Str(v, "color"), B1Json.Str(v, "sizeCode"))).ToList();
        variants.Should().BeEquivalentTo(new[] { ("Đen", "M"), ("Đen", "L") });
        B1Json.Prop(r.Data, "norms")!.Value.GetArrayLength().Should().Be(1);
        B1Json.Prop(r.Data, "markerComponents").Should().NotBeNull();
        B1Json.Prop(r.Data, "markerFabrics").Should().NotBeNull();

        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().CountAsync(c => c.ProductId == B.Pr1.ProductId)).Should().Be(3);
        (await db.ProductVariants.AsNoTracking().CountAsync(v => v.ProductId == B.Pr1.ProductId)).Should().Be(2);
        (await db.ProductComponentMaterials.AsNoTracking().CountAsync(n => n.ProductId == B.Pr1.ProductId)).Should().Be(1);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_141_Spec_Of_Missing_Product_Returns_404()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var missing = await MissingIdAsync();
        (await GetAsync(SpecUrl(missing), token)).Code.Should().Be(404);
        (await PutAsync(SpecUrl(missing), token, Spec([], [], [], [], []))).Code.Should().Be(404);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_142_Save_Spec_Adds_Component_Replaces_Norms_And_Markers_But_Not_Variants()
    {
        var variantsBefore = await VariantsSnapshotAsync();
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(), ValidSpecWithNeck());
        r.Code.Should().Be(200, r.ToString());

        await using var db = NewDb();
        var names = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).Select(c => c.ComponentName).ToListAsync();
        names.Should().BeEquivalentTo("Thân trước", "Túi", "Nhãn", "Cổ");
        var norms = await db.ProductComponentMaterials.AsNoTracking()
            .Where(n => n.ProductId == B.Pr1.ProductId && n.ProductColorId == B.Pr1Black.ProductColorId && n.Status == "ACTIVE")
            .Select(n => new { n.ProductComponent.ComponentName, n.MaterialId }).ToListAsync();
        norms.Select(n => (n.ComponentName, n.MaterialId)).Should().BeEquivalentTo(new[] { ("Thân trước", B.Ma1.MaterialId), ("Cổ", B.Ma2.MaterialId) });
        var markers = await db.ProductMarkerComponents.AsNoTracking().Where(m => m.ProductId == B.Pr1.ProductId)
            .Select(m => new { m.MarkerNo, m.ProductComponent.ComponentName }).ToListAsync();
        markers.Select(m => ((int)m.MarkerNo, m.ComponentName)).Should().BeEquivalentTo(new[] { (1, "Thân trước"), (1, "Cổ") }, "nhóm bìa được thay nguyên");
        (await VariantsSnapshotAsync()).Should().Be(variantsBefore, "ProductVariant không đổi");
    }

    [Fact]
    public async Task IT_B2_143_Removed_Components_Are_Deleted_Or_Retired_Depending_On_Usage()
    {
        var variantsBefore = await VariantsSnapshotAsync();
        var orderLines = await Db.OrderLines.AsNoTracking().CountAsync(l => l.OrderId == B.Od1.OrderId);
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(),
            Spec([Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId)], BaseVariants(), [], [], []));
        r.Code.Should().Be(200, r.ToString());

        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().AnyAsync(c => c.ProductComponentId == B.Pr1Pocket.ProductComponentId))
            .Should().BeFalse("\"Túi\" chưa dùng ⇒ bị DELETE");
        var front = await db.ProductComponents.AsNoTracking().SingleOrDefaultAsync(c => c.ProductComponentId == B.Pr1Front.ProductComponentId);
        front.Should().NotBeNull("\"Thân trước\" đang dùng ⇒ không bị xóa");
        front!.Status.Should().Be("DISCONTINUED");
        (await db.OrderLines.AsNoTracking().CountAsync(l => l.OrderId == B.Od1.OrderId)).Should().Be(orderLines, "dữ liệu đơn OD1 không đổi");
        (await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderId == B.Od1.OrderId)).Status.Should().Be(B.Od1.Status);
        (await VariantsSnapshotAsync()).Should().Be(variantsBefore);
    }

    [Fact]
    public async Task IT_B2_144_Norm_With_Unknown_Color_Rolls_Back_Whole_Request()
    {
        var before = await SnapshotB2Async();
        var body = Spec([.. BaseComponents(), Comp("Cổ", "PANEL", 1)], BaseVariants(),
            [Norm("Thân trước", "Đen", B.Ma1.MaterialId), Norm("Thân trước", "Trắng", B.Ma1.MaterialId)], [], []);
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(), body);
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before, "Components / Norms / Markers của PR1 không đổi dù có phần hợp lệ");
    }

    [Fact]
    public async Task IT_B2_145_Norm_With_Missing_Material_Returns_Controlled_Error()
    {
        var before = await SnapshotB2Async();
        var body = Spec(BaseComponents(), BaseVariants(), [Norm("Thân trước", "Đen", await MissingIdAsync())], [], []);
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(), body);
        r.Code.Should().BeOneOf(new[] { 400, 404 }, r.ToString());
        AssertNoSqlLeak(r);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_146_Norm_With_Discontinued_Material_Is_Rejected()
    {
        var before = await SnapshotB2Async();
        var body = Spec(BaseComponents(), BaseVariants(), [Norm("Thân trước", "Đen", B.Ma4.MaterialId)], [], []);
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(), body);
        r.Code.Should().Be(400, r.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_147_Spec_Cannot_Create_New_Variants()
    {
        var token = await TkAdm();
        var variantsBefore = await VariantsSnapshotAsync();
        var body = Spec(BaseComponents(), [.. BaseVariants(), Var("Xanh", "XL")], BaseNorms(), [], []);
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), token, body);
        r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        Note($"Gửi biến thể (Xanh, XL) chưa tồn tại → {r.Code}");
        (await VariantsSnapshotAsync()).Should().Be(variantsBefore, "ProductVariant không có dòng (Xanh, XL)");
        await using var db = NewDb();
        (await db.ProductVariants.AsNoTracking().AnyAsync(v => v.ProductId == B.Pr1.ProductId && v.SizeCode == "XL")).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B2_148_Norm_Or_Marker_Referencing_Unknown_Component_Returns_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var norm = await PutAsync(SpecUrl(B.Pr1.ProductId), token,
            Spec(BaseComponents(), BaseVariants(), [Norm("Tay áo", "Đen", B.Ma1.MaterialId)], [], []));
        norm.Code.Should().Be(400, norm.ToString());
        var marker = await PutAsync(SpecUrl(B.Pr1.ProductId), token,
            Spec(BaseComponents(), BaseVariants(), BaseNorms(), [Marker(1, "Tay áo")], [MarkerColor(1, "Đen")]));
        marker.Code.Should().Be(400, marker.ToString());
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_149_Invalid_Component_Type_Or_Duplicate_Names_Return_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var panel2 = await PutAsync(SpecUrl(B.Pr1.ProductId), token,
            Spec([Comp("Thân trước", "PANEL2", 1, B.Pr1Front.ProductComponentId)], BaseVariants(), [], [], []));
        panel2.Code.Should().Be(400, panel2.ToString());
        var duplicate = await PutAsync(SpecUrl(B.Pr1.ProductId), token,
            Spec([Comp("Cổ", "PANEL", 1), Comp("Cổ", "PANEL", 1)], BaseVariants(), [], [], []));
        duplicate.Code.Should().Be(400, duplicate.ToString());
        await AssertB2UnchangedAsync(before);

        var lower = await PutAsync(SpecUrl(B.Pr1.ProductId), token,
            Spec([Comp("Thân trước", "panel", 1, B.Pr1Front.ProductComponentId)], BaseVariants(), [], [], []));
        AssertNoSqlLeak(lower);
        lower.Code.Should().BeOneOf(new[] { 200, 400 }, lower.ToString());
        Note($"ComponentType 'panel' → {lower.Code}");
    }

    [Fact]
    public async Task IT_B2_150_Pieces_Per_Garment_Boundaries()
    {
        var token = await TkAdm();
        var url = SpecUrl(B.Pr1.ProductId);
        Task<Api> Put(int pieces) => PutAsync(url, token, Spec(
            [Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId), Comp("Túi", "PANEL", pieces, B.Pr1Pocket.ProductComponentId),
                Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId)], BaseVariants(), BaseNorms(), [], []));
        async Task<short> PocketPiecesAsync()
        {
            await using var db = NewDb();
            return (await db.ProductComponents.AsNoTracking().SingleAsync(c => c.ProductComponentId == B.Pr1Pocket.ProductComponentId)).PiecesPerGarment;
        }

        (await Put(0)).Code.Should().Be(400);
        (await Put(-1)).Code.Should().Be(400);
        (await PocketPiecesAsync()).Should().Be(2, "ca không hợp lệ không được lưu");
        (await Put(1)).Code.Should().Be(200);
        (await PocketPiecesAsync()).Should().Be(1);
        var max = await Put(32767);
        max.Code.Should().BeOneOf(new[] { 200, 400 }, max.ToString());
        Note($"PiecesPerGarment=32767 → {max.Code}");
        var expected = max.Code == 200 ? (short)32767 : (short)1;
        (await PocketPiecesAsync()).Should().Be(expected);
        var overflow = await Put(32768);
        overflow.Code.Should().Be(400, overflow.ToString());
        overflow.ShouldNotBeServerError();
        (await PocketPiecesAsync()).Should().Be(expected);
    }

    [Fact]
    public async Task IT_B2_151_Marker_No_Boundaries()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        foreach (var markerNo in new long[] { 0, -1, 2_147_483_648 })
        {
            var body = Spec(BaseComponents(), BaseVariants(), BaseNorms(),
                [new { MarkerNo = markerNo, ComponentName = "Thân trước" }], [new { MarkerNo = markerNo, Color = "Đen" }]);
            var r = await PutAsync(SpecUrl(B.Pr1.ProductId), token, body);
            r.Code.Should().Be(400, $"MarkerNo={markerNo}: {r}");
            r.ShouldNotBeServerError();
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_152_Empty_Spec_Retires_Used_Components_And_Deletes_Unused()
    {
        var before = await SnapshotB2Async();
        var variantsBefore = await VariantsSnapshotAsync();
        var r = await PutAsync(SpecUrl(B.Pr1.ProductId), await TkAdm(), Spec([], [], [], [], []));
        r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        Note($"PUT spec toàn mảng rỗng → {r.Code}");
        if (r.Code == 400)
        {
            await AssertB2UnchangedAsync(before);
            return;
        }

        await using var db = NewDb();
        var left = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).ToListAsync();
        left.Where(c => c.ComponentName is "Túi" or "Nhãn").Should().BeEmpty("thành phần chưa dùng bị xóa");
        left.Single(c => c.ComponentName == "Thân trước").Status.Should().Be("DISCONTINUED", "thành phần đang dùng chỉ bị ngừng dùng");
        (await VariantsSnapshotAsync()).Should().Be(variantsBefore);
    }

    [Fact]
    public async Task IT_B2_155_Save_Spec_Of_Discontinued_Product_Is_Handled_Consistently()
    {
        var r = await PutAsync(SpecUrl(B.Pr3.ProductId), await TkAdm(), Spec([Comp("Tay", "PANEL", 2)], [], [], [], []));
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        Note($"PUT spec cho mã hàng đã ngừng → {r.Code}");
        await using var db = NewDb();
        var has = await db.ProductComponents.AsNoTracking().AnyAsync(c => c.ProductId == B.Pr3.ProductId && c.ComponentName == "Tay");
        has.Should().Be(r.Code == 200, "DB nhất quán với API");
        (await ProductAsync(B.Pr3.ProductId))!.Status.Should().Be("DISCONTINUED");
    }
}
