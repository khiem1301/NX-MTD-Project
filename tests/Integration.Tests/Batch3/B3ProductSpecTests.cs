using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_025–044, 047 — PUT/GET spec: FK ghép theo ProductId, toàn vẹn, biên. 045 / 046 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3ProductSpecTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Get = HttpMethod.Get, Put = HttpMethod.Put;

    private string SpecUrl => A + $"products/{B.Pr1.ProductId}/spec";

    private Task<Api> PutSpec(object body) => Adm(Put, SpecUrl, body);

    private async Task<List<ProductVariant>> VariantsAsync(int productId)
    {
        await using var db = NewDb();
        return await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == productId).ToListAsync();
    }

    private async Task<List<ProductMarkerFabric>> FabricsAsync()
    {
        await using var db = NewDb();
        return await db.ProductMarkerFabrics.AsNoTracking().Where(f => f.ProductId == B.Pr1.ProductId).ToListAsync();
    }

    [Fact]
    public async Task IT_B3_025_Get_Spec_Same_For_Admin_And_Accountant()
    {
        var before = await Snap();
        var admin = await Adm(Get, SpecUrl);
        var acc = await Acc(Get, AC + $"products/{B.Pr1.ProductId}/spec");

        admin.Code.Should().Be(200, admin.ToString());
        acc.Code.Should().Be(200, acc.ToString());
        acc.Data.GetRawText().Should().Be(admin.Data.GetRawText());
        var variants = B1Json.Prop(admin.Data, "variants")!.Value.EnumerateArray().Select(v => B1Json.Str(v, "color") + "/" + B1Json.Str(v, "sizeCode")).ToList();
        variants.Should().BeEquivalentTo(["Đen/M", "Đen/L", "Trắng/M"]);
        var norm = B1Json.Prop(admin.Data, "norms")!.Value.EnumerateArray().Single();
        B1Json.Str(norm, "materialCode").Should().Be("V-0001");
        B1Json.Str(norm, "materialName").Should().Be("Kaki");
        var fabric = B1Json.Prop(admin.Data, "markerFabrics")!.Value.EnumerateArray().Single();
        B1Json.Int(fabric, "markerNo").Should().Be(1);
        B1Json.Str(fabric, "color").Should().Be("Đen");
        B1Json.Int(fabric, "materialId").Should().Be(B.Ma1.MaterialId);
        (await VariantsAsync(B.Pr1.ProductId)).Should().HaveCount(3);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_026_Save_Spec_Adds_Component_Replaces_Norms_And_Keeps_Variants()
    {
        var variantsBefore = (await VariantsAsync(B.Pr1.ProductId)).Select(v => (v.ProductVariantId, v.ProductColorId, v.SizeCode, v.Status)).ToList();
        var colorsBefore = await ColorCountAsync();

        var r = await PutSpec(SpecBody(
            comps: [.. BaseComps(), Comp("Cổ", "PANEL", 1)],
            norms: [Norm("Thân trước", "Đen", B.Ma1.MaterialId), Norm("Cổ", "Đen", B.Ma2.MaterialId), Norm("Thân trước", "Trắng", B.Ma2.MaterialId)],
            mcomps: [MComp(1, "Thân trước"), MComp(1, "Cổ")], mcolors: [MColor(1, "Đen"), MColor(1, "Trắng")]));

        r.Code.Should().Be(200, r.ToString());
        (await CompsOfAsync(B.Pr1.ProductId)).Should().Contain(c => c.ComponentName == "Cổ");
        var norms = await NormsOfAsync(B.Pr1.ProductId);
        norms.Should().HaveCount(3).And.OnlyContain(n => n.Status == "ACTIVE");
        await using var db = NewDb();
        (await db.ProductMarkerComponents.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId)).Should().Be(2);
        (await FabricsAsync()).Select(f => f.ProductColorId).Should().BeEquivalentTo([B.Pr1Black.ProductColorId, P.Pr1White.ProductColorId]);
        (await VariantsAsync(B.Pr1.ProductId)).Select(v => (v.ProductVariantId, v.ProductColorId, v.SizeCode, v.Status)).Should().BeEquivalentTo(variantsBefore);
        (await ColorCountAsync()).Should().Be(colorsBefore);
    }

    private async Task<int> ColorCountAsync()
    {
        await using var db = NewDb();
        return await db.ProductColors.AsNoTracking().CountAsync(c => c.ProductId == B.Pr1.ProductId);
    }

    [Fact]
    public async Task IT_B3_027_Save_Spec_Without_White_Variants_Leaves_White_Untouched()
    {
        Db.ProductComponentMaterials.Add(new ProductComponentMaterial
        {
            ProductId = B.Pr1.ProductId, ProductColorId = P.Pr1White.ProductColorId, ProductComponentId = B.Pr1Front.ProductComponentId,
            MaterialId = B.Ma2.MaterialId, Status = "ACTIVE",
        });
        await Db.SaveChangesAsync();

        var r = await PutSpec(SpecBody(variants: BaseVariants()[..2]));

        r.Code.Should().Be(200, r.ToString());
        var white = (await VariantsAsync(B.Pr1.ProductId)).Single(v => v.ProductColorId == P.Pr1White.ProductColorId);
        white.Status.Should().Be("ACTIVE");
        (await NormsOfAsync(B.Pr1.ProductId)).Should().Contain(n => n.ProductColorId == P.Pr1White.ProductColorId && n.Status == "ACTIVE");
    }

    [Fact]
    public async Task IT_B3_028_Removing_Used_And_Unused_Components_Deletes_Or_Retires()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(comps: [BaseComps()[2]], norms: [], mcomps: [], mcolors: []));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 409);
        if (r.Code == 409) { await AssertSame(before); return; }
        var comps = await CompsOfAsync(B.Pr1.ProductId);
        comps.Should().NotContain(c => c.ProductComponentId == B.Pr1Pocket.ProductComponentId, "C2 chưa dùng bị DELETE");
        var c1 = comps.SingleOrDefault(c => c.ProductComponentId == B.Pr1Front.ProductComponentId);
        c1.Should().NotBeNull("C1 đang dùng không bị DELETE");
        c1!.Status.Should().NotBe("ACTIVE");
        (await OpComponentsAsync(P.Op1.OperationId)).Should().Contain(B.Pr1Front.ProductComponentId);
    }

    [Fact]
    public async Task IT_B3_029_Norm_With_Color_Of_Other_Product_Rolls_Back()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(norms: [Norm("Thân trước", "Xanh", B.Ma1.MaterialId)]));

        Failed(r, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_030_Component_Id_Of_Other_Product_Is_Rejected()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(comps: [.. BaseComps(), Comp("Thân sau", "PANEL", 1, P.C9.ProductComponentId)]));

        Failed(r, 400, 404);
        (await CompAsync(P.C9.ProductComponentId)).ProductId.Should().Be(B.Pr2.ProductId);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_031_Variant_Id_Of_Other_Product_Is_Rejected()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(variants: [.. BaseVariants(),
            new { ProductVariantId = (int?)P.Pr2BlueM.ProductVariantId, Color = "Xanh", SizeCode = "M" }]));

        Failed(r, 400, 404);
        (await ColorCountAsync()).Should().Be(2, "PR1 không có màu Xanh");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_032_Spec_Never_Creates_Variants()
    {
        var r = await PutSpec(SpecBody(variants: [.. BaseVariants(), new { ProductVariantId = (int?)null, Color = "Đen", SizeCode = "XL" }]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        (await VariantsAsync(B.Pr1.ProductId)).Should().NotContain(v => v.SizeCode == "XL");
    }

    [Fact]
    public async Task IT_B3_033_Norm_With_Missing_Or_Discontinued_Material_Is_Rejected()
    {
        var before = await Snap();
        var missing = await PutSpec(SpecBody(norms: [Norm("Thân trước", "Đen", await MissingIdAsync())]));
        var discontinued = await PutSpec(SpecBody(norms: [Norm("Thân trước", "Đen", B.Ma4.MaterialId)]));

        Failed(missing, 400, 404);
        Failed(discontinued, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_034_Duplicate_Norm_In_One_Request_Never_Duplicates_Rows()
    {
        var before = await Snap();
        var norm = Norm("Thân trước", "Đen", B.Ma1.MaterialId);
        var r = await PutSpec(SpecBody(norms: [norm, norm]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        if (r.Code == 400) await AssertSame(before);
        (await NormsOfAsync(B.Pr1.ProductId)).Count(n => n.MaterialId == B.Ma1.MaterialId && n.ProductComponentId == B.Pr1Front.ProductComponentId).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_035_One_Component_Can_Use_Two_Materials()
    {
        var r = await PutSpec(SpecBody(norms: [Norm("Thân trước", "Đen", B.Ma1.MaterialId), Norm("Thân trước", "Đen", B.Ma2.MaterialId)],
            mcomps: [], mcolors: []));

        r.Code.Should().Be(200, r.ToString());
        (await NormsOfAsync(B.Pr1.ProductId)).Count(n => n.ProductColorId == B.Pr1Black.ProductColorId
            && n.ProductComponentId == B.Pr1Front.ProductComponentId).Should().Be(2);
    }

    [Fact]
    public async Task IT_B3_036_Duplicate_Component_Names_Are_Rejected()
    {
        var before = await Snap();
        var dup = await PutSpec(SpecBody(comps: [.. BaseComps(), Comp("Túi", "PANEL", 1)]));
        Failed(dup, 400, 409);
        await AssertSame(before);

        var lower = await PutSpec(SpecBody(comps: [.. BaseComps(), Comp("túi", "PANEL", 1)]));
        lower.ShouldNotBeServerError();
        Note($"tên khác hoa/thường → {lower.Code}");
        if (lower.Code >= 400) await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_037", "name", 100, true)]
    [InlineData("IT_B3_037", "name", 101, false)]
    [InlineData("IT_B3_037", "note", 200, true)]
    [InlineData("IT_B3_037", "note", 201, false)]
    public async Task IT_B3_037_Text_Length_Boundaries(string caseId, string field, int length, bool ok)
    {
        var text = Viet(length);
        var r = field == "name"
            ? await PutSpec(SpecBody(comps: [.. BaseComps(), Comp(text, "PANEL", 1)]))
            : await PutSpec(SpecBody(norms: [Norm("Thân trước", "Đen", B.Ma1.MaterialId, text)]));

        r.ShouldNotBeServerError();
        if (!ok) { r.Code.Should().Be(400, r.ToString()); return; }
        r.Code.Should().Be(200, r.ToString());
        if (field == "name") (await CompsOfAsync(B.Pr1.ProductId)).Should().Contain(c => c.ComponentName == text);
        else (await NormsOfAsync(B.Pr1.ProductId)).Should().Contain(n => n.SpecNote == text);
    }

    [Theory]
    [InlineData("IT_B3_038", "BUTTON")]
    [InlineData("IT_B3_038", "panel")]
    [InlineData("IT_B3_038", "PANELXXXXXX")]
    [InlineData("IT_B3_038", "")]
    public async Task IT_B3_038_ComponentType_Only_Panel_Or_Trim(string caseId, string type)
    {
        var r = await PutSpec(SpecBody(comps: [.. BaseComps(), Comp("Cổ", type, 1)]));

        r.ShouldNotBeServerError();
        if (type == "panel") r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(400, r.ToString());
        (await CompsOfAsync(B.Pr1.ProductId)).Should().OnlyContain(c => c.ComponentType == "PANEL" || c.ComponentType == "TRIM");
    }

    [Theory]
    [InlineData("IT_B3_039", 0, 400)]
    [InlineData("IT_B3_039", -1, 400)]
    [InlineData("IT_B3_039", 1, 200)]
    [InlineData("IT_B3_039", 32767, 0)]
    [InlineData("IT_B3_039", 32768, 400)]
    public async Task IT_B3_039_PiecesPerGarment_Boundaries(string caseId, int pieces, int expected)
    {
        var r = await PutSpec(SpecBody(comps: [.. BaseComps(), Comp("Cổ", "PANEL", pieces)]));

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(expected, r.ToString());
        var stored = (await CompsOfAsync(B.Pr1.ProductId)).SingleOrDefault(c => c.ComponentName == "Cổ");
        if (r.Code == 200) stored.Should().NotBeNull(); else stored.Should().BeNull();
    }

    [Theory]
    [InlineData("IT_B3_040", 0, 400)]
    [InlineData("IT_B3_040", 255, 0)]
    [InlineData("IT_B3_040", 256, 400)]
    [InlineData("IT_B3_040", -1, 400)]
    public async Task IT_B3_040_MarkerNo_Boundaries(string caseId, int no, int expected)
    {
        var r = await PutSpec(SpecBody(mcomps: [MComp(no, "Thân trước")], mcolors: []));

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(expected, r.ToString());
        await using var db = NewDb();
        (await db.ProductMarkerComponents.AsNoTracking().Where(x => x.ProductId == B.Pr1.ProductId).Select(x => (int)x.MarkerNo).ToListAsync())
            .Should().OnlyContain(n => n >= 0 && n <= 255);
    }

    [Fact]
    public async Task IT_B3_041_Marker_Rows_Must_Reference_Existing_Components_And_Colors()
    {
        var before = await Snap();
        var component = await PutSpec(SpecBody(mcomps: [MComp(1, "Tay áo")]));
        var color = await PutSpec(SpecBody(mcolors: [MColor(1, "Xanh")]));

        Failed(component, 400);
        Failed(color, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_042_Marker_Color_Without_Fabric_Norm_Never_Stores_Broken_Fabric()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(norms: []));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        if (r.Code == 400) await AssertSame(before);
        var materialIds = (await FabricsAsync()).Select(f => f.MaterialId).ToList();
        await using var db = NewDb();
        foreach (var id in materialIds) (await db.Materials.AsNoTracking().AnyAsync(m => m.MaterialId == id)).Should().BeTrue();
    }

    [Fact]
    public async Task IT_B3_043_One_Marker_Color_Never_Gets_Two_Fabrics()
    {
        var before = await Snap();
        var r = await PutSpec(SpecBody(
            comps: [.. BaseComps(), Comp("Cổ", "PANEL", 1)],
            norms: [Norm("Thân trước", "Đen", B.Ma1.MaterialId), Norm("Cổ", "Đen", B.Ma2.MaterialId)],
            mcomps: [MComp(1, "Thân trước"), MComp(1, "Cổ")], mcolors: [MColor(1, "Đen")]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        if (r.Code == 400) await AssertSame(before);
        (await FabricsAsync()).Count(f => f.MarkerNo == 1 && f.ProductColorId == B.Pr1Black.ProductColorId).Should().Be(1);
    }

    [Fact]
    public async Task IT_B3_044_Missing_Or_Null_Arrays_Return_400()
    {
        var before = await Snap();
        var missing = await PutSpec(new { Components = BaseComps(), Variants = BaseVariants(), Norms = new object[0], MarkerComponents = new object[0] });
        var nulls = await PutSpec(new { Components = BaseComps(), Variants = BaseVariants(), Norms = new object[0], MarkerComponents = new object[0], MarkerColors = (object?)null });

        Failed(missing, 400);
        Failed(nulls, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_047_Spec_Of_Unknown_Or_Discontinued_Product()
    {
        var before = await Snap();
        var missing = await Adm(Put, A + $"products/{await MissingIdAsync()}/spec", SpecBody());
        var notNumber = await Adm(Put, A + "products/abc/spec", SpecBody());
        var discontinued = await Adm(Put, A + $"products/{B.Pr3.ProductId}/spec", SpecBody());

        Failed(missing, 404);
        Failed(notNumber, 404);
        discontinued.ShouldNotBeServerError();
        discontinued.Code.Should().BeOneOf(200, 400);
        if (discontinued.Code == 400) await AssertSame(before);
    }
}
