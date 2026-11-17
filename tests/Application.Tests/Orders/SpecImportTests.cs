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

/// <summary>DEV-ALL-114: nhập Excel định mức mã hàng + bìa của đơn (một file 4 bảng, gộp theo khóa, lưu một giao dịch).</summary>
[Collection(ExcelTestCollection.Name)]
public class SpecImportTests : ProductionTestBase
{
    private const int ProductId = 1;
    private const int OrderId = 7;

    private readonly IProductSpecService _specs = Substitute.For<IProductSpecService>();
    private readonly IOrderMarkerService _markers = Substitute.For<IOrderMarkerService>();
    private readonly List<CustomerOrder> _orders = [new() { OrderId = OrderId, OrderCode = "DH01", Status = OrderStatuses.InProduction }];

    public SpecImportTests()
    {
        SetupQueryable(new List<Product> { new() { ProductId = ProductId, ProductCode = "AO01", ProductName = "Áo", Status = RecordStatus.Active } });
        SetupQueryable(_orders);
        SetupQueryable(new List<Material>
        {
            Mat(100, "VAI01"), Mat(101, "VAI02"), Mat(102, "VAI03"), Mat(200, "NHAN01"),
            new() { MaterialId = 300, MaterialCode = "VAICU", MaterialName = "Vải cũ", Color = "Đen", Unit = "m", Status = MaterialStatuses.Discontinued },
        });
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
        new OrderMarkerProductDto(ProductId, "AO01", "Áo", ["S", "M"], [],
            [new OrderMarkerDto(500, 1, "1A", 2.2m, 1.5m, "Active", [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 2)])]),
    ]);

    private SpecImportService Sut() => new(Uow, _specs, _markers, new ClosedXmlSpecWorkbookParser(),
        new SaveProductSpecDtoValidator(), new SaveOrderMarkersDtoValidator());

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

    /// <summary>File mẫu tải từ app (fe/public/templates) phải đọc được đủ 4 bảng định mức và bảng công đoạn.</summary>
    [Fact]
    public void Template_InRepo_ParsesAllTables()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "fe", "public", "templates", "Mau_Nhap_Dinh_Muc.xlsx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("file mẫu phải nằm ở fe/public/templates");
        var path = Path.Combine(dir!.FullName, "fe", "public", "templates", "Mau_Nhap_Dinh_Muc.xlsx");

        using (var stream = File.OpenRead(path))
        {
            var book = new ClosedXmlSpecWorkbookParser().Parse(stream);
            book.Components.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
            book.Norms.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
            book.MarkerGroups.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
            book.OrderMarkers.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        }
        using (var stream = File.OpenRead(path))
        {
            var sheet = new ClosedXmlOperationParser().Parse(stream).Single();
            sheet.HasGroupColumn.Should().BeTrue();
            sheet.Rows.Should().OnlyContain(r => r.ParseError == null).And.ContainSingle(r => r.IsFinal);
        }
        using (var stream = File.OpenRead(path))
        {
            var order = new ClosedXmlOrderLinesParser().Parse(stream);   // DEV-ALL-117: sheet Đơn hàng
            order.SizeColumns.Should().Equal("S", "M", "L", "XL");
            order.Rows.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        }
        using (var stream = File.OpenRead(path))
        {
            var materials = new ClosedXmlOrderMaterialParser().Parse(stream);   // DEV-ALL-118: sheet NVL của đơn
            materials.Should().HaveCount(3).And.OnlyContain(r => r.ParseError == null);
        }
        using (var stream = File.OpenRead(path))
        {
            var products = new ClosedXmlProductParser().Parse(stream);   // DEV-ALL-119: sheet Mã hàng
            products.Select(p => p.ProductCode).Should().Equal("AO-POLO01", "QUAN-KAKI02");
        }
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

        preview.Norms.Select(n => n.Action).Should().Equal("Error", "Error", "Error", "Update", "Error");
        preview.Norms[0].Error.Should().Contain("Tím");
        preview.Norms[1].Error.Should().Contain("VAI99");
        preview.Norms[2].Error.Should().Contain("VAICU");          // NVL đã ngừng dùng
        preview.Norms[4].Error.Should().Contain("dòng 7");          // trùng màu + chi tiết + NVL với dòng 7
        preview.MarkerGroups.Single().Error.Should().Contain("TRIM");
        preview.DuplicateCount.Should().Be(1);
        preview.ErrorCount.Should().Be(4);
        preview.ValidCount.Should().Be(1);
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
}
