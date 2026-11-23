using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-115: nhập Excel định mức mã hàng + bìa của đơn (một file 4 bảng, gộp theo khóa, lưu một giao dịch).</summary>
[Collection(ExcelTestCollection.Name)]
public class SpecImportTests : ProductionTestBase
{
    private const int ProductId = 1;
    private const int OrderId = 7;

    private readonly IProductSpecService _specs = Substitute.For<IProductSpecService>();
    private readonly IOrderMarkerService _markers = Substitute.For<IOrderMarkerService>();
    /// <summary>Dòng định mức đã được lệnh cắt dùng (khóa = ProductComponentMaterialId); nhóm bìa đã có bìa của đơn.</summary>
    private readonly List<WorkOrderLine> _workOrderLines = [];
    private readonly List<ProductMarker> _productMarkers = [];
    private readonly List<ProductColor> _productColors =
    [
        new() { ProductColorId = 1, ProductId = ProductId, Color = "Đen" },
        new() { ProductColorId = 2, ProductId = ProductId, Color = "Xanh" },
    ];
    private readonly List<CustomerOrder> _orders = [new() { OrderId = OrderId, OrderCode = "DH01", Status = OrderStatuses.InProduction }];

    public SpecImportTests()
    {
        SetupQueryable(new List<Product> { new() { ProductId = ProductId, ProductCode = "AO01", Status = RecordStatus.Active } });
        SetupQueryable(_orders);
        SetupQueryable(new List<Material>
        {
            Mat(100, "VAI01"), Mat(101, "VAI02"), Mat(102, "VAI03"), Mat(200, "NHAN01"),
            new() { MaterialId = 300, MaterialCode = "VAICU", MaterialName = "Vải cũ", Color = "Đen", Unit = "m", Status = MaterialStatuses.Discontinued },
        });
        SetupQueryable(_workOrderLines);
        SetupQueryable(_productMarkers);
        SetupQueryable(new List<RequestLine>());
        SetupQueryable(new List<CuttingLayLine>());
        SetupQueryable(new List<CutHandoverLine>());
        SetupQueryable(new List<MaterialIssueLine>());
        SetupQueryable(new List<StockBalance>());
        SetupQueryable(_productColors);
        _specs.GetAsync(ProductId, Arg.Any<CancellationToken>()).Returns(Result<ProductSpecDto>.Success(Spec()));
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _specs.WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>()).Returns(Result<int>.Success(ProductId));
        _markers.GetAsync(OrderId, Arg.Any<CancellationToken>()).Returns(Result<OrderMarkersDto>.Success(Markers()));
        _markers.CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _markers.WriteAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>()).Returns(Result<int>.Success(OrderId));
    }

    private static Material Mat(int id, string code) =>
        new() { MaterialId = id, MaterialCode = code, MaterialName = $"NVL {code}", Color = "Đen", Unit = "m", Status = MaterialStatuses.Active };

    /// <summary>Định mức đang có: 3 chi tiết, 2 màu (Đen S/M, Xanh S); nhóm 1 = thân, nhóm 2 = tay.</summary>
    private static ProductSpecDto Spec() => new(ProductId,
        [
            new ProductComponentDto(1, "Thân trước", "Panel", 2, "Active"),
            new ProductComponentDto(2, "Tay áo", "Panel", 2, "Active"),
            new ProductComponentDto(3, "Nhãn", "Trim", 1, "Active"),
        ],
        [
            new ProductVariantDto(10, "Đen", "S", "Active"),
            new ProductVariantDto(11, "Đen", "M", "Active"),
            new ProductVariantDto(12, "Xanh", "S", "Active"),
        ],
        [
            Norm("Thân trước", "Đen", 100), Norm("Tay áo", "Đen", 100), Norm("Nhãn", "Đen", 200),
            Norm("Thân trước", "Xanh", 101), Norm("Tay áo", "Xanh", 101),
        ],
        [new ProductMarkerComponentDto(1, "Thân trước"), new ProductMarkerComponentDto(2, "Tay áo")],
        [
            Fabric(1, "Đen", 100), Fabric(1, "Xanh", 101), Fabric(2, "Đen", 100), Fabric(2, "Xanh", 101),
        ]);

    private static ProductNormDto Norm(string component, string color, int materialId) =>
        new(materialId * 10, component, color, materialId, $"M{materialId}", "NVL", "m", color, null);

    private static ProductMarkerFabricDto Fabric(int no, string color, int materialId) =>
        new(no, color, materialId, $"M{materialId}", "NVL", color);

    private static OrderMarkersDto Markers() => new(OrderId,
    [
        new OrderMarkerProductDto(ProductId, "AO01", ["S", "M"], [],
            [new OrderMarkerDto(500, 1, "1A", 2.2m, 1.5m, "Active", [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 2)])]),
    ]);

    private SpecImportService Sut() => new(Uow, _specs, _markers, new ClosedXmlSpecWorkbookParser(),
        new SaveProductSpecDtoValidator(), new SaveOrderMarkersDtoValidator(), Substitute.For<IClock>());

    // ---------- File mẫu ----------

    private static MemoryStream Book(Action<XLWorkbook> build)
    {
        var wb = new XLWorkbook();
        build(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>Bảng có dòng tiêu đề ở dòng 3 (dòng 1–2 là tiêu đề / ghi chú như file mẫu).</summary>
    private static void Table(XLWorkbook wb, string sheet, string[] headers, params object?[][] rows)
    {
        var ws = wb.Worksheets.Add(sheet);
        ws.Cell(1, 1).Value = sheet.ToUpperInvariant();
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = ws.Cell(4 + r, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case double d: cell.Value = d; break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
    }

    private static readonly string[] ComponentHeaders = ["Tên chi tiết", "Loại", "Số chi tiết / áo"];
    private static readonly string[] NormHeaders = ["Màu áo", "Tên chi tiết", "Mã NVL", "Ghi chú quy cách"];
    private static readonly string[] GroupHeaders = ["Nhóm bìa", "Chi tiết trên nhóm", "Màu áo dùng nhóm"];
    private static readonly string[] MarkerHeaders = ["Mã hàng", "Nhóm bìa", "Mã bìa", "Dài (m)", "Khổ (m)", "Trạng thái", "S", "M", "XL"];

    // ========== Parser ==========

    [Theory]
    [InlineData("Đang dùng", RecordStatus.Active)]
    [InlineData("Ngừng dùng", RecordStatus.Discontinued)]
    public void Parser_MarkerStatusInVietnamese_MapsToStatusCode(string text, string expected)
    {
        using var stream = Book(wb => Table(wb, "Bìa", MarkerHeaders, [null, 1, "1A", 2.1, 1.5, text, 1, null, 4]));

        var marker = new ClosedXmlSpecWorkbookParser().Parse(stream).OrderMarkers.Single();

        marker.ParseError.Should().BeNull();
        marker.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, RecordStatus.Active)]
    [InlineData("1", RecordStatus.Discontinued)]
    [InlineData("0", RecordStatus.Active)]
    [InlineData("✓", RecordStatus.Discontinued)]
    [InlineData("x", RecordStatus.Discontinued)]
    [InlineData("không", RecordStatus.Active)]
    public void Parser_MarkerTickColumn_BlankIsActiveTickedIsDiscontinued(string? tick, string expected)
    {
        string[] headers = ["Mã hàng", "Nhóm bìa", "Mã bìa", "Dài (m)", "Khổ (m)", "Ngừng dùng", "S", "M"];
        using var stream = Book(wb => Table(wb, "Bìa", headers, [null, 1, "1A", 2.1, 1.5, tick, 1, 2]));

        var book = new ClosedXmlSpecWorkbookParser().Parse(stream);

        book.SizeColumns.Should().Equal("S", "M");
        book.OrderMarkers.Single().Status.Should().Be(expected);
    }

    [Fact]
    public void Parser_FindsTablesByHeaderNames_SplitsListsAndReadsSizeColumns()
    {
        using var stream = Book(wb =>
        {
            Table(wb, "Sheet A", ComponentHeaders, ["Lót tay", "Vải cắt", 2], ["Khóa", "phụ liệu", null]);
            Table(wb, "Bất kỳ", GroupHeaders, [3, "Thân trước ;  Tay áo", "Đen; Xanh"]);
            Table(wb, "Bìa", MarkerHeaders, [null, 1, "1b", 2.1, 1.5, null, 1, null, 4]);
        });

        var book = new ClosedXmlSpecWorkbookParser().Parse(stream);

        book.HasComponents.Should().BeTrue();
        book.HasNorms.Should().BeFalse();
        book.Components.Select(c => (c.Name, c.ComponentType, c.PiecesPerGarment)).Should().Equal(
            ("Lót tay", ComponentTypes.Panel, (short?)2), ("Khóa", ComponentTypes.Trim, (short?)1));
        book.MarkerGroups.Single().ComponentNames.Should().Equal("Thân trước", "Tay áo");
        book.MarkerGroups.Single().Colors.Should().Equal("Đen", "Xanh");
        book.SizeColumns.Should().Equal("S", "M", "XL");
        var marker = book.OrderMarkers.Single();
        marker.MarkerCode.Should().Be("1B");
        marker.Status.Should().Be(RecordStatus.Active);
        marker.Ratios.Select(r => $"{r.SizeCode}:{r.PiecesPerPly}").Should().Equal("S:1", "XL:4");
    }

    [Fact]
    public void Parser_NoKnownTable_Throws()
    {
        using var stream = Book(wb => Table(wb, "Khác", ["Cột A", "Cột B"], ["x", "y"]));

        var act = () => new ClosedXmlSpecWorkbookParser().Parse(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parser_BadComponentType_MarksRowError()
    {
        using var stream = Book(wb => Table(wb, "TP", ComponentHeaders, ["Cổ", "Vải", 1]));

        var row = new ClosedXmlSpecWorkbookParser().Parse(stream).Components.Single();

        row.ParseError.Should().Contain("PANEL");
    }

    // ========== Xem trước ==========

    [Fact]
    public async Task Preview_FlagsUnknownColorMaterialTrimInGroupAndDuplicates()
    {
        using var stream = Book(wb =>
        {
            Table(wb, "ĐM", NormHeaders,
                ["Tím", "Thân trước", "VAI01", null],
                ["Đen", "Thân trước", "VAI99", null],
                ["Đen", "Nhãn", "VAICU", null],
                ["Xanh", "Tay áo", "VAI03", null],
                ["xanh", "tay áo", "vai03", null]);
            Table(wb, "Nhóm", GroupHeaders, [3, "Nhãn", "Đen"]);
        });

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;

        preview.Norms.Select(n => n.Action).Should().Equal("New", "Error", "Error", "Update", "Error");
        preview.Norms[0].Error.Should().BeNull();                  // màu Tím chưa có: tạo khi Nhập
        preview.Norms[1].Error.Should().Contain("VAI99");
        preview.Norms[2].Error.Should().Contain("VAICU");          // NVL đã ngừng dùng
        preview.Norms[4].Error.Should().Contain("dòng 7");          // trùng màu + chi tiết + NVL với dòng 7
        preview.MarkerGroups.Single().Error.Should().Contain("TRIM");
        preview.DuplicateCount.Should().Be(1);
        preview.ErrorCount.Should().Be(3);
        preview.ValidCount.Should().Be(2);
        preview.CanImport.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_SameAsCurrent_IsUnchangedAndCannotImport()
    {
        using var stream = Book(wb => Table(wb, "ĐM", NormHeaders, ["Đen", "Thân trước", "VAI01", null]));

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;

        preview.Norms.Single().Action.Should().Be("Unchanged");
        preview.CanImport.Should().BeFalse();
        preview.Notes.Should().Contain(Messages.SpecImportNoChange);
    }

    [Fact]
    public async Task Preview_CrossErrorFromSaveCheck_BlocksImport()
    {
        _specs.CheckAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.ProductSpecInvalid, "Nhóm bìa 2, màu Xanh: các chi tiết dùng vải khác nhau."));
        using var stream = Book(wb => Table(wb, "ĐM", NormHeaders, ["Xanh", "Tay áo", "VAI03", null]));

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;
        stream.Position = 0;
        var import = await Sut().ImportAsync(ProductId, null, stream, default);

        preview.CrossErrors.Should().ContainSingle().Which.Should().Contain("vải khác nhau");
        preview.CanImport.Should().BeFalse();
        import.IsSuccess.Should().BeFalse();
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_WithoutOrder_SkipsOrderMarkerTable()
    {
        using var stream = Book(wb => Table(wb, "Bìa", MarkerHeaders, ["AO01", 1, "1B", 2.0, 1.5, null, 1, 1, null]));

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;

        preview.HasOrderMarkers.Should().BeFalse();
        preview.OrderMarkers.Should().BeEmpty();
        preview.Notes.Should().Contain(Messages.SpecImportMarkersSkipped);
    }

    [Fact]
    public async Task Preview_OrderMarkers_IgnoresUnknownSizeAndFlagsWidthAndUnknownGroup()
    {
        using var stream = Book(wb => Table(wb, "Bìa", MarkerHeaders,
            ["AO01", 1, "1A", 2.2, 1.5, "ACTIVE", 1, 2, 5],       // giống bìa đang có, cột XL bị bỏ
            [null, 1, "1C", 2.0, 4.0, null, 1, 1, null],         // khổ > 3,5
            [null, 9, "9A", 2.0, 1.5, null, 1, 1, null],         // nhóm 9 chưa khai
            ["QUAN02", 1, "1D", 2.0, 1.5, null, 1, 1, null]));   // mã hàng khác: bỏ qua

        var preview = (await Sut().PreviewAsync(ProductId, OrderId, stream, default)).Data!;

        preview.OrderMarkers.Select(m => m.Action).Should().Equal("Unchanged", "Error", "Error");
        preview.OrderMarkers[0].Ratios.Select(r => r.SizeCode).Should().Equal("S", "M");
        preview.OrderMarkers[1].Error.Should().Contain("3,5");
        preview.OrderMarkers[2].Error.Should().Be(string.Format(Messages.SpecImportGroupUnknown, 9));
        preview.Notes.Should().Contain(string.Format(Messages.SpecImportSizesIgnored, "XL"));
        preview.Notes.Should().Contain(string.Format(Messages.SpecImportOtherProductRows, 1));
    }

    [Fact]
    public async Task Preview_ClosedOrder_Fails()
    {
        _orders[0].Status = OrderStatuses.Completed;
        using var stream = Book(wb => Table(wb, "ĐM", NormHeaders, ["Đen", "Thân trước", "VAI01", null]));

        var result = await Sut().PreviewAsync(ProductId, OrderId, stream, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.OrderMarkerOrderClosed);
    }

    // ========== Nhập ==========

    [Fact]
    public async Task Import_MergesByKey_OverwritesFilePairsAndKeepsTheRest()
    {
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        using var stream = Book(wb =>
        {
            Table(wb, "TP", ComponentHeaders, ["Lót tay", "PANEL", 2], ["Tay áo", "PANEL", 1]);
            Table(wb, "ĐM", NormHeaders, ["Xanh", "Tay áo", "VAI03", "phối"], ["Xanh", "Lót tay", "VAI01", null]);
            Table(wb, "Nhóm", GroupHeaders, [2, "Tay áo; Lót tay", "Đen; Xanh"]);
        });

        var result = await Sut().ImportAsync(ProductId, null, stream, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().Be(new SpecImportResultDto(2, 2, 1, 0));
        written!.Components.Select(c => $"{c.ComponentName}:{c.PiecesPerGarment}")
            .Should().BeEquivalentTo("Thân trước:2", "Tay áo:1", "Nhãn:1", "Lót tay:2");
        written.Components.Single(c => c.ComponentName == "Tay áo").ProductComponentId.Should().Be(2);
        written.Norms.Select(n => $"{n.Color}|{n.ComponentName}|{n.MaterialId}").Should().BeEquivalentTo(
            "Đen|Thân trước|100", "Đen|Tay áo|100", "Đen|Nhãn|200",       // màu Đen không có trong file: giữ nguyên
            "Xanh|Thân trước|101",                                         // cặp không có trong file: giữ nguyên
            "Xanh|Tay áo|102", "Xanh|Lót tay|100");                       // cặp có trong file: thay theo file
        written.MarkerComponents.Select(m => $"{m.MarkerNo}:{m.ComponentName}")
            .Should().BeEquivalentTo("1:Thân trước", "2:Tay áo", "2:Lót tay");
        written.MarkerColors.Should().HaveCount(4);
        await Uow.Received(1).BeginTransactionAsync();
        await Uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task Import_NewColorInFile_CreatesColorAndImportsNorms()
    {
        Uow.Repository<ProductColor>().AddRangeAsync(Arg.Do<IEnumerable<ProductColor>>(c => _productColors.AddRange(c)))
            .Returns(Task.CompletedTask);
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        using var stream = Book(wb => Table(wb, "ĐM", NormHeaders, ["Kem", "Tay áo", "VAI01", null]));

        var result = await Sut().ImportAsync(ProductId, null, stream, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _productColors.Select(c => c.Color).Should().Contain("Kem");
        written!.Norms.Should().Contain(n => n.Color == "Kem" && n.ComponentName == "Tay áo" && n.MaterialId == 100);
        written.Variants.Should().NotContain(v => v.SizeCode == SpecImportBuilder.ColorOnlySize);
        await Uow.Received(1).CommitTransactionAsync();
    }

    [Fact]
    public async Task Import_OrderMarkersOnly_SkipsSpecWriteAndSavesMergedMarkers()
    {
        SaveOrderMarkersDto? written = null;
        _markers.WriteAsync(OrderId, ProductId, Arg.Do<SaveOrderMarkersDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(OrderId));
        using var stream = Book(wb => Table(wb, "Bìa", MarkerHeaders,
            [null, 1, "1A", 2.4, 1.5, null, 1, 2, null],
            [null, 2, "2A", 1.6, 1.5, "ngừng dùng", 1, 2, null]));

        var result = await Sut().ImportAsync(ProductId, OrderId, stream, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        await _markers.Received(1).CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>());
        written!.Markers.Should().HaveCount(2);
        written.Markers.Single(m => m.MarkerCode == "1A").Should().Match<SaveOrderMarkerDto>(m => m.ProductMarkerId == 500 && m.LengthM == 2.4m);
        written.Markers.Single(m => m.MarkerCode == "2A").Status.Should().Be("Discontinued");
    }

    [Fact]
    public async Task Import_MarkerCheckFailsAfterSpecWrite_RollsBack()
    {
        _markers.CheckAsync(OrderId, ProductId, Arg.Any<SaveOrderMarkersDto>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.OrderMarkerInvalid, "Bìa 1A: size M không có trong đơn ở các màu dùng nhóm bìa của bìa."));
        using var stream = Book(wb =>
        {
            Table(wb, "Nhóm", GroupHeaders, [1, "Thân trước", "Đen"]);
            Table(wb, "Bìa", MarkerHeaders, [null, 1, "1B", 2.0, 1.5, null, 1, 1, null]);
        });

        var result = await Sut().ImportAsync(ProductId, OrderId, stream, default);

        result.IsSuccess.Should().BeFalse();
        await _specs.Received(1).WriteAsync(ProductId, Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
        await Uow.Received(1).RollbackTransactionAsync();
        await Uow.DidNotReceive().CommitTransactionAsync();
    }

    [Fact]
    public async Task Preview_Parts_KeepsOnlyChosenTables()
    {
        using var stream = Book(wb =>
        {
            Table(wb, "TP", ComponentHeaders, ["Lót tay", "PANEL", 2]);
            Table(wb, "ĐM", NormHeaders, ["Xanh", "Tay áo", "VAI03", null]);
        });

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default, [SpecImportParts.Components])).Data!;

        preview.HasComponents.Should().BeTrue();
        preview.HasNorms.Should().BeFalse();
        preview.Components.Should().ContainSingle();
        preview.Norms.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_GroupWithUnknownColors_KeepsThemToCreateOnImport()
    {
        using var stream = Book(wb => Table(wb, "Nhóm", GroupHeaders,
            [1, "Thân trước", "Đen; Hồng; Đỏ"],
            [2, "Tay áo", "Hồng"]));

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;

        preview.MarkerGroups[0].Error.Should().BeNull();
        preview.MarkerGroups[0].Colors.Should().Equal("Đen", "Hồng", "Đỏ");   // màu chưa có: tạo khi Nhập
        preview.MarkerGroups[1].Error.Should().BeNull();
    }

    [Fact]
    public async Task Check_NewProduct_ColorsPendingButOtherErrorsReported()
    {
        var workbook = Parse(Book(wb =>
        {
            Table(wb, "TP", ComponentHeaders, ["Cổ", "PANEL", 1]);
            Table(wb, "ĐM", NormHeaders, ["Trắng", "Cổ", "VAI01", null], ["Trắng", "Cổ", "VAI99", null], ["Trắng", "Túi", "VAI01", null]);
            Table(wb, "Nhóm", GroupHeaders, [1, "Cổ", "Trắng"]);
        }));

        var check = (await Sut().CheckAsync(null, null, workbook, default)).Data!;

        check.Components.Single().Action.Should().Be("New");
        // DEV-ALL-123: mã NVL chưa có trong danh mục là cảnh báo NewMaterial, không phải lỗi.
        check.Norms.Select(n => n.Action).Should().Equal("Pending", "NewMaterial", "Error");
        check.Norms[0].Error.Should().Contain("Trắng");
        check.Norms[1].Error.Should().Contain("VAI99");
        check.Norms[2].Error.Should().Contain("Túi");
        check.MarkerGroups.Single().Action.Should().Be("Pending");
        check.ErrorCount.Should().Be(1);
        await _specs.DidNotReceive().WriteAsync(Arg.Any<int>(), Arg.Any<SaveProductSpecDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Check_ExistingProductWithoutOrder_MarkersPending()
    {
        var workbook = Parse(Book(wb => Table(wb, "Bìa", MarkerHeaders, [null, 1, "1Z", 2.0, 1.5, null, 1, 1, null])));

        var check = (await Sut().CheckAsync(ProductId, null, workbook, default)).Data!;

        check.OrderMarkers.Single().Action.Should().Be("Pending");
        check.HasOrderMarkers.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewFromData_SameResultAsFile()
    {
        using var stream = Book(wb => Table(wb, "ĐM", NormHeaders, ["Xanh", "Tay áo", "VAI03", null]));
        var workbook = Parse(Book(wb => Table(wb, "ĐM", NormHeaders, ["Xanh", "Tay áo", "VAI03", null])));

        var fromFile = (await Sut().PreviewAsync(ProductId, null, stream, default)).Data!;
        var fromData = (await Sut().PreviewAsync(ProductId, null, workbook, default)).Data!;

        fromData.Norms.Single().Action.Should().Be(fromFile.Norms.Single().Action);
        fromData.CanImport.Should().Be(fromFile.CanImport);
    }

    private static SpecWorkbookDto Parse(MemoryStream stream)
    {
        using (stream) return new ClosedXmlSpecWorkbookParser().Parse(stream);
    }

    [Fact]
    public async Task Preview_PartNotInFile_Fails()
    {
        using var stream = Book(wb => Table(wb, "TP", ComponentHeaders, ["Lót tay", "PANEL", 2]));

        var result = await Sut().PreviewAsync(ProductId, null, stream, default, [SpecImportParts.Norms]);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Định mức NVL");
    }

    [Fact]
    public async Task Preview_UnknownPart_Fails()
    {
        using var stream = Book(wb => Table(wb, "TP", ComponentHeaders, ["Lót tay", "PANEL", 2]));

        var result = await Sut().PreviewAsync(ProductId, null, stream, default, ["abc"]);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("abc");
    }

    // ========== Chế độ thay thế (bỏ dòng cũ không có trong file) ==========

    [Fact]
    public async Task Import_Replace_DropsComponentsAndNormPairsNotInFile_KeepsTablesAbsentFromFile()
    {
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        using var stream = Book(wb =>
        {
            Table(wb, "TP", ComponentHeaders, ["Tay áo", "PANEL", 2]);
            Table(wb, "ĐM", NormHeaders, ["Đen", "Tay áo", "VAI02", null]);
        });

        var result = await Sut().ImportAsync(ProductId, null, stream, default, replace: true);

        result.IsSuccess.Should().BeTrue(result.Message);
        // Thân trước, Nhãn không có trong file -> bị bỏ; cặp Xanh|Tay áo không có trong file -> bỏ; Đen|Tay áo thay theo file
        result.Data!.Removed.Should().Be(3);
        written!.Components.Select(c => c.ComponentName).Should().Equal("Tay áo");
        written.Norms.Select(n => $"{n.Color}|{n.ComponentName}|{n.MaterialId}").Should().Equal("Đen|Tay áo|101");
        // bảng Nhóm bìa không có trong file nên không bị đụng: nhóm 2 (Tay áo) còn, nhóm 1 mất theo chi tiết Thân trước
        written.MarkerComponents.Select(m => $"{m.MarkerNo}:{m.ComponentName}").Should().Equal("2:Tay áo");
        result.Message.Should().Contain("Đã xóa 3 dòng cũ");
    }

    [Fact]
    public async Task Import_Replace_KeepsUsedNormsComponentsAndGroupsUntouched()
    {
        // Định mức Tay áo|Xanh (mã 1010) đã dùng ở lệnh cắt; nhóm bìa 1 đã có bìa của đơn
        _workOrderLines.Add(new WorkOrderLine { ProductComponentMaterialId = 1010 });
        _productMarkers.Add(new ProductMarker { ProductId = ProductId, MarkerNo = 1 });
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        using var stream = Book(wb =>
        {
            Table(wb, "TP", ComponentHeaders, ["Nhãn", "TRIM", 1]);
            Table(wb, "ĐM", NormHeaders, ["Đen", "Nhãn", "NHAN01", null]);
            Table(wb, "NB", GroupHeaders, ["2", "Tay áo", "Đen"]);
        });

        var preview = (await Sut().PreviewAsync(ProductId, null, stream, default, replace: true)).Data!;
        stream.Position = 0;
        var result = await Sut().ImportAsync(ProductId, null, stream, default, replace: true);

        result.IsSuccess.Should().BeTrue(result.Message);
        // Tay áo giữ vì có định mức đã dùng; Thân trước giữ vì nằm trên nhóm 1 đã có bìa; chỉ bỏ được những dòng chưa dùng
        written!.Components.Select(c => c.ComponentName).Should().BeEquivalentTo("Nhãn", "Tay áo", "Thân trước");
        written.Norms.Should().Contain(n => n.Color == "Xanh" && n.ComponentName == "Tay áo" && n.MaterialId == 101);
        written.MarkerComponents.Select(m => m.MarkerNo).Should().Contain(1);
        preview.Notes.Should().Contain(n => n.Contains("Giữ nguyên") && n.Contains("Tay áo"));
        preview.RemovedNames.Should().NotContain(n => n.Contains("Tay áo") && !n.Contains("Đen"));
    }

    [Fact]
    public async Task Import_WithoutReplace_NeverDropsAnything()
    {
        SaveProductSpecDto? written = null;
        _specs.WriteAsync(ProductId, Arg.Do<SaveProductSpecDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(ProductId));
        using var stream = Book(wb => Table(wb, "TP", ComponentHeaders, ["Tay áo", "PANEL", 1]));

        var result = await Sut().ImportAsync(ProductId, null, stream, default);

        result.Data!.Removed.Should().Be(0);
        written!.Components.Select(c => c.ComponentName).Should().BeEquivalentTo("Thân trước", "Tay áo", "Nhãn");
    }

    [Fact]
    public async Task Preview_Replace_ListsRemovedRows_AndFileWithOnlyErrorRowsKeepsOldData()
    {
        using var ok = Book(wb => Table(wb, "TP", ComponentHeaders, ["Tay áo", "PANEL", 2]));
        var preview = (await Sut().PreviewAsync(ProductId, null, ok, default, replace: true)).Data!;
        preview.RemovedCount.Should().Be(2);
        preview.RemovedNames.Should().BeEquivalentTo("Thành phần: Thân trước", "Thành phần: Nhãn");
        preview.CanImport.Should().BeTrue();

        // dòng lỗi (loại sai) của Tay áo: vẫn tính là có trong file nên Tay áo không bị bỏ; Thân trước, Nhãn vẫn bị bỏ
        using var bad = Book(wb => Table(wb, "TP", ComponentHeaders, ["Tay áo", "XYZ", 2], ["Thân trước", "PANEL", 2]));
        var second = (await Sut().PreviewAsync(ProductId, null, bad, default, replace: true)).Data!;
        second.RemovedNames.Should().BeEquivalentTo("Thành phần: Nhãn");
    }

    [Fact]
    public async Task Import_Replace_DropsOrderMarkersNotInFile()
    {
        SaveOrderMarkersDto? written = null;
        _markers.WriteAsync(OrderId, ProductId, Arg.Do<SaveOrderMarkersDto>(d => written = d), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(OrderId));
        using var stream = Book(wb => Table(wb, "Bìa", MarkerHeaders, ["AO01", 1, "1B", 2.0, 1.5, null, 1, 1, null]));

        var result = await Sut().ImportAsync(ProductId, OrderId, stream, default, replace: true);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Removed.Should().Be(1);                                  // bìa 1A cũ không có trong file
        written!.Markers.Select(m => m.MarkerCode).Should().Equal("1B");
    }
}
