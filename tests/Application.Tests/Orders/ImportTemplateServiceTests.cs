using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>File mẫu nhập Excel theo từng chức năng: bảng có dữ liệu thì dùng dữ liệu thật, chưa có thì dòng ví dụ (IsDemo).</summary>
[Collection(ExcelTestCollection.Name)]
public class ImportTemplateServiceTests : ProductionTestBase
{
    private readonly List<Product> _products = [];
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<ProductComponent> _components = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<ProductMarkerComponent> _markerComponents = [];
    private readonly List<ProductMarkerFabric> _markerFabrics = [];
    private readonly List<ProductMarker> _markers = [];
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<Operation> _operations = [];

    public ImportTemplateServiceTests()
    {
        SetupQueryable(_products); SetupQueryable(_orders); SetupQueryable(_components); SetupQueryable(_norms);
        SetupQueryable(_markerComponents); SetupQueryable(_markerFabrics); SetupQueryable(_markers); SetupQueryable(_lines);
        SetupQueryable(_orderMaterials); SetupQueryable(_operations);
    }

    private ImportTemplateService Sut(IImportTemplateWriter? writer = null) =>
        new(Uow, writer ?? new ClosedXmlImportTemplateWriter(), Clock);

    private static MemoryStream Open(Result<ExcelFileDto> result) => new(result.Data!.Content);

    private static IImportTemplateWriter Capture(Action<ImportTemplateModel> onWrite)
    {
        var writer = Substitute.For<IImportTemplateWriter>();
        writer.Write(Arg.Do(onWrite)).Returns([1]);
        return writer;
    }

