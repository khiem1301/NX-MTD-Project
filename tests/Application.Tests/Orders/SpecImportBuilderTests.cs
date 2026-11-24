using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>
/// UTCID750–UTCID786: SpecImportBuilder (constructor + kết quả gộp). Assert khóa theo
/// sheet SpecImportBuilder trong UNIT_TEST_CASES_REBASELINE_v3.1.
/// </summary>
public class SpecImportBuilderTests
{
    private const string Red = "Đỏ";
    private const string Body = "Thân trước";
    private const string Sleeve = "Tay";

    [Fact]
    public void UTCID750_Build_EmptySpec_AllRowsNewAndSpecChanged()
    {
        // Arrange — spec chưa có thành phần / định mức / nhóm; còn biến thể màu để dòng NVL và nhóm hợp lệ
        var spec = Spec(variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(
            components: [Panel(4, Body, 2), Panel(5, Sleeve, 2)],
            norms: [NormExcel(4, Red, Body, "M1"), NormExcel(5, Red, Sleeve, "M2")],
            groups: [GroupExcel(4, 1, [Body], [Red])]);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1"), Mat(2, "M2")), replace: false);

        // Assert
        builder.Components.Should().HaveCount(2);
        builder.Norms.Should().HaveCount(2);
        builder.MarkerGroups.Should().HaveCount(1);
        builder.AllActions.Should().OnlyContain(a => a == SpecImportBuilder.ActionNew);
        ErrorCount(builder).Should().Be(0);
        builder.SpecChanged.Should().BeTrue();
    }

    [Fact]
    public void UTCID751_Build_WorkbookMatchesSpec_AllUnchangedAndSpecNotChanged()
    {
        // Arrange
        var spec = Spec(
            components: [Part(1, Body, "Panel", 2), Part(2, Sleeve, "Panel", 2)],
            variants: [ColorSize(1, Red, "S")],
            norms: [Norm(10, Body, Red, 1, "M1"), Norm(11, Sleeve, Red, 2, "M2")],
            markerComponents: [new ProductMarkerComponentDto(1, Body)],
            markerFabrics: [Fabric(1, Red)]);
        var workbook = Book(
            components: [Panel(4, Body, 2), Panel(5, Sleeve, 2)],
            norms: [NormExcel(4, Red, Body, "M1"), NormExcel(5, Red, Sleeve, "M2")],
            groups: [GroupExcel(4, 1, [Body], [Red])]);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1"), Mat(2, "M2")), replace: false);

        // Assert
        builder.AllActions.Should().OnlyContain(a => a == SpecImportBuilder.ActionUnchanged);
        builder.SpecChanged.Should().BeFalse();
    }

    [Fact]
    public void UTCID752_Build_PiecesChanged_ActionUpdate()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 2)]);
        var workbook = Book(components: [Panel(4, Body, 1)]);

        // Act
        var builder = Build(spec, workbook);

        // Assert
        builder.Components.Should().ContainSingle();
        builder.Components[0].Action.Should().Be(SpecImportBuilder.ActionUpdate);
        builder.Components[0].Error.Should().BeNull();
    }

    [Fact]
    public void UTCID753_Build_ReplaceUnusedComponent_ListedInRemoved()
    {
        // Arrange
        var spec = Spec(components: [Part(1, "Túi", "Trim", 1), Part(2, Body, "Panel", 2)]);
        var workbook = Book(components: [Panel(4, Body, 2)]);

        // Act
        var builder = Build(spec, workbook, replace: true, usage: SpecImportUsage.None);

        // Assert
        builder.RemovedNames.Should().Contain(n => n.Contains("Túi"));
        builder.Removed.Components.Should().Be(1);
        builder.Spec.Components.Should().NotContain(c => c.ComponentName == "Túi");
    }

    [Fact]
    public void UTCID754_Build_ComponentName100_Valid()
    {
        // Arrange
        var name = new string('A', 100);
        var workbook = Book(components: [Panel(4, name, 1)]);

        // Act
        var row = Build(Spec(), workbook).Components.Single();

        // Assert
        row.Action.Should().NotBe(SpecImportBuilder.ActionError);
        row.Error.Should().BeNull();
        row.Action.Should().Be(SpecImportBuilder.ActionNew);
    }

    [Fact]
    public void UTCID755_Build_ComponentName101_ActionError()
    {
        // Arrange
        var workbook = Book(components: [Panel(4, new string('A', 101), 1)]);

        // Act
        var row = Build(Spec(), workbook).Components.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.SpecImportNameTooLong);
    }

    [Fact]
    public void UTCID756_Build_Pieces1And99_Valid()
    {
        // Arrange
        var workbook = Book(components: [Panel(4, Body, 1), Panel(5, Sleeve, 99)]);

        // Act
        var rows = Build(Spec(), workbook).Components;

        // Assert
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Action != SpecImportBuilder.ActionError && r.Error == null);
        rows.Select(r => r.PiecesPerGarment).Should().Equal((short)1, (short)99);
    }

    [Fact]
    public void UTCID757_Build_Pieces0_ActionError()
    {
        // Arrange
        var workbook = Book(components: [ComponentRow(4, Body, ComponentTypes.Panel, 0)]);

        // Act
        var row = Build(Spec(), workbook).Components.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.SpecImportPiecesRange);
    }

    [Fact]
    public void UTCID758_Build_Pieces100_ActionError()
    {
        // Arrange
        var workbook = Book(components: [ComponentRow(4, Body, ComponentTypes.Panel, 100)]);

        // Act
        var row = Build(Spec(), workbook).Components.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.SpecImportPiecesRange);
    }

    [Fact]
    public void UTCID759_Build_Note200_Valid()
    {
        // Arrange
        var note = new string('G', 200);
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M1", note)]);

        // Act
        var row = Build(spec, workbook, Materials(Mat(1, "M1"))).Norms.Single();

        // Assert
        row.Action.Should().NotBe(SpecImportBuilder.ActionError);
        row.Error.Should().BeNull();
        row.SpecNote.Should().Be(note);
    }

    [Fact]
    public void UTCID760_Build_Note201_ActionError()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M1", new string('G', 201))]);

        // Act
        var row = Build(spec, workbook, Materials(Mat(1, "M1"))).Norms.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.SpecImportNoteTooLong);
    }

    [Fact]
    public void UTCID761_Build_MarkerNo99_Valid()
    {
        // Arrange — case: MarkerNo max = 99
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(groups: [GroupExcel(4, 99, [Body], [Red])]);

        // Act
        var row = Build(spec, workbook).MarkerGroups.Single();

        // Assert — MarkerNo 99 hợp lệ. ⚠ Code MaxMarkerNo = 20 → Action=Error.
        row.Action.Should().NotBe(SpecImportBuilder.ActionError);
        row.Error.Should().BeNull();
    }

    [Fact]
    public void UTCID762_Build_MarkerNo100_ActionError()
    {
        // Arrange — case: MarkerNo max = 99
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(groups: [GroupExcel(4, 100, [Body], [Red])]);

        // Act
        var row = Build(spec, workbook).MarkerGroups.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportMarkerNoRange, SaveProductSpecDtoValidator.MaxMarkerNo));
    }

    [Fact]
    public void UTCID763_Build_EmptyWorkbook_NoRowsAndSpecNotChanged()
    {
        // Arrange
        var workbook = Book();

        // Act
        var builder = Build(Spec(), workbook);

        // Assert
        builder.Components.Should().BeEmpty();
        builder.Norms.Should().BeEmpty();
        builder.MarkerGroups.Should().BeEmpty();
        builder.OrderMarkers.Should().BeEmpty();
        ErrorCount(builder).Should().Be(0);
        builder.SpecChanged.Should().BeFalse();
    }

    [Fact]
    public void UTCID764_Build_ColorOnlySize_StrippedFromMergedSpec()
    {
        // Arrange — màu Vàng chỉ có biến thể size "?"
        var spec = Spec(
            components: [Part(1, Body, "Panel", 1)],
            variants: [ColorSize(1, "Vàng", SpecImportBuilder.ColorOnlySize)]);
        var workbook = Book(norms: [NormExcel(4, "Vàng", Body, "M1")]);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1")));

        // Assert
        builder.Norms.Should().ContainSingle(n => n.Action != SpecImportBuilder.ActionError);
        builder.Spec.Variants.Should().NotContain(v => v.SizeCode == SpecImportBuilder.ColorOnlySize);
    }

    [Fact]
    public void UTCID765_Build_RealSizeQuestionMark_Kept()
    {
        // Arrange — đơn có size thật tên "?"
        var spec = Spec(
            components: [Part(1, Body, "Panel", 1)],
            variants: [ColorSize(1, Red, "?")]);
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M1")]);
        var order = new OrderMarkerProductDto(1, "AO01", ["?"], [], []);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1")), order);

        // Assert — size thật "?" phải còn. ⚠ Bị strip như color-only.
        builder.Spec.Variants.Should().Contain(v => v.SizeCode == "?");
    }

    [Fact]
    public void UTCID766_Build_NormPair_ReplacedByFileMaterialsOnly()
    {
        // Arrange — cặp Đỏ|Thân trước đang có M1 + M2; file chỉ còn M1
        var spec = Spec(
            components: [Part(1, Body, "Panel", 1)],
            variants: [ColorSize(1, Red, "S")],
            norms: [Norm(10, Body, Red, 1, "M1"), Norm(11, Body, Red, 2, "M2")]);
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M1")]);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1"), Mat(2, "M2")), replace: false);

        // Assert
        builder.Spec.Norms.Where(n => n.Color == Red && n.ComponentName == Body)
            .Select(n => n.MaterialId).Should().Equal(1);
    }

    [Fact]
    public void UTCID767_Build_ReplaceUsedComponent_NotRemoved()
    {
        // Arrange
        var spec = Spec(components: [Part(1, "Túi", "Trim", 1)]);
        var workbook = Book(components: [Panel(4, Body, 1)], hasComponents: true);

        // Act
        var builder = Build(spec, workbook, replace: true, usage: Usage(components: ["Túi"]));

        // Assert
        builder.RemovedNames.Should().NotContain(n => n.Contains("Túi"));
        builder.Spec.Components.Should().Contain(c => c.ComponentName == "Túi");
    }

    [Fact]
    public void UTCID768_Build_ReplaceUsedNormPair_Kept()
    {
        // Arrange — cặp Đỏ|Thân trước|M1 đã dùng; file không có cặp này
        var spec = Spec(
            components: [Part(1, Body, "Panel", 1)],
            variants: [ColorSize(1, Red, "S")],
            norms: [Norm(10, Body, Red, 1, "M1")]);
        var workbook = Book(hasNorms: true);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1")), replace: true, usage: Usage(normPairs: [$"{Red}|{Body}"]));

        // Assert
        builder.Spec.Norms.Should().Contain(n => n.Color == Red && n.ComponentName == Body && n.MaterialId == 1);
        builder.RemovedNames.Should().NotContain(n => n.Contains(Body));
    }

    [Fact]
    public void UTCID769_Build_ReplaceUsedGroup_Kept()
    {
        // Arrange — nhóm 1 đã dùng; file không có nhóm 1
        var spec = Spec(
            components: [Part(1, Body, "Panel", 1)],
            variants: [ColorSize(1, Red, "S")],
            markerComponents: [new ProductMarkerComponentDto(1, Body)],
            markerFabrics: [Fabric(1, Red)]);
        var workbook = Book(hasGroups: true);

        // Act
        var builder = Build(spec, workbook, replace: true, usage: Usage(groups: [1]));

        // Assert
        builder.Spec.MarkerComponents.Should().Contain(m => m.MarkerNo == 1);
        builder.Spec.MarkerColors.Should().Contain(m => m.MarkerNo == 1);
        builder.Removed.MarkerGroups.Should().Be(0);
    }

    [Fact]
    public void UTCID770_Build_ReplaceUsedOrderMarker_Kept()
    {
        // Arrange — OM1 đã dùng; file có bảng bìa nhưng không có OM1
        var spec = Spec(
            variants: [ColorSize(1, Red, "S")],
            markerFabrics: [Fabric(1, Red)]);
        var order = Order(Marker(1, 1, "OM1"));
        var workbook = Book(hasMarkers: true);

        // Act
        var builder = Build(spec, workbook, orderProduct: order, replace: true, usage: SpecImportUsage.None);

        // Assert — OM1 phải được giữ như thành phần / định mức. ⚠ Không có usage-keep cho bìa của đơn → bị xóa.
        builder.Markers.Should().NotBeNull();
        builder.Markers!.Markers.Should().Contain(m => m.MarkerCode == "OM1");
    }

    [Fact]
    public void UTCID771_Build_OnlyOrderMarkersChanged_ChangeIsVisible()
    {
        // Arrange — định mức không đổi; file chỉ sửa bìa của đơn
        var spec = Spec(
            variants: [ColorSize(1, Red, "S")],
            markerFabrics: [Fabric(1, Red)]);
        var order = Order(Marker(500, 1, "1A", length: 2.2m));
        var workbook = Book(markers:
        [
            MarkerExcel(4, 1, "1A", 3.1m, 1.5m, ratios: [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 1)]),
        ]);

        // Act
        var builder = Build(spec, workbook, orderProduct: order, replace: false);

        // Assert — SpecChanged hoặc cờ riêng phải thấy thay đổi bìa
        builder.OrderMarkers.Should().ContainSingle();
        builder.OrderMarkers[0].Action.Should().Be(SpecImportBuilder.ActionUpdate);
        (builder.SpecChanged || builder.Changes.OrderMarkers > 0).Should().BeTrue();
    }

    [Fact]
    public void UTCID772_Build_NoOrderProduct_SkipsOrderMarkersWithoutThrowing()
    {
        // Arrange
        var workbook = Book(markers: [MarkerExcel(4, 1, "1A", 2m, 1.5m)], hasMarkers: true);

        // Act
        var act = () => Build(Spec(), workbook, orderProduct: null);

        // Assert
        var builder = act.Should().NotThrow().Subject;
        builder.OrderMarkers.Should().BeEmpty();
        builder.Notes.Should().Contain(Messages.SpecImportMarkersSkipped);
    }

    [Fact]
    public void UTCID773_Build_PiecesNotANumber_ActionError()
    {
        // Arrange — parser gắn ParseError khi ô Pieces = "abc"
        const string parseError = "Cột Số chi tiết / áo không phải số.";
        var workbook = Book(components: [ComponentRow(4, Body, ComponentTypes.Panel, null, parseError)]);

        // Act
        var row = Build(Spec(), workbook).Components.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(parseError);
    }

    [Fact]
    public void UTCID774_Build_DuplicateComponent_ErrorAndDuplicateCount()
    {
        // Arrange
        var workbook = Book(components: [Panel(4, Sleeve, 1), Panel(5, Sleeve, 1)]);

        // Act
        var builder = Build(Spec(), workbook);

        // Assert
        builder.Components[0].Action.Should().Be(SpecImportBuilder.ActionNew);
        builder.Components[1].Action.Should().Be(SpecImportBuilder.ActionError);
        builder.Components[1].Error.Should().Be(string.Format(Messages.SpecImportDuplicateRow, 4));
        builder.DuplicateCount.Should().Be(1);
        ErrorCount(builder).Should().Be(1);
    }

    [Fact]
    public void UTCID775_Build_UnknownColor_ActionError()
    {
        // Arrange — "Tím" không có trong biến thể / ProductColor của spec
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms: [NormExcel(4, "Tím", Body, "M1")]);

        // Act
        var row = Build(spec, workbook, Materials(Mat(1, "M1"))).Norms.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportColorUnknown, "Tím"));
    }

    [Fact]
    public void UTCID776_Build_UnknownComponent_ActionError()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms: [NormExcel(4, Red, "Cầu vai", "M1")]);

        // Act
        var row = Build(spec, workbook, Materials(Mat(1, "M1"))).Norms.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportComponentUnknown, "Cầu vai"));
    }

    [Fact]
    public void UTCID777_Build_UnknownMaterial_ActionError()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms: [NormExcel(4, Red, Body, "M9")]);

        // Act
        var row = Build(spec, workbook, Materials(Mat(1, "M1"))).Norms.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportMaterialUnknown, "M9"));
    }

    [Fact]
    public void UTCID778_Build_DuplicateNorm_ActionError()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(norms:
        [
            NormExcel(4, Red, Body, "M1"),
            NormExcel(5, Red, Body, "M1"),
        ]);

        // Act
        var builder = Build(spec, workbook, Materials(Mat(1, "M1")));

        // Assert
        builder.Norms[1].Action.Should().Be(SpecImportBuilder.ActionError);
        builder.Norms[1].Error.Should().Be(string.Format(Messages.SpecImportDuplicateRow, 4));
        builder.DuplicateCount.Should().Be(1);
        ErrorCount(builder).Should().Be(1);
    }

    [Fact]
    public void UTCID779_Build_GroupWithoutPanel_ActionError()
    {
        // Arrange — nhóm chỉ có chi tiết TRIM
        var spec = Spec(components: [Part(1, "Nhãn", "Trim", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(groups: [GroupExcel(4, 1, ["Nhãn"], [Red])]);

        // Act
        var row = Build(spec, workbook).MarkerGroups.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportComponentNotPanel, "Nhãn"));
    }

    [Fact]
    public void UTCID780_Build_GroupAllColorsUnknown_ActionError()
    {
        // Arrange
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(groups: [GroupExcel(4, 1, [Body], ["Tím", "Hồng"])]);

        // Act
        var row = Build(spec, workbook).MarkerGroups.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportColorUnknown, "Tím"));
    }

    [Fact]
    public void UTCID781_Build_GroupMixedColors_SkipsUnknownColor()
    {
        // Arrange — Đỏ đã có, Tím lạ
        var spec = Spec(components: [Part(1, Body, "Panel", 1)], variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(groups: [GroupExcel(4, 1, [Body], [Red, "Tím"])]);

        // Act
        var builder = Build(spec, workbook);
        var row = builder.MarkerGroups.Single();

        // Assert
        row.Action.Should().NotBe(SpecImportBuilder.ActionError);
        row.Error.Should().BeNull();
        row.Colors.Should().Equal(Red);
        builder.Notes.Should().Contain(string.Format(Messages.SpecImportGroupColorsSkipped, 1, "Tím"));
    }

    [Fact]
    public void UTCID782_Build_OtherProductMarker_Skipped()
    {
        // Arrange
        var workbook = Book(markers: [MarkerExcel(4, 1, "1A", 2m, 1.5m, productCode: "AO02")]);

        // Act
        var builder = Build(Spec(), workbook, orderProduct: Order(), productCode: "AO01");

        // Assert
        builder.OrderMarkers.Should().BeEmpty();
        builder.Notes.Should().Contain(string.Format(Messages.SpecImportOtherProductRows, 1));
    }

    [Fact]
    public void UTCID783_Build_MarkerWidthNull_ActionError()
    {
        // Arrange
        var workbook = Book(markers: [MarkerExcel(4, 1, "1A", 2m, null)]);

        // Act
        var row = Build(Spec(), workbook, orderProduct: Order()).OrderMarkers.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.Required("khổ bìa"));
    }

    [Fact]
    public void UTCID784_Build_MarkerSizeNotInOrder_ActionError()
    {
        // Arrange — đơn có S, M; bìa chỉ khai XL
        var workbook = Book(
            markers: [MarkerExcel(4, 1, "1A", 2m, 1.5m, ratios: [new MarkerRatioDto("XL", 1)])],
            sizeColumns: ["S", "M", "XL"]);

        // Act
        var row = Build(Spec(), workbook, orderProduct: Order()).OrderMarkers.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(Messages.SpecImportNoSizeOfOrder);
    }

    [Fact]
    public void UTCID785_Build_UnknownMarkerGroup_ActionError()
    {
        // Arrange — bìa trỏ nhóm 9, spec không có nhóm này
        var spec = Spec(variants: [ColorSize(1, Red, "S")]);
        var workbook = Book(markers: [MarkerExcel(4, 9, "9A", 2m, 1.5m)]);

        // Act
        var row = Build(spec, workbook, orderProduct: Order()).OrderMarkers.Single();

        // Assert
        row.Action.Should().Be(SpecImportBuilder.ActionError);
        row.Error.Should().Be(string.Format(Messages.SpecImportGroupUnknown, 9));
    }

    [Fact]
    public void UTCID786_Build_NullWorkbook_ThrowsArgumentNullException()
    {
        // Arrange
        var act = () => new SpecImportBuilder(Spec(), Materials(), null, "AO01", null!);

        // Act + Assert — workbook null phải là ArgumentNullException. ⚠ Code dereference → NullReferenceException.
        act.Should().Throw<ArgumentNullException>();
    }

    private static int ErrorCount(SpecImportBuilder builder) =>
        builder.AllActions.Count(a => a == SpecImportBuilder.ActionError);

    private static SpecImportBuilder Build(
        ProductSpecDto spec,
        SpecWorkbookDto workbook,
        Dictionary<string, SpecImportMaterial>? materials = null,
        OrderMarkerProductDto? orderProduct = null,
        string productCode = "AO01",
        bool replace = false,
        SpecImportUsage? usage = null) =>
        new(spec, materials ?? new Dictionary<string, SpecImportMaterial>(), orderProduct, productCode, workbook, replace, usage);

    private static ProductSpecDto Spec(
        ProductComponentDto[]? components = null,
        ProductVariantDto[]? variants = null,
        ProductNormDto[]? norms = null,
        ProductMarkerComponentDto[]? markerComponents = null,
        ProductMarkerFabricDto[]? markerFabrics = null) =>
        new(1, components ?? [], variants ?? [], norms ?? [], markerComponents ?? [], markerFabrics ?? []);

    private static SpecWorkbookDto Book(
        SpecComponentExcelRow[]? components = null,
        SpecNormExcelRow[]? norms = null,
        SpecMarkerGroupExcelRow[]? groups = null,
        SpecOrderMarkerExcelRow[]? markers = null,
        string[]? sizeColumns = null,
        bool? hasComponents = null,
        bool? hasNorms = null,
        bool? hasGroups = null,
        bool? hasMarkers = null) =>
        new(
            hasComponents ?? components is { Length: > 0 },
            components ?? [],
            hasNorms ?? norms is { Length: > 0 },
            norms ?? [],
            hasGroups ?? groups is { Length: > 0 },
            groups ?? [],
            hasMarkers ?? markers is { Length: > 0 },
            markers ?? [],
            sizeColumns ?? []);

    private static Dictionary<string, SpecImportMaterial> Materials(params SpecImportMaterial[] items) =>
        items.ToDictionary(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase);

    private static SpecImportMaterial Mat(int id, string code) => new(id, code, $"NVL {code}", "m", Red);

    private static SpecImportUsage Usage(string[]? components = null, string[]? normPairs = null, int[]? groups = null) =>
        new(
            new HashSet<string>(components ?? [], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(normPairs ?? [], StringComparer.OrdinalIgnoreCase),
            (groups ?? []).ToHashSet());

    private static ProductComponentDto Part(int id, string name, string type, short pieces) =>
        new(id, name, type, pieces, "Active");

    private static ProductVariantDto ColorSize(int id, string color, string size) =>
        new(id, color, size, "Active");

    private static ProductNormDto Norm(int lineId, string component, string color, int materialId, string code, string? note = null) =>
        new(lineId, component, color, materialId, code, $"NVL {code}", "m", color, note);

    private static ProductMarkerFabricDto Fabric(int no, string color) =>
        new(no, color, 1, "M1", "NVL M1", color);

    private static SpecComponentExcelRow Panel(int row, string name, short pieces) =>
        new(row, name, ComponentTypes.Panel, pieces, null);

    private static SpecComponentExcelRow ComponentRow(int row, string name, string? type, short? pieces, string? parseError = null) =>
        new(row, name, type, pieces, parseError);

    private static SpecNormExcelRow NormExcel(int row, string color, string component, string code, string? note = null) =>
        new(row, color, component, code, note, null);

    private static SpecMarkerGroupExcelRow GroupExcel(int row, int? markerNo, string[] components, string[] colors) =>
        new(row, markerNo, components, colors, null);

    private static SpecOrderMarkerExcelRow MarkerExcel(
        int row, int? markerNo, string code, decimal? length, decimal? width,
        string? productCode = null, MarkerRatioDto[]? ratios = null, string status = "ACTIVE") =>
        new(row, productCode, markerNo, code, length, width, status,
            ratios ?? [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 1)], null);

    private static OrderMarkerProductDto Order(params OrderMarkerDto[] markers) =>
        new(1, "AO01", ["S", "M"], [], markers);

    private static OrderMarkerDto Marker(int id, int no, string code, decimal length = 2m, decimal width = 1.5m) =>
        new(id, no, code, length, width, "Active", [new MarkerRatioDto("S", 1), new MarkerRatioDto("M", 1)]);
}
