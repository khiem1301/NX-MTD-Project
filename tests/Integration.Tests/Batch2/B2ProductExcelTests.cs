using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_156–175, 177 — Excel: parse, check, preview (chỉ đọc) và import (1 giao dịch). Ca đồng thời 176 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2ProductExcelTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string P => A + "products/";

    // ------------------------------------------------------------------ builder SpecWorkbookDto

    private static object CompRow(int n, string name, string? type, int? pieces, string? error = null) =>
        new { RowNumber = n, Name = name, ComponentType = type, PiecesPerGarment = pieces, ParseError = error };

    private static object NormRow(int n, string color, string component, string code, string? note = null, string? error = null) =>
        new { RowNumber = n, Color = color, ComponentName = component, MaterialCode = code, SpecNote = note, ParseError = error };

    private static object GroupRow(int n, int? markerNo, string[] components, string[] colors, string? error = null) =>
        new { RowNumber = n, MarkerNo = markerNo, ComponentNames = components, Colors = colors, ParseError = error };

    private static object OrderMarkerRow(int n, int? markerNo, string code, decimal? length, decimal? width, string status, string? error = null) =>
        new
        {
            RowNumber = n, ProductCode = "SP001", MarkerNo = markerNo, MarkerCode = code, LengthM = length, WidthM = width, Status = status,
            Ratios = new[] { new { SizeCode = "M", PiecesPerPly = 2 }, new { SizeCode = "L", PiecesPerPly = 1 } }, ParseError = error,
        };

    private static object Workbook(object[]? components = null, object[]? norms = null, object[]? groups = null, object[]? markers = null,
        string[]? sizes = null) => new
    {
        HasComponents = components is not null, Components = components ?? [],
        HasNorms = norms is not null, Norms = norms ?? [],
        HasMarkerGroups = groups is not null, MarkerGroups = groups ?? [],
        HasOrderMarkers = markers is not null, OrderMarkers = markers ?? [],
        SizeColumns = sizes ?? [],
    };

    private static object Bundle(int? productId, int? orderId, object spec, bool replace = false) => new
    {
        ProductId = productId, OrderId = orderId, Replace = replace,
        Bundle = new { HasProducts = false, Products = Array.Empty<object>(), Spec = spec, HasOrderMaterials = false, OrderMaterials = Array.Empty<object>() },
    };

    private object[] AllComponents() =>
    [
        CompRow(1, "Thân trước", "PANEL", 1), CompRow(2, "Túi", "PANEL", 2), CompRow(3, "Nhãn", "TRIM", 1),
    ];

    private async Task<(int Components, int Norms, int Variants, int Markers)> CountsAsync()
    {
        await using var db = NewDb();
        return (await db.ProductComponents.AsNoTracking().CountAsync(c => c.ProductId == B.Pr1.ProductId),
            await db.ProductComponentMaterials.AsNoTracking().CountAsync(n => n.ProductId == B.Pr1.ProductId),
            await db.ProductVariants.AsNoTracking().CountAsync(v => v.ProductId == B.Pr1.ProductId),
            await db.ProductMarkers.AsNoTracking().CountAsync(m => m.ProductId == B.Pr1.ProductId));
    }

    /// <summary>Số dòng được ghi khi import = dòng New / Update của preview (Unchanged không ghi, Error chặn nhập).</summary>
    private static int RowsOk(JsonElement data, string table) =>
        B1Json.Prop(data, table)!.Value.EnumerateArray().Count(r => B1Json.Str(r, "action") is "New" or "Update");

    // ------------------------------------------------------------------ parse / check

    [Fact]
    public async Task IT_B2_156_Parse_Valid_Workbook_Returns_Rows_Without_Writing()
    {
        var token = await TkAdm();
        var file = await SpecOkAsync();
        var before = await SnapshotB2Async();
        var r = await UploadXlsxAsync(P + "excel/parse", token, file, "spec_ok.xlsx");
        r.Code.Should().Be(200, r.ToString());
        r.Data.ValueKind.Should().Be(JsonValueKind.Object, "trả các dòng đã parse");
        await AssertB2UnchangedAsync(before, "snapshot toàn bộ bảng Product / Spec / Material");
    }

    [Fact]
    public async Task IT_B2_157_Parse_Invalid_Files_Return_400()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        foreach (var (bytes, name) in new[] { (CorruptXlsx(), "corrupt.xlsx"), (PdfBytes(), "not_excel.pdf") })
        {
            var r = await UploadXlsxAsync(P + "excel/parse", token, bytes, name);
            r.Code.Should().Be(400, $"{name}: {r}");
            r.ShouldNotBeServerError();
        }

        var none = await UploadXlsxAsync(P + "excel/parse", token, null);
        none.Code.Should().Be(400, none.ToString());
        none.ShouldNotBeServerError();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_158_Check_Reports_Missing_Material_Without_Creating_It()
    {
        var materials = await CountAllAsync("Material");
        var spec = Workbook(norms: [NormRow(1, "Đen", "Thân trước", "V-9999")]);
        var r = await PostAsync(P + "excel/check", await TkAdm(), Bundle(B.Pr1.ProductId, null, spec));
        r.Code.Should().Be(200, r.ToString());
        var missing = B1Json.Prop(r.Data, "missingMaterials")!.Value.EnumerateArray().ToList();
        var item = missing.Single(m => B1Json.Str(m, "materialCode") == "V-9999");
        B1Json.Str(item, "suggestedType").Should().BeOneOf("FABRIC", "TRIM");
        B1Json.Prop(item, "colors")!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        (await CountAllAsync("Material")).Should().Be(materials, "không tự tạo NVL");
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().AnyAsync(m => m.MaterialCode == "V-9999")).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B2_159_Check_With_Unknown_Product_Or_Order_Is_Rejected_Cleanly()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var spec = Workbook(components: AllComponents());
        var missing = await MissingIdAsync();
        var product = await PostAsync(P + "excel/check", token, Bundle(missing, null, spec));
        product.Code.Should().BeOneOf(new[] { 400, 404 }, product.ToString());
        var order = await PostAsync(P + "excel/check", token, Bundle(B.Pr1.ProductId, missing, spec));
        order.Code.Should().BeOneOf(new[] { 400, 404 }, order.ToString());
        await AssertB2UnchangedAsync(before);
    }

    // ------------------------------------------------------------------ preview (chỉ đọc)

    [Fact]
    public async Task IT_B2_160_Preview_Valid_File_Has_No_Errors_And_Writes_Nothing()
    {
        var token = await TkAdm();
        var file = await SpecOkAsync();
        var before = await SnapshotB2Async();
        var r = await UploadXlsxAsync(P + $"{B.Pr1.ProductId}/import/preview?replace=false", token, file, "spec_ok.xlsx");
        r.Code.Should().Be(200, r.ToString());
        B1Json.Bool(r.Data, "canImport").Should().BeTrue(r.Body);
        B1Json.Int(r.Data, "errorCount").Should().Be(0);
        foreach (var table in new[] { "components", "norms", "markerGroups" })
            foreach (var row in B1Json.Prop(r.Data, table)!.Value.EnumerateArray())
                B1Json.Str(row, "action").Should().BeOneOf("New", "Update", "Unchanged");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_161_Preview_Reports_Row_And_Cross_Errors()
    {
        var before = await SnapshotB2Async();
        var workbook = Workbook(
            components: [CompRow(1, "Thân trước", "PANEL", 1), CompRow(2, "Lỗi", null, null, "Loại chi tiết không hợp lệ")],
            norms: [NormRow(1, "Đen", "Chi tiết không có", "V-0001")]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import/preview-data", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Bool(r.Data, "canImport").Should().BeFalse();
        B1Json.Int(r.Data, "errorCount").Should().BeGreaterThanOrEqualTo(1);
        B1Json.Prop(r.Data, "crossErrors")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        B1Json.Prop(r.Data, "components")!.Value.EnumerateArray().Select(c => B1Json.Str(c, "action")).Should().Contain("Error");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_162_Preview_Replace_Counts_Removed_Rows_But_Does_Not_Delete()
    {
        var before = await SnapshotB2Async();
        var workbook = Workbook(components: [CompRow(1, "Thân trước", "PANEL", 1), CompRow(2, "Nhãn", "TRIM", 1)]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import/preview-data?replace=true&parts=components", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removedCount").Should().Be(1, "chỉ \"Túi\" có trong DB mà không có trong file");
        await AssertB2UnchangedAsync(before, "preview không được xóa thật");
    }

    [Fact]
    public async Task IT_B2_163_Parts_Parameter_Is_Normalized()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var workbook = Workbook(
            components: AllComponents(), norms: [NormRow(1, "Đen", "Thân trước", "V-0001")],
            groups: [GroupRow(1, 1, ["Thân trước"], ["Đen"])]);
        var url = P + $"{B.Pr1.ProductId}/import/preview-data";

        var some = await PostAsync(url + "?parts=" + Uri.EscapeDataString(" Norms , ,NORMS,components"), token, workbook);
        some.Code.Should().Be(200, some.ToString());
        B1Json.Prop(some.Data, "components")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        B1Json.Prop(some.Data, "norms")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        B1Json.Prop(some.Data, "markerGroups")!.Value.GetArrayLength().Should().Be(0, "chỉ preview norms + components");

        var empty = await PostAsync(url + "?parts=", token, workbook);
        empty.Code.Should().Be(200, empty.ToString());
        B1Json.Prop(empty.Data, "markerGroups")!.Value.GetArrayLength().Should().BeGreaterThan(0, "parts rỗng = mọi phần");

        var unknown = await PostAsync(url + "?parts=abc", token, workbook);
        unknown.ShouldNotBeServerError();
        unknown.Code.Should().BeOneOf(new[] { 200, 400 }, unknown.ToString());
        Note($"parts=abc → {unknown.Code}");
        await AssertB2UnchangedAsync(before);
    }

    // ------------------------------------------------------------------ import (1 giao dịch)

    [Fact]
    public async Task IT_B2_164_Import_Valid_File_Matches_Preview()
    {
        var token = await TkAdm();
        var file = await SpecOkAsync();
        var preview = await UploadXlsxAsync(P + $"{B.Pr1.ProductId}/import/preview?replace=false", token, file, "spec_ok.xlsx");
        preview.Code.Should().Be(200, preview.ToString());
        var lastAudit = await LastAuditIdAsync();
        var variantsBefore = (await CountsAsync()).Variants;

        var r = await UploadXlsxAsync(P + $"{B.Pr1.ProductId}/import?replace=false", token, file, "spec_ok.xlsx");
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "components").Should().Be(RowsOk(preview.Data, "components"), "khớp preview");
        B1Json.Int(r.Data, "norms").Should().Be(RowsOk(preview.Data, "norms"));
        B1Json.Int(r.Data, "markerGroups").Should().Be(RowsOk(preview.Data, "markerGroups"));
        B1Json.Int(r.Data, "removed").Should().Be(0);
        (await CountsAsync()).Variants.Should().Be(variantsBefore, "ProductVariant không đổi");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_165_Import_File_With_Error_Row_Rolls_Back_Whole_Import()
    {
        var token = await TkAdm();
        var file = await SpecErrAsync();
        var before = await SnapshotB2Async();
        var r = await UploadXlsxAsync(P + $"{B.Pr1.ProductId}/import?replace=false", token, file, "spec_err.xlsx");
        r.Code.Should().BeOneOf(new[] { 400, 409 }, r.ToString());
        r.ShouldNotBeServerError();
        r.Body.ToLowerInvariant().Should().ContainAny("rownumber", "dòng", "row");
        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().AnyAsync(c => c.ProductId == B.Pr1.ProductId && c.ComponentName == "Túi mới"))
            .Should().BeFalse("không nhập dòng hợp lệ nào");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_166_Import_Data_With_Unknown_Material_Code_Is_Rejected()
    {
        var before = await SnapshotB2Async();
        var workbook = Workbook(components: AllComponents(), norms: [NormRow(1, "Đen", "Thân trước", "V-9999")]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data", await TkAdm(), workbook);
        r.Code.Should().Be(400, r.ToString());
        await using var db = NewDb();
        (await db.Materials.AsNoTracking().AnyAsync(m => m.MaterialCode == "V-9999")).Should().BeFalse("không tự tạo Material V-9999");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_167_Import_Replace_Deletes_Unused_And_Retires_Used_Components()
    {
        var workbook = Workbook(components: [CompRow(1, "Nhãn", "TRIM", 1), CompRow(2, "Cổ", "PANEL", 1)]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?replace=true&parts=components", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removed").Should().Be(2);
        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().AnyAsync(c => c.ProductComponentId == B.Pr1Pocket.ProductComponentId)).Should().BeFalse("\"Túi\" bị DELETE");
        var front = await db.ProductComponents.AsNoTracking().SingleOrDefaultAsync(c => c.ProductComponentId == B.Pr1Front.ProductComponentId);
        front.Should().NotBeNull();
        front!.Status.Should().Be("DISCONTINUED");
        (await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderId == B.Od1.OrderId)).Status.Should().Be(B.Od1.Status);
        (await db.OrderLines.AsNoTracking().CountAsync(l => l.OrderId == B.Od1.OrderId)).Should().Be(B.Od1Lines.Count);
    }

    [Fact]
    public async Task IT_B2_168_Import_Without_Replace_Keeps_Existing_Components()
    {
        var workbook = Workbook(components: [CompRow(1, "Nhãn", "TRIM", 1), CompRow(2, "Cổ", "PANEL", 1)]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?replace=false&parts=components", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removed").Should().Be(0);
        await using var db = NewDb();
        var rows = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == B.Pr1.ProductId).ToListAsync();
        rows.Select(c => c.ComponentName).Should().BeEquivalentTo("Thân trước", "Túi", "Nhãn", "Cổ");
        rows.Should().OnlyContain(c => c.Status == "ACTIVE");
    }

    [Fact]
    public async Task IT_B2_169_Import_Parts_Norms_Only_Changes_Norms()
    {
        var before = await CountsAsync();
        var componentsBefore = await SnapshotComponentsAsync();
        var workbook = Workbook(components: [.. AllComponents(), CompRow(4, "Cổ", "PANEL", 1)], norms: [NormRow(1, "Đen", "Thân trước", "V-0002")]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?parts=norms", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        (await SnapshotComponentsAsync()).Should().Be(componentsBefore, "ProductComponent không đổi");
        await using var db = NewDb();
        (await db.ProductMarkerComponents.AsNoTracking().CountAsync(m => m.ProductId == B.Pr1.ProductId)).Should().Be(0);
        var norms = await db.ProductComponentMaterials.AsNoTracking().Where(n => n.ProductId == B.Pr1.ProductId && n.Status == "ACTIVE")
            .Select(n => n.MaterialId).ToListAsync();
        norms.Should().Contain(B.Ma2.MaterialId, "chỉ phần Norms được xử lý");
        (await CountsAsync()).Components.Should().Be(before.Components);
    }

    private async Task<string> SnapshotComponentsAsync()
    {
        await using var db = NewDb();
        return JsonSerializer.Serialize(await db.ProductComponents.AsNoTracking().OrderBy(c => c.ProductComponentId)
            .Select(c => new { c.ProductComponentId, c.ProductId, c.ComponentName, c.ComponentType, c.PiecesPerGarment, c.Status }).ToListAsync());
    }

    [Fact]
    public async Task IT_B2_170_Order_Markers_Without_Order_Id_Are_Skipped_With_Note()
    {
        var before = await CountsAsync();
        var workbook = Workbook(
            groups: [GroupRow(1, 1, ["Thân trước"], ["Đen"])],
            markers: [OrderMarkerRow(1, 1, "1A", 2.5m, 1.5m, "")], sizes: ["M", "L"]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        var notes = B1Json.Prop(r.Data, "notes");
        (notes is { ValueKind: JsonValueKind.Array } n && n.GetArrayLength() > 0 || r.Body.Contains("bỏ qua", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue($"phải có ghi chú bảng bìa của đơn bị bỏ qua. Body: {r.Body}");
        (await CountsAsync()).Markers.Should().Be(before.Markers, "không có thay đổi ở bảng bìa cắt của đơn");
    }

    [Fact]
    public async Task IT_B2_171_Import_Order_Markers_For_Matching_Order_Creates_Active_Markers()
    {
        var workbook = Workbook(
            groups: [GroupRow(1, 1, ["Thân trước"], ["Đen"])],
            markers: [OrderMarkerRow(1, 1, "1A", 2.5m, 1.5m, "")], sizes: ["M", "L"]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?orderId={B.Od1.OrderId}", await TkAdm(), workbook);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "orderMarkers").Should().BeGreaterThan(0);
        await using var db = NewDb();
        var markers = await db.ProductMarkers.AsNoTracking().Where(m => m.OrderId == B.Od1.OrderId && m.ProductId == B.Pr1.ProductId).ToListAsync();
        markers.Should().NotBeEmpty();
        markers.Should().OnlyContain(m => m.Status == "ACTIVE", "Status để trống = ACTIVE");
    }

    [Fact]
    public async Task IT_B2_172_Import_With_Order_Of_Another_Product_Is_Rejected()
    {
        var od2 = await SeedOd2Async();
        var before = await SnapshotB2Async();
        var workbook = Workbook(
            groups: [GroupRow(1, 1, ["Thân trước"], ["Đen"])],
            markers: [OrderMarkerRow(1, 1, "1A", 2.5m, 1.5m, "")], sizes: ["M", "L"]);
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?orderId={od2.OrderId}", await TkAdm(), workbook);
        r.Code.Should().BeOneOf(new[] { 400, 404 }, r.ToString());
        r.Body.Should().NotContain(od2.OrderCode, "không lộ thông tin của OD2");
        await AssertB2UnchangedAsync(before, "không có thay đổi ở OD2 hay PR1");
    }

    [Fact]
    public async Task IT_B2_173_Import_Data_With_Missing_Order_Or_Product_Returns_404()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var missing = await MissingIdAsync();
        var workbook = Workbook(components: AllComponents());
        (await PostAsync(P + $"{B.Pr1.ProductId}/import-data?orderId={missing}", token, workbook)).Code.Should().Be(404);
        (await PostAsync(P + $"{missing}/import-data", token, workbook)).Code.Should().Be(404);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_174_Invalid_Order_Marker_Fields_Are_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var cases = new[]
        {
            OrderMarkerRow(1, 1, "1A", 2.5m, 1.5m, "Active"),
            OrderMarkerRow(1, 1, "1A", 2.5m, 1.5m, "PAUSED"),
            OrderMarkerRow(1, 1, "1A", -1m, 1.5m, ""),
            OrderMarkerRow(1, 1, "1A", 2.5m, 100.00m, ""),
        };
        foreach (var marker in cases)
        {
            var workbook = Workbook(groups: [GroupRow(1, 1, ["Thân trước"], ["Đen"])], markers: [marker], sizes: ["M", "L"]);
            var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?orderId={B.Od1.OrderId}", token, workbook);
            r.Code.Should().Be(400, r.ToString());
            r.ShouldNotBeServerError();
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_175_Empty_Workbook_Never_Deletes_Data_Even_With_Replace()
    {
        var before = await SnapshotB2Async();
        var r = await PostAsync(P + $"{B.Pr1.ProductId}/import-data?replace=true", await TkAdm(), Workbook());
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(new[] { 200, 400 }, r.ToString());
        Note($"Workbook rỗng → {r.Code}");
        if (r.Code == 200)
            foreach (var key in new[] { "components", "norms", "markerGroups", "orderMarkers" })
                B1Json.Int(r.Data, key).Should().Be(0, key);
        await AssertB2UnchangedAsync(before, "file rỗng không được xóa dữ liệu kể cả replace=true");
    }

    [Fact]
    public async Task IT_B2_177_Import_Oversized_Or_Corrupt_File_Is_Rejected()
    {
        var token = await TkAdm();
        var before = await SnapshotB2Async();
        var url = P + $"{B.Pr1.ProductId}/import";
        var big = await UploadAsync(url, token, Jpeg(6_291_457), "img_over.jpg", "image/jpeg");
        big.Code.Should().BeOneOf(new[] { 400, 413 }, big.ToString());
        big.ShouldNotBeServerError();
        var corrupt = await UploadXlsxAsync(url, token, CorruptXlsx(), "corrupt.xlsx");
        corrupt.Code.Should().BeOneOf(new[] { 400, 413 }, corrupt.ToString());
        corrupt.ShouldNotBeServerError();
        await AssertB2UnchangedAsync(before);
    }
}