    /// <summary>Không có dữ liệu nào: mọi file mẫu chứa ví dụ và vẫn đọc được bằng đúng bộ đọc của chức năng đó.</summary>
    [Fact]
    public async Task Demo_Spec_ParsesAllTables()
    {
        var result = await Sut().BuildAsync(ImportTemplateKinds.Spec, null, null, default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("Mau_Dinh_Muc_Bia_Cat.xlsx");
        var book = new ClosedXmlSpecWorkbookParser().Parse(Open(result));
        book.Components.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.Norms.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.MarkerGroups.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.OrderMarkers.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
    }

    [Fact]
    public async Task Demo_OrderLines_ParsesSizeColumns()
    {
        var result = await Sut().BuildAsync(ImportTemplateKinds.OrderLines, null, null, default);

        var order = new ClosedXmlOrderLinesParser().Parse(Open(result));
        order.SizeColumns.Should().Equal("S", "M", "L", "XL");
        order.Rows.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
    }

    [Fact]
    public async Task Demo_OrderMaterials_Parses()
    {
        var result = await Sut().BuildAsync(ImportTemplateKinds.OrderMaterials, null, null, default);

        new ClosedXmlOrderMaterialParser().Parse(Open(result)).Should().HaveCount(4).And.OnlyContain(r => r.ParseError == null);
    }

    [Fact]
    public async Task Demo_Operations_ParsesFinalStepAndGroups()
    {
        var result = await Sut().BuildAsync(ImportTemplateKinds.Operations, null, null, default);

        var sheet = new ClosedXmlOperationParser().Parse(Open(result)).Single();
        sheet.HasGroupColumn.Should().BeTrue();
        sheet.Rows.Should().OnlyContain(r => r.ParseError == null).And.ContainSingle(r => r.IsFinal);
    }

    [Fact]
    public async Task Demo_ProductForm_ParsesItsSheets()
    {
        var result = await Sut().BuildAsync(ImportTemplateKinds.ProductForm, null, null, default);

        result.Data!.FileName.Should().Be("Mau_Form_Ma_Hang.xlsx");
        new ClosedXmlProductParser().Parse(Open(result)).Should().HaveCount(1);
        var book = new ClosedXmlSpecWorkbookParser().Parse(Open(result));
        book.Components.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.Norms.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.MarkerGroups.Should().NotBeEmpty().And.OnlyContain(r => r.ParseError == null);
        book.OrderMarkers.Should().BeEmpty();
    }

    /// <summary>Mã hàng đã có thành phần nhưng chưa có định mức: sheet Thành phần dùng dữ liệu thật, sheet Định mức NVL dùng ví dụ.</summary>
    [Fact]
    public async Task Spec_RealComponents_DemoNorms()
    {
        _products.Add(new Product { ProductId = 7, ProductCode = "AS-1" });
        _components.AddRange([
            new ProductComponent { ProductComponentId = 1, ProductId = 7, ComponentName = "Thân trước", ComponentType = "PANEL", PiecesPerGarment = 2, Status = RecordStatus.Active },
            new ProductComponent { ProductComponentId = 2, ProductId = 7, ComponentName = "Khóa", ComponentType = "TRIM", PiecesPerGarment = 1, Status = RecordStatus.Active },
            new ProductComponent { ProductComponentId = 3, ProductId = 7, ComponentName = "Cũ", ComponentType = "TRIM", PiecesPerGarment = 1, Status = RecordStatus.Discontinued },
            new ProductComponent { ProductComponentId = 4, ProductId = 8, ComponentName = "Của mã khác", ComponentType = "PANEL", PiecesPerGarment = 1, Status = RecordStatus.Active }]);
        ImportTemplateModel? model = null;

        var result = await Sut(Capture(m => model = m)).BuildAsync(ImportTemplateKinds.Spec, 7, null, default);

        result.IsSuccess.Should().BeTrue();
        var components = model!.Tables.Single(t => t.Sheet == "Thành phần");
        components.IsDemo.Should().BeFalse();
        components.Rows.Select(r => r[0]).Should().Equal("Thân trước", "Khóa");
        components.Rows[0].Should().Equal("Thân trước", "PANEL", 2);
        model.Tables.Single(t => t.Sheet == "Định mức NVL").IsDemo.Should().BeTrue();
        model.Tables.Single(t => t.Sheet == "Bìa của đơn").IsDemo.Should().BeTrue();
    }

    [Fact]
    public async Task OrderLines_RealLines_GroupByProductColor_WithSizeColumns()
    {
        _orders.Add(new CustomerOrder { OrderId = 3, OrderCode = "DH" });
        var product = new Product { ProductId = 7, ProductCode = "AS-1" };
        var white = new ProductColor { ProductColorId = 1, ProductId = 7, Color = "Trắng" };
        ProductVariant V(int id, string size) => new() { ProductVariantId = id, ProductId = 7, Product = product, ProductColorId = 1, ProductColor = white, SizeCode = size };
        _lines.AddRange([
            new OrderLine { OrderLineId = 1, OrderId = 3, ProductId = 7, ProductVariant = V(1, "XL"), OrderedQty = 20, CmtUnitPrice = 38000 },
            new OrderLine { OrderLineId = 2, OrderId = 3, ProductId = 7, ProductVariant = V(2, "M"), OrderedQty = 100, CmtUnitPrice = 38000 },
            new OrderLine { OrderLineId = 3, OrderId = 4, ProductId = 7, ProductVariant = V(3, "S"), OrderedQty = 5, CmtUnitPrice = 1 }]);
        ImportTemplateModel? model = null;

        var result = await Sut(Capture(m => model = m)).BuildAsync(ImportTemplateKinds.OrderLines, null, 3, default);

        result.IsSuccess.Should().BeTrue();
        var table = model!.Tables.Single();
        table.IsDemo.Should().BeFalse();
        table.Columns.Select(c => c.Name).Should().Equal("Mã hàng", "Màu", "Đơn giá CMT", "M", "XL");   // size sắp theo thứ tự thông dụng
        table.Rows.Should().ContainSingle();
        table.Rows[0].Should().Equal("AS-1", "Trắng", 38000d, 100, 20);
    }

    private void OrderWithSizes(params string[] sizes)
    {
        _products.Add(new Product { ProductId = 7, ProductCode = "AS-1" });
        _orders.Add(new CustomerOrder { OrderId = 3, OrderCode = "DH" });
        var product = new Product { ProductId = 7, ProductCode = "AS-1" };
        var white = new ProductColor { ProductColorId = 1, ProductId = 7, Color = "Trắng" };
        var id = 1;
        foreach (var size in sizes)
            _lines.Add(new OrderLine
            {
                OrderLineId = id, OrderId = 3, ProductId = 7, OrderedQty = 1,
                ProductVariant = new ProductVariant { ProductVariantId = id++, ProductId = 7, Product = product, ProductColorId = 1, ProductColor = white, SizeCode = size },
            });
    }

    [Fact]
    public async Task Spec_OrderWithoutMarkers_DemoMarkerColumnsAreOnlyOrderSizes()
    {
        OrderWithSizes("XL", "M");
        ImportTemplateModel? model = null;

        var result = await Sut(Capture(m => model = m)).BuildAsync(ImportTemplateKinds.Spec, 7, 3, default);

        result.IsSuccess.Should().BeTrue();
        var table = model!.Tables.Single(t => t.Sheet == "Bìa của đơn");
        table.IsDemo.Should().BeTrue();
        table.Columns.Skip(6).Select(c => c.Name).Should().Equal("M", "XL");
        table.Rows.Should().OnlyContain(r => r.Count == 8);
    }

    [Fact]
    public async Task Spec_OrderWithMarkers_ColumnsAreOnlyOrderSizesEvenIfMarkerHasOtherSize()
    {
        OrderWithSizes("S", "M");
        _markers.Add(new ProductMarker
        {
            ProductMarkerId = 1, ProductId = 7, OrderId = 3, MarkerNo = 1, MarkerCode = "1A", LengthM = 2, WidthM = 1.5m, Status = RecordStatus.Active,
            Product = new Product { ProductCode = "AS-1" },
            ProductMarkerRatios = [new ProductMarkerRatio { SizeCode = "S", PiecesPerPly = 1 }, new ProductMarkerRatio { SizeCode = "XXL", PiecesPerPly = 3 }],
        });
        ImportTemplateModel? model = null;

        await Sut(Capture(m => model = m)).BuildAsync(ImportTemplateKinds.Spec, 7, 3, default);

        var table = model!.Tables.Single(t => t.Sheet == "Bìa của đơn");
        table.IsDemo.Should().BeFalse();
        table.Columns.Skip(6).Select(c => c.Name).Should().Equal("S", "M");
    }

    [Fact]
    public async Task OrderMaterials_RealRows_CustomerQtyBlankWhenZero()
    {
        _orders.Add(new CustomerOrder { OrderId = 3, OrderCode = "DH" });
        _orderMaterials.AddRange([
            new OrderMaterial { OrderMaterialId = 1, OrderId = 3, RequiredQty = 100, CustomerQty = 0, Material = new Material { MaterialCode = "V1" } },
            new OrderMaterial { OrderMaterialId = 2, OrderId = 3, RequiredQty = 50, CustomerQty = 20, Note = "n", Material = new Material { MaterialCode = "T1" } },
            new OrderMaterial { OrderMaterialId = 3, OrderId = 9, RequiredQty = 1, CustomerQty = 0, Material = new Material { MaterialCode = "X" } }]);
        ImportTemplateModel? model = null;

        await Sut(Capture(m => model = m)).BuildAsync(ImportTemplateKinds.OrderMaterials, null, 3, default);

        var table = model!.Tables.Single();
        table.IsDemo.Should().BeFalse();
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Should().Equal("V1", 100d, null, null);
        table.Rows[1].Should().Equal("T1", 50d, 20d, "n");
    }

    [Fact]
    public async Task UnknownProductOrOrderOrKind_IsNotFound()
    {
        (await Sut().BuildAsync(ImportTemplateKinds.Spec, 99, null, default)).Kind.Should().Be(ResultKind.NotFound);
        (await Sut().BuildAsync(ImportTemplateKinds.OrderLines, null, 99, default)).Kind.Should().Be(ResultKind.NotFound);
        (await Sut().BuildAsync("khac", null, null, default)).Kind.Should().Be(ResultKind.NotFound);
    }
}
