using FluentAssertions;
using Integration.Tests.Batch1;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_048–065 — nhập Excel định mức, trigger ProductMarker / ProductMarkerRatio. 066 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3ProductImportTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Post = HttpMethod.Post;

    private string Pid => B.Pr1.ProductId.ToString();
    private string ImportData(string query) => A + $"products/{Pid}/import-data?{query}";
    private string PreviewData(string query) => A + $"products/{Pid}/import/preview-data?{query}";
    private string OrderQ => $"orderId={B.Od1.OrderId}&parts=markers";

    private Task<Api> Import(object[] markers, string? query = null) => Adm(Post, ImportData(query ?? OrderQ), Workbook(markers: markers));

    private static string CheckBody(int? productId, int? orderId, object bundle) =>
        System.Text.Json.JsonSerializer.Serialize(new { ProductId = productId, OrderId = orderId, Bundle = bundle, Replace = false });

    private static object Bundle(object? products = null, object[]? orderMaterials = null, object? spec = null) => new
    {
        HasProducts = products is not null, Products = products ?? Array.Empty<object>(), Spec = spec ?? Workbook(),
        HasOrderMaterials = orderMaterials is not null, OrderMaterials = orderMaterials ?? [],
    };

    // ---------------------------------------------------------------- 048–051 parse / check (chỉ đọc)

    [Fact]
    public async Task IT_B3_048_Parse_Then_Check_Is_Read_Only()
    {
        var before = await Snap();
        var token = await TkAdm();
        var parsed = await UploadXlsxAsync(A + "products/excel/parse", token, await SpecOkAsync());
        parsed.Code.Should().Be(200, parsed.ToString());

        var check = await Adm(Post, A + "products/excel/check", $"{{\"productId\":{Pid},\"bundle\":{parsed.Data.GetRawText()}}}");

        check.Code.Should().Be(200, check.ToString());
        B1Json.Prop(check.Data, "spec").Should().NotBeNull();
        B1Json.Prop(check.Data, "missingMaterials")!.Value.GetArrayLength().Should().Be(0);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_049_Check_Reports_Existing_And_New_Product_Without_Writing()
    {
        var before = await Snap();
        var rows = new[]
        {
            new { RowNumber = 1, ProductCode = "SP001", Description = (string?)null },
            new { RowNumber = 2, ProductCode = "SP400", Description = (string?)null },
        };

        var r = await Adm(Post, A + "products/excel/check", CheckBody(null, null, Bundle(products: rows)));

        r.Code.Should().Be(200, r.ToString());
        FindBy(r.Data, "productCode", "SP001").Should().NotBeNull();
        B1Json.Str(FindBy(r.Data, "productCode", "SP001")!.Value, "action").Should().Be("Exists");
        B1Json.Str(FindBy(r.Data, "productCode", "SP400")!.Value, "action").Should().BeOneOf("New", "Pending");
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Products, p => p.ProductCode == "SP400")).Should().BeFalse();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_050_Check_Order_Materials_Reports_Missing_Material_And_Negative_Qty()
    {
        var before = await Snap();
        var rows = new[]
        {
            new { RowNumber = 1, MaterialCode = "V-9999", RequiredQty = (decimal?)-5m, CustomerQty = (decimal?)null, Note = (string?)null,
                ParseError = (string?)null, MaterialName = (string?)"Vải mới", Unit = (string?)null },
        };

        var r = await Adm(Post, A + "products/excel/check", CheckBody(B.Pr1.ProductId, B.Od1.OrderId, Bundle(orderMaterials: rows)));

        r.Code.Should().Be(200, r.ToString());
        var missing = B1Json.Prop(r.Data, "missingMaterials")!.Value.EnumerateArray().Single();
        B1Json.Str(missing, "materialCode").Should().Be("V-9999");
        B1Json.Str(missing, "suggestedName").Should().Be("Vải mới");
        B1Json.Str(FindBy(r.Data, "materialCode", "V-9999", skip: B1Json.Prop(r.Data, "missingMaterials")!.Value)!.Value, "action").Should().Be("Error");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_051_Wrong_Body_Shape_Returns_400()
    {
        var before = await Snap();
        var bundleToImport = await Adm(Post, ImportData("replace=false"), CheckBody(B.Pr1.ProductId, null, Bundle()));
        var workbookToCheck = await Adm(Post, A + "products/excel/check", Workbook());

        Failed(bundleToImport, 400);
        Failed(workbookToCheck, 400);
        await AssertSame(before);
    }

    // ---------------------------------------------------------------- 052–062 bìa của đơn

    [Fact]
    public async Task IT_B3_052_Preview_Order_Marker_Does_Not_Touch_Db()
    {
        var before = await Snap();
        var r = await Adm(Post, PreviewData(OrderQ), Workbook(markers: [OMarker("S2", 2, 6.00m, 1.50m)]));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(B1Json.Prop(r.Data, "orderMarkers")!.Value.EnumerateArray().Single(), "action").Should().Be("New");
        B1Json.Bool(r.Data, "canImport").Should().BeTrue();
        B1Json.Prop(r.Data, "orderSizes")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_053_Import_Order_Marker_Stores_Marker_And_Ratios()
    {
        var r = await Import([OMarker("S2", 2, 6.00m, 1.50m)]);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "orderMarkers").Should().Be(1);
        var marker = (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Single(m => m.MarkerCode == "S2");
        (marker.MarkerNo, marker.LengthM, marker.WidthM, marker.Status).Should().Be(((byte)2, 6.00m, 1.50m, "ACTIVE"));
        (await RatiosAsync(marker.ProductMarkerId)).Select(x => (x.SizeCode, x.PiecesPerPly)).Should().BeEquivalentTo([("M", (short)1), ("L", (short)1)]);
    }

    [Theory]
    [InlineData("IT_B3_054", "3.50", 200)]
    [InlineData("IT_B3_054", "3.51", 400)]
    [InlineData("IT_B3_054", "99.99", 400)]
    [InlineData("IT_B3_054", "100.00", 400)]
    public async Task IT_B3_054_WidthM_Boundaries(string caseId, string width, int expected)
    {
        var r = await Import([OMarker("W1", 1, 5.00m, decimal.Parse(width, System.Globalization.CultureInfo.InvariantCulture))]);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        r.Body.Should().NotContainAny("trg_", "SqlException");
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.ProductMarkers, m => m.WidthM > 3.50m)).Should().BeFalse();
    }

    [Theory]
    [InlineData("IT_B3_055", "99999.99", 0)]
    [InlineData("IT_B3_055", "100000.00", 400)]
    [InlineData("IT_B3_055", "0", 400)]
    [InlineData("IT_B3_055", "-1", 400)]
    [InlineData("IT_B3_055", "null", 400)]
    public async Task IT_B3_055_LengthM_Boundaries(string caseId, string length, int expected)
    {
        decimal? value = length == "null" ? null : decimal.Parse(length, System.Globalization.CultureInfo.InvariantCulture);
        var r = await Import([OMarker("L1", 1, value, 1.50m)]);

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(expected, r.ToString());
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Should().OnlyContain(m => m.LengthM > 0 && m.LengthM < 100000m);
    }

    [Theory]
    [InlineData("IT_B3_056", "len20", 200)]
    [InlineData("IT_B3_056", "len21", 400)]
    [InlineData("IT_B3_056", "dup", 200)]
    public async Task IT_B3_056_MarkerCode_Length_And_Duplicate(string caseId, string kind, int expected)
    {
        var code = kind switch { "len20" => new string('M', 20), "len21" => new string('M', 21), _ => "S1" };
        var r = await Import([OMarker(code, 1, 5.20m, 1.50m)]);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Count(m => m.MarkerNo == 1 && m.MarkerCode == code).Should().Be(kind == "len21" ? 0 : 1);
    }

    [Theory]
    [InlineData("IT_B3_057", "XXL")]
    [InlineData("IT_B3_057", "dupM")]
    [InlineData("IT_B3_057", "zero")]
    [InlineData("IT_B3_057", "negative")]
    [InlineData("IT_B3_057", "overflow")]
    public async Task IT_B3_057_Invalid_Ratios_Roll_Back(string caseId, string kind)
    {
        var before = await Snap();
        object[] ratios = kind switch
        {
            "XXL" => [Ratio("M", 1), Ratio("XXL", 1)],
            "dupM" => [Ratio("M", 1), Ratio("M", 2)],
            "zero" => [Ratio("M", 0)],
            "negative" => [Ratio("M", -1)],
            _ => [Ratio("M", 32768)],
        };

        var r = await Import([OMarker("S2", 1, 6.00m, 1.50m, ratios: ratios)]);

        Failed(r, 400);
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Should().NotContain(m => m.MarkerCode == "S2");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_058_One_Bad_Marker_Rolls_Back_The_Valid_One()
    {
        var before = await Snap();
        var r = await Import([OMarker("V1", 1, 6.00m, 1.50m, row: 1), OMarker("V2", 1, 6.00m, 4.00m, row: 2)]);

        Failed(r, 400, 409);
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Should().NotContain(m => m.MarkerCode == "V1");
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_059_Order_Of_Other_Product_Is_Rejected()
    {
        var before = await Snap();
        var r = await Import([OMarker("S2", 1, 6.00m, 1.50m)], $"orderId={P.Od2.OrderId}&parts=markers");

        Failed(r, 400, 404);
        (await MarkersAsync(P.Od2.OrderId, B.Pr1.ProductId)).Should().BeEmpty();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_060_Order_Markers_Without_OrderId_Are_Skipped()
    {
        var before = await Snap();
        var preview = await Adm(Post, PreviewData("parts=markers"), Workbook(markers: [OMarker("S2", 1, 6.00m, 1.50m)]));
        var r = await Adm(Post, ImportData("parts=markers"), Workbook(markers: [OMarker("S2", 1, 6.00m, 1.50m)]));

        preview.Code.Should().Be(200, preview.ToString());
        B1Json.Prop(preview.Data, "notes")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "orderMarkers").Should().Be(0);
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_061", "", 200)]
    [InlineData("IT_B3_061", "Active", 400)]
    [InlineData("IT_B3_061", "PAUSED", 400)]
    public async Task IT_B3_061_Marker_Status_Is_Upper_Case_Only(string caseId, string status, int expected)
    {
        var r = await Import([OMarker("S2", 1, 6.00m, 1.50m, status)]);

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Should().OnlyContain(m => m.Status == "ACTIVE" || m.Status == "DISCONTINUED");
        if (expected == 200) (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Single(m => m.MarkerCode == "S2").Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B3_062_Parts_Filter_Is_Case_Insensitive_And_Ignores_Empty_And_Duplicates()
    {
        var wb = Workbook(
            comps: [.. new[] { XComp(1, "Thân trước"), XComp(2, "Túi", pieces: 2), XComp(3, "Nhãn", "TRIM"), XComp(4, "Cổ") }],
            norms: [new { RowNumber = 1, Color = "Đen", ComponentName = "Cổ", MaterialCode = "V-0002", SpecNote = (string?)null, ParseError = (string?)null }],
            markers: [OMarker("S9", 1, 6.00m, 1.50m)]);

        var r = await Adm(Post, ImportData($"orderId={B.Od1.OrderId}&parts=COMPONENTS,norms,,components"), wb);

        r.Code.Should().Be(200, r.ToString());
        (await CompsOfAsync(B.Pr1.ProductId)).Should().Contain(c => c.ComponentName == "Cổ");
        var co = (await CompsOfAsync(B.Pr1.ProductId)).Single(c => c.ComponentName == "Cổ");
        (await NormsOfAsync(B.Pr1.ProductId)).Should().Contain(n => n.ProductComponentId == co.ProductComponentId);
        (await MarkersAsync(B.Od1.OrderId, B.Pr1.ProductId)).Should().NotContain(m => m.MarkerCode == "S9");
    }

    // ---------------------------------------------------------------- 063–065

    [Fact]
    public async Task IT_B3_063_Replace_Removes_Unused_And_Retires_Used_Components()
    {
        var r = await Adm(Post, ImportData("replace=true&parts=components"), Workbook(comps: [XComp(1, "Nhãn", "TRIM")]));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removed").Should().Be(2);
        var comps = await CompsOfAsync(B.Pr1.ProductId);
        comps.Should().NotContain(c => c.ProductComponentId == B.Pr1Pocket.ProductComponentId);
        comps.Single(c => c.ProductComponentId == B.Pr1Front.ProductComponentId).Status.Should().NotBe("ACTIVE");
        (await OpComponentsAsync(P.Op1.OperationId)).Should().Contain(B.Pr1Front.ProductComponentId);
    }

    [Fact]
    public async Task IT_B3_064_Import_File_With_Error_Row_Rolls_Back()
    {
        var before = await Snap();
        var r = await UploadXlsxAsync(A + $"products/{Pid}/import?replace=false", await TkAdm(), await SpecErrAsync(), "spec_err.xlsx");

        Failed(r, 400);
        (await CompsOfAsync(B.Pr1.ProductId)).Should().NotContain(c => c.ComponentName == "Túi mới");
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_065", "import", "none")]
    [InlineData("IT_B3_065", "import", "corrupt")]
    [InlineData("IT_B3_065", "import", "pdf")]
    [InlineData("IT_B3_065", "import/preview", "none")]
    [InlineData("IT_B3_065", "import/preview", "corrupt")]
    [InlineData("IT_B3_065", "import/preview", "pdf")]
    public async Task IT_B3_065_Bad_Upload_Returns_400_Not_500(string caseId, string endpoint, string kind)
    {
        var before = await Snap();
        byte[]? content = kind switch { "none" => null, "corrupt" => CorruptXlsx(), _ => PdfBytes() };

        var r = await UploadXlsxAsync(A + $"products/{Pid}/{endpoint}", await TkAdm(), content, kind == "pdf" ? "not_excel.pdf" : "spec.xlsx");

        Failed(r, 400);
        await AssertSame(before);
    }
}
