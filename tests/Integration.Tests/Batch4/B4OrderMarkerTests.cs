using System.Globalization;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>IT_B4_097–128 — M/order-markers: bìa của đơn, WidthM [Đ2], Status [Đ3], trigger [Đ4], upload Excel [Đ6].</summary>
public sealed class B4OrderMarkerTests(ITestOutputHelper output) : B4TestBase(output)
{
    private static decimal Dec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private Task<Api> Save(CustomerOrder od, params object[] markers) => Mgr(Put, MarkersUrl(od), SaveMarkers(markers));

    private async Task<List<ProductMarker>> Od1MarkersAsync() => await MarkersAsync(D.Od1.OrderId, B.Pr1.ProductId);

    private async Task AssertOutcomeAsync(Api r, string expected)
    {
        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        var allowed = expected.Split('|').Select(int.Parse).SelectMany(c => c == 200 ? new[] { 200, 201 } : [c]).ToArray();
        r.Code.Should().BeOneOf(allowed, r.ToString());
        (await Od1MarkersAsync()).Any().Should().Be(r.Code is 200 or 201);
    }

    private static object Workbook(object[]? comps = null, object[]? markers = null) => new
    {
        HasComponents = comps is not null, Components = comps ?? Array.Empty<object>(),
        HasNorms = false, Norms = Array.Empty<object>(), HasMarkerGroups = false, MarkerGroups = Array.Empty<object>(),
        HasOrderMarkers = markers is not null, OrderMarkers = markers ?? Array.Empty<object>(), SizeColumns = new[] { "M", "L" },
    };

    private static object XMarker(string code, int no, decimal len, decimal? wid, int row = 1) => new
    {
        RowNumber = row, ProductCode = (string?)null, MarkerNo = (int?)no, MarkerCode = code, LengthM = (decimal?)len, WidthM = wid, Status = "ACTIVE",
        Ratios = new[] { new { SizeCode = "M", PiecesPerPly = 1 }, new { SizeCode = "L", PiecesPerPly = 1 } }, ParseError = (string?)null,
    };

    // ---------------------------------------------------------------- 097–100 đọc / lưu / Status

    [Fact]
    public async Task IT_B4_097_Get_Markers_And_Product_Spec()
    {
        var before = await Snap();

        var spec = await Mgr(Get, M + $"order-markers/product-spec/{B.Pr1.ProductId}");
        var r = await Mgr(Get, M + $"order-markers/{D.Od2.OrderId}");

        spec.Code.Should().Be(200, spec.ToString());
        r.Code.Should().Be(200, r.ToString());
        var marker = r.Body.Contains("S1") ? FindMarker(r, "S1") : throw new Xunit.Sdk.XunitException("OD2 phải có bìa S1: " + r.Body);
        Note($"Status của bìa = {B1Json.Str(marker, "status")}");
        B1Json.Prop(marker, "ratios")!.Value.EnumerateArray()
            .Should().ContainSingle(x => B1Json.Str(x, "sizeCode") == "M" && B1Json.Int(x, "piecesPerPly") == 2);
        await Same(before);
    }

    private static System.Text.Json.JsonElement FindMarker(Api r, string code)
    {
        foreach (var p in B1Json.Prop(r.Data, "products")!.Value.EnumerateArray())
            foreach (var m in B1Json.Prop(p, "markers")!.Value.EnumerateArray())
                if (B1Json.Str(m, "markerCode") == code) return m;
        throw new Xunit.Sdk.XunitException($"không có bìa {code}: {r.Body}");
    }

