using ClosedXML.Excel;
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

/// <summary>DEV-ALL-122/123: đọc file Excel nhiều sheet và kiểm bản đã sửa trên popup (không ghi gì).</summary>
[Collection(ExcelTestCollection.Name)]
public class ExcelBundleServiceTests : ProductionTestBase
{
    private readonly IProductImportService _productImport = Substitute.For<IProductImportService>();
    private readonly ISpecImportService _specImport = Substitute.For<ISpecImportService>();
    private readonly IOrderMaterialService _orderMaterials = Substitute.For<IOrderMaterialService>();

    private static readonly SpecWorkbookDto EmptySpec = new(false, [], false, [], false, [], false, [], []);

    public ExcelBundleServiceTests()
    {
        SetupQueryable(new List<Material>
        {
            new() { MaterialId = 1, MaterialCode = "V-0001", MaterialName = "Vải", Color = "Hồng", Unit = "m", Status = MaterialStatuses.Active },
            new()
            {
                MaterialId = 2, MaterialCode = "V-0002", MaterialName = "Kate trắng", MaterialType = MaterialTypes.Fabric, Color = "Trắng",
                Unit = "m", WidthM = 1.5m, Status = MaterialStatuses.Active,
            },
        });
        _specImport.CheckAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<SpecWorkbookDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<SpecImportPreviewDto>.Success(SpecPreview()));
    }

    private ExcelBundleService Sut() => new(Uow, new ClosedXmlProductParser(), new ClosedXmlSpecWorkbookParser(),
        new ClosedXmlOrderMaterialParser(), new ClosedXmlMaterialParser(), _productImport, _specImport, _orderMaterials);

    private static SpecImportPreviewDto SpecPreview(params SpecImportNormRowDto[] norms) => new(0, string.Empty, null, null,
        [], norms, [], [], false, norms.Length > 0, false, false, norms.Length, 0, 0, 0, [], [], false);

    private static MemoryStream Book(Action<XLWorkbook> build)
    {
        using var wb = new XLWorkbook();
        build(wb);
        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Parse_FileWithoutKnownTables_Fails()
    {
        using var stream = Book(wb => wb.AddWorksheet("X").Cell(1, 1).Value = "Không phải bảng nào");

        var result = Sut().Parse(stream);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(Messages.ExcelBundleEmpty);
    }

    [Fact]
    public void Parse_OrderMaterialSheet_ReadsOptionalNameAndUnit()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("NVL");
            string[] headers = ["Mã NVL", "Tên NVL", "Đơn vị", "Số cần"];
            for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(2, 1).Value = "lua-xanh";
            ws.Cell(2, 2).Value = "Lụa xanh";
            ws.Cell(2, 3).Value = "m";
            ws.Cell(2, 4).Value = 10;
        });

        var bundle = Sut().Parse(stream).Data!;

        bundle.HasProducts.Should().BeFalse();
        bundle.HasOrderMaterials.Should().BeTrue();
        bundle.OrderMaterials.Single().Should().Match<OrderMaterialExcelRow>(r =>
            r.MaterialCode == "LUA-XANH" && r.MaterialName == "Lụa xanh" && r.Unit == "m" && r.RequiredQty == 10);
    }

    [Fact]
    public async Task CheckAsync_NoOrder_UnknownMaterialIsNewMaterialWithSuggestion()
    {
        var bundle = new ExcelBundleDto(false, [], EmptySpec, true,
        [
            new OrderMaterialExcelRow(5, "V-0001", 10, null, null, null),
            new OrderMaterialExcelRow(6, "LUA-XANH", 20, null, null, null, "Lụa xanh", "m"),
        ]);

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, bundle), default)).Data!;

        result.OrderMaterials!.Rows.Select(r => r.Action).Should().Equal(
            SpecImportBuilder.ActionPending, SpecImportBuilder.ActionNewMaterial);
        result.MissingMaterials.Should().ContainSingle().Which.Should().Match<MissingMaterialDto>(m =>
            m.MaterialCode == "LUA-XANH" && m.SuggestedType == MaterialTypes.Trim && m.SuggestedName == "Lụa xanh" && m.SuggestedUnit == "m");
        await _orderMaterials.DidNotReceive().PreviewImportAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<OrderMaterialExcelRow>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_NormUsedOnPanel_SuggestsFabricWithColors()
    {
        var spec = EmptySpec with
        {
            HasComponents = true,
            Components = [new SpecComponentExcelRow(5, "Thân", ComponentTypes.Panel, 1, null)],
        };
        _specImport.CheckAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<SpecWorkbookDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<SpecImportPreviewDto>.Success(SpecPreview(
                new SpecImportNormRowDto(5, SpecImportBuilder.ActionNewMaterial, "x", "Xanh", "Thân", "LUA-XANH", null, null, null, null))));

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, new ExcelBundleDto(false, [], spec, false, [])), default))
            .Data!;

        result.MissingMaterials.Should().ContainSingle().Which.Should().Match<MissingMaterialDto>(m =>
            m.SuggestedType == MaterialTypes.Fabric && m.Colors.SequenceEqual(new[] { "Xanh" }));
    }

    [Fact]
    public void Parse_MaterialSheet_ReadsFullInfo()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("NVL");
            string[] headers = ["Mã NVL", "Tên NVL", "Loại", "Đơn vị", "Màu", "Khổ vải (m)"];
            for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(2, 1).Value = "v-0003";
            ws.Cell(2, 2).Value = "Kate xanh";
            ws.Cell(2, 3).Value = "Vải";
            ws.Cell(2, 4).Value = "m";
            ws.Cell(2, 5).Value = "Xanh";
            ws.Cell(2, 6).Value = 1.6;
        });

        var bundle = Sut().Parse(stream).Data!;

        bundle.HasMaterials.Should().BeTrue();
        bundle.HasOrderMaterials.Should().BeFalse();
        bundle.Materials!.Single().Should().Match<MaterialExcelRow>(r =>
            r.MaterialCode == "V-0003" && r.MaterialName == "Kate xanh" && r.MaterialType == MaterialTypes.Fabric && r.Unit == "m"
            && r.Color == "Xanh" && r.WidthM == 1.6m && r.ParseError == null);
    }

    private async Task<ExcelBundleCheckResultDto> CheckSheetAsync(params MaterialExcelRow[] rows) =>
        (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, new ExcelBundleDto(false, [], EmptySpec, false, [], true, rows)), default))
        .Data!;

    [Fact]
    public async Task CheckAsync_MaterialSheetSameCodeSameInfo_Unchanged()
    {
        var result = await CheckSheetAsync(new MaterialExcelRow(2, "V-0002", "Kate trắng", MaterialTypes.Fabric, "m", "Trắng", 1.5m, null));

        result.Materials!.Single().Action.Should().Be(SpecImportBuilder.ActionUnchanged);
    }

    [Fact]
    public async Task CheckAsync_MaterialSheetSameCodeDifferentInfo_ErrorListsDifferences()
    {
        var result = await CheckSheetAsync(new MaterialExcelRow(2, "V-0002", "Kate đen", MaterialTypes.Fabric, "m", "Đen", 1.4m, null));

        var row = result.Materials!.Single();
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.MaterialSheetConflict, "V-0002",
            $"{Messages.MaterialFieldName}, {Messages.MaterialFieldColor}, {Messages.MaterialFieldWidth}"));
    }

    [Fact]
    public async Task CheckAsync_MaterialSheetNewCode_NewAndSuggestedWithFullInfo()
    {
        var result = await CheckSheetAsync(new MaterialExcelRow(2, "V-0003", "Kate xanh", MaterialTypes.Fabric, "m", "Xanh", 1.6m, null));

        result.Materials!.Single().Action.Should().Be(SpecImportBuilder.ActionNew);
        result.MissingMaterials.Should().ContainSingle().Which.Should().Match<MissingMaterialDto>(m =>
            m.MaterialCode == "V-0003" && m.SuggestedType == MaterialTypes.Fabric && m.SuggestedName == "Kate xanh"
            && m.SuggestedColor == "Xanh" && m.SuggestedWidthM == 1.6m);
    }

    [Fact]
    public async Task CheckAsync_MaterialSheetFabricWithoutWidth_Error()
    {
        var result = await CheckSheetAsync(new MaterialExcelRow(2, "V-0003", "Kate xanh", MaterialTypes.Fabric, "m", "Xanh", null, null));

        result.Materials!.Single().Action.Should().Be(SpecImportBuilder.ActionError);
        result.MissingMaterials.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_MaterialSheetDuplicateCode_SecondRowError()
    {
        var result = await CheckSheetAsync(
            new MaterialExcelRow(2, "T-0001", "Nút", MaterialTypes.Trim, "cái", null, null, null),
            new MaterialExcelRow(3, "t-0001", "Nút", MaterialTypes.Trim, "cái", null, null, null));

        result.Materials!.Select(r => r.Action).Should().Equal(SpecImportBuilder.ActionNew, SpecImportBuilder.ActionError);
    }
    private static readonly MaterialExcelRow PendingFabric =
        new(0, "V-0009", "Kate đỏ", MaterialTypes.Fabric, "m", "Đỏ", 1.5m, null);

    [Fact]
    public async Task CheckAsync_NormUsesPendingOrderMaterial_NotWarnedAndNotCreatedAgain()
    {
        _specImport.CheckAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<SpecWorkbookDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<SpecImportPreviewDto>.Success(SpecPreview(
                new SpecImportNormRowDto(5, SpecImportBuilder.ActionNewMaterial, "x", "Đỏ", "Thân", "V-0009", null, null, null, null))));
        var dto = new ExcelBundleCheckDto(null, null, new ExcelBundleDto(false, [], EmptySpec, false, []), false, [PendingFabric]);

        var result = (await Sut().CheckAsync(dto, default)).Data!;

        result.Spec.Norms.Single().Action.Should().Be(SpecImportBuilder.ActionNew);
        result.MissingMaterials.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Kate đỏ", SpecImportBuilder.ActionUnchanged)]
    [InlineData("Kate hồng", SpecImportBuilder.ActionError)]
    public async Task CheckAsync_SheetCodeSameAsPendingOrderMaterial_ComparesInfo(string name, string expected)
    {
        var bundle = new ExcelBundleDto(false, [], EmptySpec, false, [], true, [PendingFabric with { RowNumber = 2, MaterialName = name }]);

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, bundle, false, [PendingFabric]), default)).Data!;

        result.Materials!.Single().Action.Should().Be(expected);
        result.MissingMaterials.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_MaterialDeclaredInSheet_NotWarnedAsMissing()
    {
        var bundle = new ExcelBundleDto(false, [], EmptySpec, true,
        [
            new OrderMaterialExcelRow(5, "V-0003", 10, null, null, null),
            new OrderMaterialExcelRow(6, "LUA-XANH", 20, null, null, null, "Lụa xanh", "m"),
        ], true, [new MaterialExcelRow(2, "V-0003", "Kate xanh", MaterialTypes.Fabric, "m", "Xanh", 1.6m, null)]);

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, bundle), default)).Data!;

        result.OrderMaterials!.Rows.Select(r => r.Action).Should().Equal(SpecImportBuilder.ActionNew, SpecImportBuilder.ActionNewMaterial);
        result.MissingMaterials.Where(m => m.FromSheet).Select(m => m.MaterialCode).Should().Equal("V-0003");
        result.MissingMaterials.Where(m => !m.FromSheet).Select(m => m.MaterialCode).Should().Equal("LUA-XANH");
    }
}
