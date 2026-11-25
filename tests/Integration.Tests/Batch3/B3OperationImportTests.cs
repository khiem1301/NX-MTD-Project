using FluentAssertions;
using Integration.Tests.Batch1;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>IT_B3_095–113 — nhập Excel công đoạn (D2–D5): toàn vẹn transaction. 114, 115 nằm ở B3ConcurrencyTests.</summary>
public sealed class B3OperationImportTests(ITestOutputHelper output) : B3TestBase(output)
{
    private static readonly HttpMethod Post = HttpMethod.Post;
    private static readonly DateOnly D2027 = new(2027, 1, 1);

    private int Pr1 => B.Pr1.ProductId;
    private string Q(string extra = "") => $"productId={Pr1}{extra}";
    private Task<Api> ImportData(object sheet, string extra = "") => Acc(Post, AC + $"operations/import-data?{Q(extra)}", sheet);
    private Task<Api> PreviewData(object sheet, string extra = "") => Acc(Post, AC + $"operations/import/preview-data?{Q(extra)}", sheet);
    private static string Eff(DateOnly d) => $"&effectiveDate={Dated(d)}";

    private async Task<NX_MTD.Domain.Entities.Operation> OpBySeq(int seq)
    {
        await using var db = NewDb();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Operations, o => o.ProductId == Pr1 && o.StationSequence == seq && o.OperationName.StartsWith("May túi"));
    }

    private async Task<byte[]> OpsFileAsync() => await TemplateAsync("operations", Pr1, await TkAcc());

    private object[] ThreeNewRows() =>
    [
        OpRow(1, 10, "May túi 10", 1000m, "Nhóm May", ["Túi"]),
        OpRow(2, 11, "May túi 11", 1100m, "Nhóm May", ["Túi"]),
        OpRow(3, 12, "May túi 12", 1200m, "Nhóm May", ["Túi"]),
    ];

    // ---------------------------------------------------------------- 095–098 file / preview

    [Fact]
    public async Task IT_B3_095_Preview_File_Is_Read_Only()
    {
        var before = await Snap();
        var r = await UploadXlsxAsync(AC + $"operations/import/preview?{Q()}", await TkAcc(), await OpsFileAsync(), "ops_ok.xlsx");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Prop(r.Data, "sheetNames")!.Value.GetArrayLength().Should().BeGreaterThan(0);
        B1Json.Bool(r.Data, "productHasFinalStep").Should().BeTrue();
        var rows = B1Json.Prop(r.Data, "rows")!.Value.EnumerateArray().ToList();
        rows.Select(x => B1Json.Str(x, "action")).Should().OnlyContain(a => a == "New" || a == "Update" || a == "Unchanged" || a == "Error");
        (B1Json.Int(r.Data, "newCount")!.Value + B1Json.Int(r.Data, "updateCount")!.Value + B1Json.Int(r.Data, "unchangedCount")!.Value
            + B1Json.Int(r.Data, "errorCount")!.Value).Should().Be(rows.Count);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_096_Preview_Unknown_Sheet_Fails_And_Default_Sheet_Works()
    {
        var before = await Snap();
        var file = await OpsFileAsync();
        var token = await TkAcc();
        var missing = await UploadXlsxAsync(AC + $"operations/import/preview?{Q("&sheetName=KhongCo")}", token, file, "ops_ok.xlsx");
        var none = await UploadXlsxAsync(AC + $"operations/import/preview?{Q()}", token, file, "ops_ok.xlsx");

        Failed(missing, 400, 404);
        none.Code.Should().Be(200, none.ToString());
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_097", "none")]
    [InlineData("IT_B3_097", "corrupt")]
    [InlineData("IT_B3_097", "pdf")]
    [InlineData("IT_B3_097", "big")]
    public async Task IT_B3_097_Bad_Upload_Is_Rejected_Without_500(string caseId, string kind)
    {
        var before = await Snap();
        byte[]? content = kind switch
        {
            "none" => null, "corrupt" => CorruptXlsx(), "pdf" => PdfBytes(),
            _ => new byte[] { 0x50, 0x4B, 0x03, 0x04 }.Concat(new byte[6 * 1024 * 1024]).ToArray(),
        };

        var r = await UploadXlsxAsync(AC + $"operations/import/preview?{Q()}", await TkAcc(), content, kind == "pdf" ? "not_excel.pdf" : "ops.xlsx");

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(kind == "big" ? new[] { 400, 413 } : new[] { 400 }, r.ToString());
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_098", "none", 400)]
    [InlineData("IT_B3_098", "zero", 400)]
    [InlineData("IT_B3_098", "missing", 404)]
    public async Task IT_B3_098_Preview_Bad_ProductId(string caseId, string kind, int expected)
    {
        var before = await Snap();
        var query = kind switch { "none" => "", "zero" => "?productId=0", _ => $"?productId={await MissingIdAsync()}" };

        var r = await UploadXlsxAsync(AC + "operations/import/preview" + query, await TkAcc(), await OpsFileAsync(), "ops_ok.xlsx");

        Failed(r, expected);
        await AssertSame(before);
    }

    // ---------------------------------------------------------------- 099–102 ghi dữ liệu

    [Fact]
    public async Task IT_B3_099_Import_Data_Creates_Operations_Components_And_Draft_Rates()
    {
        var r = await ImportData(Sheet(ThreeNewRows()), Eff(D2027) + "&submitRates=false");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "created").Should().Be(3);
        B1Json.Int(r.Data, "rateCreated").Should().Be(3);
        for (var stt = 10; stt <= 12; stt++)
        {
            var op = await OpBySeq(stt);
            (op.Status, op.TeamType, op.OperationGroupId).Should().Be(("ACTIVE", "SEWING", (int?)P.Og1.OperationGroupId));
            (await OpComponentsAsync(op.OperationId)).Should().Equal(B.Pr1Pocket.ProductComponentId);
            var rate = (await RatesOfAsync(op.OperationId, D2027)).Single();
            rate.Status.Should().Be("DRAFT");
        }
    }

    [Fact]
    public async Task IT_B3_100_Import_Data_With_SubmitRates_Creates_Pending_Rates()
    {
        var date = new DateOnly(2027, 2, 1);
        var r = await ImportData(Sheet(ThreeNewRows()), Eff(date) + "&submitRates=true");

        r.Code.Should().Be(200, r.ToString());
        for (var stt = 10; stt <= 12; stt++)
        {
            var op = await OpBySeq(stt);
            var rate = (await RatesOfAsync(op.OperationId, date)).Single();
            rate.Status.Should().Be("PENDING_APPROVAL");
            rate.SubmittedAt.Should().NotBeNull();
            AssertCreatedNow(rate.SubmittedAt!.Value);
        }
    }

    [Fact]
    public async Task IT_B3_101_Rates_Without_EffectiveDate_Are_Skipped_Or_Rejected()
    {
        var r = await ImportData(Sheet(ThreeNewRows()));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400);
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.PieceRates)).Should().Be(5, "S0 có 5 đơn giá, import không tạo thêm");
        if (r.Code == 200) B1Json.Int(r.Data, "rateSkipped").Should().BeGreaterThanOrEqualTo(3);
    }

    [Theory]
    [InlineData("IT_B3_102", "0001-01-01")]
    [InlineData("IT_B3_102", "9999-12-31")]
    [InlineData("IT_B3_102", "2027-02-30")]
    public async Task IT_B3_102_Invalid_EffectiveDate_Returns_400(string caseId, string date)
    {
        var before = await Snap();
        var r = await ImportData(Sheet(ThreeNewRows()), $"&effectiveDate={date}");

        Failed(r, 400);
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.PieceRates, x => x.EffectiveDate == DateOnly.MinValue)).Should().BeFalse();
        await AssertSame(before);
    }

    // ---------------------------------------------------------------- 103–106 lỗi, công đoạn cuối

    [Fact]
    public async Task IT_B3_103_Error_Row_Rolls_Back_Whole_Import()
    {
        var before = await Snap();
        var r = await ImportData(Sheet(OpRow(1, 20, "Hợp lệ 20", 1000m), OpRow(2, 21, "Hợp lệ 21", 1000m), OpRow(3, 22, "Lỗi", 1000m, err: "Ô không đọc được")),
            Eff(D2027));

        Failed(r, 400);
        (await OpByCodeAsync(Pr1, "CD20")).Should().BeNull();
        (await OpByCodeAsync(Pr1, "CD21")).Should().BeNull();
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_104_New_Final_Row_Never_Creates_Second_Active_Final_Step()
    {
        var r = await ImportData(Sheet(OpRow(1, 30, "Công đoạn cuối mới", final: true)));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 409);
        (await FinalActiveCountAsync(Pr1)).Should().Be(1);
    }

    [Theory]
    [InlineData("IT_B3_105", "twoFinal")]
    [InlineData("IT_B3_105", "badFinalStt")]
    public async Task IT_B3_105_Final_Step_Selection_Errors(string caseId, string kind)
    {
        var before = await Snap();
        var r = kind == "twoFinal"
            ? await ImportData(Sheet(OpRow(1, 40, "Cuối A", final: true), OpRow(2, 41, "Cuối B", final: true)))
            : await ImportData(Sheet(OpRow(1, 40, "May 40")), "&finalStt=999");

        Failed(r, 400);
        await AssertSame(before);
    }

    [Fact]
    public async Task IT_B3_106_FinalStt_Moves_Final_Step_In_One_Transaction()
    {
        var r = await ImportData(Sheet(OpRow(1, 11, "May 11"), OpRow(2, 12, "May 12")), "&finalStt=12");

        r.Code.Should().Be(200, r.ToString());
        (await FinalActiveCountAsync(Pr1)).Should().Be(1);
        await using var db = NewDb();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Operations,
            o => o.ProductId == Pr1 && o.IsFinalStep && o.Status == "ACTIVE")).StationSequence.Should().Be(12);
    }

    // ---------------------------------------------------------------- 107–110 replace, đơn giá

    [Fact]
    public async Task IT_B3_107_Replace_Removes_Unused_And_Retires_Used_Operations()
    {
        Created(await Acc(Post, AC + "operations", SaveOp(Pr1, "CD03")));
        var sheet = Sheet(OpRow(1, 1, "Công đoạn CD01"));
        var ratesOfOp2 = (await RatesOfAsync(P.Op2.OperationId)).Count;

        var preview = await PreviewData(sheet, "&replace=true");
        var r = await ImportData(sheet, "&replace=true");

        preview.Code.Should().Be(200, preview.ToString());
        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removed").Should().BeGreaterThanOrEqualTo(1);
        B1Json.Int(preview.Data, "removeCount").Should().Be(B1Json.Int(r.Data, "removed"));
        (await OpByCodeAsync(Pr1, "CD03")).Should().BeNull("công đoạn chưa dùng bị DELETE");
        (await OpAsync(P.Op2.OperationId))!.Status.Should().NotBe("ACTIVE");
        (await RatesOfAsync(P.Op2.OperationId)).Should().HaveCount(ratesOfOp2);
    }

    [Fact]
    public async Task IT_B3_108_Without_Replace_Nothing_Is_Removed()
    {
        Created(await Acc(Post, AC + "operations", SaveOp(Pr1, "CD03")));
        var r = await ImportData(Sheet(OpRow(1, 1, "Công đoạn CD01")), "&replace=false");

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "removed").Should().Be(0);
        (await OpAsync(P.Op2.OperationId))!.Status.Should().Be("ACTIVE");
        (await OpByCodeAsync(Pr1, "CD03"))!.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task IT_B3_109_Draft_Rate_Of_Same_Date_Is_Updated_In_Place()
    {
        var r = await ImportData(Sheet(OpRow(1, 1, "Công đoạn CD01", 1800m)), Eff(D1201));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "rateUpdated").Should().Be(1);
        (await RatesOfAsync(P.Op1.OperationId, D1201)).Should().ContainSingle().Which.Rate.Should().Be(1800.00m);
    }

    [Fact]
    public async Task IT_B3_110_Approved_Rate_Is_Never_Overwritten()
    {
        var r = await ImportData(Sheet(OpRow(1, 1, "Công đoạn CD01", 9999m)), Eff(new DateOnly(2026, 1, 1)));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 400, 409);
        if (r.Code == 200) B1Json.Int(r.Data, "rateSkipped").Should().BeGreaterThanOrEqualTo(1);
        var approved = await RateAsync(P.PrA1.PieceRateId);
        (approved.Rate, approved.Status).Should().Be((1200.00m, "APPROVED"));
        approved.ApprovedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------- 111–113 dữ liệu dòng

    [Fact]
    public async Task IT_B3_111_Component_Of_Other_Product_Rolls_Back()
    {
        var before = await Snap();
        var r = await ImportData(Sheet(OpRow(1, 50, "May 50", comps: ["Thân sau"])));

        Failed(r, 400);
        await AssertSame(before);
    }

    [Theory]
    [InlineData("IT_B3_112", "-1", 400)]
    [InlineData("IT_B3_112", "0", 400)]
    [InlineData("IT_B3_112", "9999999999999999.99", 200)]
    [InlineData("IT_B3_112", "10000000000000000", 400)]
    [InlineData("IT_B3_112", "1500.555", 0)]
    public async Task IT_B3_112_Rate_Boundaries(string caseId, string rate, int expected)
    {
        var value = decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture);
        var r = await ImportData(Sheet(OpRow(1, 60, "May 60", value)), Eff(D2027));

        r.ShouldNotBeServerError();
        if (expected == 0) r.Code.Should().BeOneOf(200, 400); else r.Code.Should().Be(expected, r.ToString());
        var op = await OpByCodeAsync(Pr1, "CD60");
        if (r.Code == 200) (await RatesOfAsync(op!.OperationId, D2027)).Should().ContainSingle();
        else op.Should().BeNull();
    }

    [Theory]
    [InlineData("IT_B3_113", "dupStt")]
    [InlineData("IT_B3_113", "emptyName")]
    [InlineData("IT_B3_113", "skill300")]
    public async Task IT_B3_113_Invalid_Rows_Return_400(string caseId, string kind)
    {
        var before = await Snap();
        var sheet = kind switch
        {
            "dupStt" => Sheet(OpRow(1, 10, "May A"), OpRow(2, 10, "May B")),
            "emptyName" => Sheet(OpRow(1, 70, "")),
            _ => Sheet(OpRow(1, 70, "May 70", skill: 300)),
        };

        var r = await ImportData(sheet);

        Failed(r, 400);
        await AssertSame(before);
    }
}
