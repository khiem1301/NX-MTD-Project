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
            new() { MaterialId = 1, MaterialName = "V-0001", Color = "Hồng", Unit = "m", Status = MaterialStatuses.Active },
        });
        _specImport.CheckAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<SpecWorkbookDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<SpecImportPreviewDto>.Success(SpecPreview()));
    }

    private ExcelBundleService Sut() => new(Uow, new ClosedXmlProductParser(), new ClosedXmlSpecWorkbookParser(),
        new ClosedXmlOrderMaterialParser(), _productImport, _specImport, _orderMaterials);

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
    public void Parse_OrderMaterialSheet_ReadsNameAndOptionalUnit()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("NVL");
            string[] headers = ["Tên NVL", "Đơn vị", "Số cần"];
            for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(2, 1).Value = "lua-xanh";
            ws.Cell(2, 2).Value = "m";
            ws.Cell(2, 3).Value = 10;
        });

        var bundle = Sut().Parse(stream).Data!;

        bundle.HasProducts.Should().BeFalse();
        bundle.HasOrderMaterials.Should().BeTrue();
        bundle.OrderMaterials.Single().Should().Match<OrderMaterialExcelRow>(r =>
            r.MaterialName == "LUA-XANH" && r.Unit == "m" && r.RequiredQty == 10);
    }

    [Fact]
    public async Task CheckAsync_NoOrder_UnknownMaterialIsNewMaterialWithSuggestion()
    {
        var bundle = new ExcelBundleDto(false, [], EmptySpec, true,
        [
            new OrderMaterialExcelRow(5, "V-0001", 10, null, null, null),
            new OrderMaterialExcelRow(6, "LUA-XANH", 20, null, null, null, "m"),
        ]);

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, bundle), default)).Data!;

        result.OrderMaterials!.Rows.Select(r => r.Action).Should().Equal(
            SpecImportBuilder.ActionPending, SpecImportBuilder.ActionNewMaterial);
        result.MissingMaterials.Should().ContainSingle().Which.Should().Match<MissingMaterialDto>(m =>
            m.MaterialName == "LUA-XANH" && m.SuggestedType == MaterialTypes.Trim && m.SuggestedUnit == "m");
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
                new SpecImportNormRowDto(5, SpecImportBuilder.ActionNewMaterial, "x", "Xanh", "Thân", "LUA-XANH", null, null, null))));

        var result = (await Sut().CheckAsync(new ExcelBundleCheckDto(null, null, new ExcelBundleDto(false, [], spec, false, [])), default))
            .Data!;

        result.MissingMaterials.Should().ContainSingle().Which.Should().Match<MissingMaterialDto>(m =>
            m.SuggestedType == MaterialTypes.Fabric && m.Colors.SequenceEqual(new[] { "Xanh" }));
    }
}