    [Fact]
    public async Task IT_B4_098_Save_Stores_Status_In_Upper_Case_With_Ratios()
    {
        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m, "Active"));

        r.Code.Should().Be(200, r.ToString());
        var m = (await Od1MarkersAsync()).Single();
        (m.MarkerNo, m.MarkerCode, m.LengthM, m.WidthM).Should().Be(((byte)1, "S1", 6.00m, 1.50m));
        m.Status.Should().Be("ACTIVE").And.NotBe("Active");
        (await RatiosAsync(m.ProductMarkerId)).Select(x => (x.SizeCode, x.PiecesPerPly)).Should().BeEquivalentTo([("M", (short)2), ("L", (short)1)]);
    }

    [Theory]
    [InlineData("IT_B4_099", "ACTIVE", "ACTIVE")]
    [InlineData("IT_B4_099", "active", "ACTIVE")]
    [InlineData("IT_B4_099", "Discontinued", "DISCONTINUED")]
    [InlineData("IT_B4_099", "DISCONTINUED", "DISCONTINUED")]
    public async Task Status_Valid_Values_Are_Stored_Upper_Case(string caseId, string input, string stored)
    {
        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m, input));

        r.Code.Should().Be(200, r.ToString());
        (await Od1MarkersAsync()).Single().Status.Should().Be(stored);
    }

    [Theory]
    [InlineData("IT_B4_100", "Paused")]
    [InlineData("IT_B4_100", "")]
    [InlineData("IT_B4_100", null)]
    public async Task Status_Invalid_Values_Are_Rejected_Or_Defaulted(string caseId, string? input)
    {
        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m, input!));

        r.ShouldNotBeServerError();
        if (input == "Paused") Rejected(r, 400);
        else r.Code.Should().BeOneOf(200, 201, 400);
        await using var db = NewDb();
        (await db.ProductMarkers.AsNoTracking().Select(m => m.Status).Distinct().ToListAsync()).Should().BeSubsetOf(["ACTIVE", "DISCONTINUED"]);
    }

    // ---------------------------------------------------------------- 101–110 WidthM, LengthM, MarkerNo, MarkerCode

    [Theory]
    [InlineData("IT_B4_101", "3.50", "200")]
    [InlineData("IT_B4_102", "3.51", "400")]
    [InlineData("IT_B4_103", "99.99", "400")]
    [InlineData("IT_B4_104", "100.00", "400")]
    [InlineData("IT_B4_105", "0", "400")]
    [InlineData("IT_B4_105", "-0.01", "400")]
    [InlineData("IT_B4_105", "0.01", "200")]
    [InlineData("IT_B4_107", "3.505", "400")]
    [InlineData("IT_B4_107", "1.555", "400|200")]
    public async Task WidthM_Boundaries(string caseId, string width, string expected)
    {
        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, Dec(width)));

        await AssertOutcomeAsync(r, expected);
        await using var db = NewDb();
        (await db.ProductMarkers.AsNoTracking().CountAsync(m => m.OrderId == D.Od1.OrderId && m.WidthM > 3.50m)).Should().Be(0, "WidthM ≤ 3,5 m");
        if (r.Code is 200 or 201 && width == "3.50") (await Od1MarkersAsync()).Single().WidthM.Should().Be(3.50m);
    }

    [Fact]
    public async Task IT_B4_106_Null_WidthM_Returns_400()
    {
        var before = await Snap();

        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, null));

        Rejected(r, 400);
        await Same(before);
    }

    [Theory]
    [InlineData("IT_B4_108", "99999.99", "200|400")]
    [InlineData("IT_B4_108", "100000.00", "400")]
    [InlineData("IT_B4_108", "0", "400")]
    [InlineData("IT_B4_108", "-1", "400")]
    public async Task LengthM_Boundaries(string caseId, string length, string expected) =>
        await AssertOutcomeAsync(await Save(D.Od1, MarkerIn("S1", 1, Dec(length), 1.50m)), expected);

    [Theory]
    [InlineData("IT_B4_109", 0, "400")]
    [InlineData("IT_B4_109", 255, "200|400")]
    [InlineData("IT_B4_109", 256, "400")]
    [InlineData("IT_B4_109", -1, "400")]
    public async Task MarkerNo_Boundaries(string caseId, int no, string expected) =>
        await AssertOutcomeAsync(await Save(D.Od1, MarkerIn("S1", no, 6.00m, 1.50m)), expected);

    [Theory]
    [InlineData("IT_B4_110", 20, "200")]
    [InlineData("IT_B4_110", 21, "400")]
    [InlineData("IT_B4_110", 0, "400")]
    public async Task MarkerCode_Length_Boundaries(string caseId, int length, string expected) =>
        await AssertOutcomeAsync(await Save(D.Od1, MarkerIn(new string('A', length), 1, 6.00m, 1.50m)), expected);

    [Fact]
    public async Task IT_B4_111_Duplicate_Marker_In_Request_Is_Rejected()
    {
        var before = await Snap();

        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m), MarkerIn("S1", 1, 7.00m, 1.50m));

        Rejected(r, 400, 409);
        await Same(before);
    }

    // ---------------------------------------------------------------- 112–118 trigger / rollback / IDOR

    [Fact]
    public async Task IT_B4_112_Ratio_Size_Not_In_Order_Is_Rejected_And_Rolled_Back()
    {
        var before = await Snap();

        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m, ratios: [RatioIn("M", 1), RatioIn("XXL", 1)]));

        Rejected(r, 400, 409);
        r.Body.Should().NotContainAny("trg_ProductMarker", "trigger", "Trigger");
        (await Od1MarkersAsync()).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_113_Invalid_Ratio_Lists_Are_Rejected()
    {
        var before = await Snap();
        var bad = new object[][]
        {
            [RatioIn("M", 1), RatioIn("M", 2)], [RatioIn("M", 0)], [RatioIn("M", -1)], [RatioIn("M", 32768)], [],
        };

        foreach (var ratios in bad) Rejected(await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m, ratios: ratios)), 400, 409);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_114_One_Invalid_Marker_Rolls_Back_The_Valid_One()
    {
        var before = await Snap();

        var r = await Save(D.Od1, MarkerIn("S1", 1, 6.00m, 1.50m), MarkerIn("S2", 1, 6.00m, 4.00m));

        Rejected(r, 400, 409);
        (await Od1MarkersAsync()).Should().BeEmpty("rollback toàn bộ");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_115_Marker_Not_In_List_Is_Removed_Or_Discontinued()
    {
        var r = await Save(D.Od2, MarkerIn("S3", 1, 6.00m, 1.50m));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 409);
        var markers = await MarkersAsync(D.Od2.OrderId, B.Pr1.ProductId);
        if (r.Code == 200)
        {
            markers.Should().Contain(m => m.MarkerCode == "S3");
            var old = markers.SingleOrDefault(m => m.ProductMarkerId == D.Mk1.ProductMarkerId);
            if (old is null) (await RatiosAsync(D.Mk1.ProductMarkerId)).Should().BeEmpty();
            else old.Status.Should().Be("DISCONTINUED");
        }
        else markers.Should().ContainSingle(m => m.ProductMarkerId == D.Mk1.ProductMarkerId);
    }

    [Fact]
    public async Task IT_B4_116_Update_Existing_Marker_Changes_Length_And_Ratio()
    {
        var r = await Save(D.Od2, MarkerIn("S1", 1, 5.50m, 1.50m, ratios: [RatioIn("M", 3)], id: D.Mk1.ProductMarkerId));

        r.Code.Should().Be(200, r.ToString());
        (await MarkersAsync(D.Od2.OrderId, B.Pr1.ProductId)).Single(m => m.ProductMarkerId == D.Mk1.ProductMarkerId).LengthM.Should().Be(5.50m);
        (await RatiosAsync(D.Mk1.ProductMarkerId)).Should().ContainSingle(x => x.SizeCode == "M" && x.PiecesPerPly == 3);
    }

    [Fact]
    public async Task IT_B4_117_Marker_Of_Another_Order_Cannot_Be_Edited()
    {
        var before = await Snap();

        var r = await Save(D.Od1, MarkerIn("S1", 1, 9.99m, 1.50m, id: D.Mk1.ProductMarkerId));

        Rejected(r, 400, 404);
        var mk = (await MarkersAsync(D.Od2.OrderId, B.Pr1.ProductId)).Single(m => m.ProductMarkerId == D.Mk1.ProductMarkerId);
        mk.OrderId.Should().Be(D.Od2.OrderId);
        mk.LengthM.Should().Be(5.20m);
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_118_Product_Not_In_Order_Or_Unknown_Order_Is_Rejected()
    {
        var before = await Snap();

        Rejected(await Save(D.Od4, MarkerIn("S1", 1, 6.00m, 1.50m)), 400, 404);
        Rejected(await Mgr(Put, M + $"order-markers/{await MissingIdAsync()}/products/{B.Pr1.ProductId}", SaveMarkers(MarkerIn("S1", 1, 6.00m, 1.50m))), 404);

        (await MarkersAsync(D.Od4.OrderId, B.Pr1.ProductId)).Should().BeEmpty();
        await Same(before);
    }

    // ---------------------------------------------------------------- 120–128 Excel [Đ6]

    [Fact]
    public async Task IT_B4_120_Parse_And_Check_Do_Not_Write()
    {
        var token = await TkMgr();
        var file = await MarkerFileAsync(D.Od1, (1, "S2", 6.00m, 1.50m));
        var before = await Snap();

        var parse = await UploadXlsxAsync(M + "order-markers/excel/parse", token, file, "marker_ok.xlsx");
        parse.Code.Should().Be(200, parse.ToString());
        var body = $"{{\"ProductId\":{B.Pr1.ProductId},\"OrderId\":{D.Od1.OrderId},\"Bundle\":{parse.Data.GetRawText()}}}";
        var check = await Mgr(Post, M + "order-markers/excel/check", body);

        check.Code.Should().Be(200, check.ToString());
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_121_Missing_File_Returns_400()
    {
        var token = await TkMgr();
        var before = await Snap();
        var p = $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}";

        foreach (var url in new[] { M + "order-markers/excel/parse", p + "/import/preview", p + "/import" })
            Rejected(await UploadXlsxAsync(url, token, null), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_122_Broken_Files_Return_400()
    {
        var token = await TkMgr();
        var before = await Snap();
        var p = $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}";
        var files = new (byte[] Bytes, string Name, string Type)[]
        {
            (ZeroXlsx(), "zero.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            (CorruptXlsx(), "corrupt.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            (PdfBytes(), "not_excel.pdf", "application/pdf"),
        };

        foreach (var (bytes, name, type) in files)
            foreach (var url in new[] { M + "order-markers/excel/parse", p + "/import" })
                Rejected(await UploadAsync(url, token, bytes, name, type), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_123_Import_Preview_Does_Not_Write()
    {
        var token = await TkMgr();
        var file = await MarkerFileAsync(D.Od1, (1, "S2", 6.00m, 1.50m));
        var before = await Snap();

        var r = await UploadXlsxAsync($"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import/preview", token, file, "marker_ok.xlsx");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Bool(r.Data, "canImport").Should().BeTrue();
        var actions = B1Json.Prop(r.Data, "order-markers")!.Value.EnumerateArray().Select(x => B1Json.Str(x, "action")).ToList();
        actions.Should().NotBeEmpty().And.OnlyContain(a => a == "New" || a == "Update" || a == "Unchanged");
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_124_Import_With_Invalid_Row_Rolls_Back_Everything()
    {
        var token = await TkMgr();
        var file = await MarkerFileAsync(D.Od1, (1, "S2", 6.00m, 1.50m), (1, "S3", 6.00m, 4.00m), (1, "S4", 6.00m, 1.50m));
        var before = await Snap();

        var r = await UploadXlsxAsync($"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import", token, file, "marker_w4.xlsx");

        Rejected(r, 400);
        (await Od1MarkersAsync()).Should().BeEmpty();
        await Same(before);
    }

    [Fact]
    public async Task IT_B4_125_Import_Replace_Removes_Markers_Missing_From_File()
    {
        await using (var db = NewDb())
        {
            var s9 = new ProductMarker { OrderId = D.Od1.OrderId, ProductId = B.Pr1.ProductId, MarkerNo = 1, MarkerCode = "S9", LengthM = 4.00m, WidthM = 1.40m, Status = "ACTIVE" };
            db.ProductMarkers.Add(s9);
            await db.SaveChangesAsync();
            db.ProductMarkerRatios.Add(new ProductMarkerRatio { ProductMarkerId = s9.ProductMarkerId, SizeCode = "M", PiecesPerPly = 1 });
            await db.SaveChangesAsync();
        }

        var token = await TkMgr();
        var file = await MarkerFileAsync(D.Od1, (1, "S2", 6.00m, 1.50m));
        var url = $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import";

        var keep = await UploadXlsxAsync(url, token, file, "marker_ok.xlsx");
        keep.Code.Should().Be(200, keep.ToString());
        (await Od1MarkersAsync()).Select(m => m.MarkerCode).Should().BeEquivalentTo(["S9", "S2"]);

        var replace = await UploadXlsxAsync(url + "?replace=true", token, file, "marker_ok.xlsx");
        replace.Code.Should().Be(200, replace.ToString());
        var after = await Od1MarkersAsync();
        after.Should().Contain(m => m.MarkerCode == "S2");
        var s9After = after.SingleOrDefault(m => m.MarkerCode == "S9");
        if (s9After is not null) s9After.Status.Should().Be("DISCONTINUED");
    }

    [Fact]
    public async Task IT_B4_126_ImportData_Requires_Markers_Part()
    {
        var before = await Snap();
        var body = Workbook(markers: [XMarker("S2", 1, 6.00m, 1.50m)]);
        var p = $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import-data";

        Rejected(await Mgr(Post, p, body), 400);
        Rejected(await Mgr(Post, p + "?parts=components,norms", body), 400);

        await Same(before);
    }

    [Fact]
    public async Task IT_B4_127_ImportData_Ignores_Parts_Other_Than_Markers()
    {
        var body = Workbook(comps: [new { RowNumber = 1, Name = "Cổ", ComponentType = "PANEL", PiecesPerGarment = (int?)1, ParseError = (string?)null }],
            markers: [XMarker("S2", 1, 6.00m, 1.50m)]);

        var r = await Mgr(Post, $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import-data?parts=markers,components", body);

        r.Code.Should().Be(200, r.ToString());
        (await Od1MarkersAsync()).Should().ContainSingle(m => m.MarkerCode == "S2");
        await using var db = NewDb();
        (await db.ProductComponents.AsNoTracking().CountAsync(c => c.ProductId == B.Pr1.ProductId && c.ComponentName == "Cổ")).Should().Be(0);
    }

    [Fact]
    public async Task IT_B4_128_Parts_Query_Is_Case_Insensitive()
    {
        var body = Workbook(markers: [XMarker("S2", 1, 6.00m, 1.50m)]);

        var r = await Mgr(Post, $"{M}order-markers/{D.Od1.OrderId}/products/{B.Pr1.ProductId}/import-data?parts=MARKERS", body);

        r.Code.Should().Be(200, r.ToString());
        (await Od1MarkersAsync()).Should().ContainSingle(m => m.MarkerCode == "S2");
    }
}

