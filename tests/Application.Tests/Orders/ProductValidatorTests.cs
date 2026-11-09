using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Giới hạn biên của mã hàng và định mức mã hàng (khớp độ rộng cột trong database).</summary>
public class ProductValidatorTests
{
    private static bool ProductIsValid(string code = "AO-01", string name = "Áo", string? image = null, string? description = null,
        IReadOnlyList<string>? extraImages = null) =>
        new SaveProductDtoValidator().Validate(new SaveProductDto(code, name, image, description, extraImages)).IsValid;

    private static string ImageKey(int i) => $"PRODUCT/2026/11/{i:x32}.jpg";

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void ProductCode_MaxLength(int length, bool expected) => ProductIsValid(code: new string('A', length)).Should().Be(expected);

    [Fact]
    public void ProductCode_Length_IsCountedAfterTrim_LikeTheStoredValue() =>
        ProductIsValid(code: $" {new string('A', 30)} ").Should().BeTrue();

    [Theory]
    [InlineData("AO-01.A_2", true)]
    [InlineData("AO 01", false)]
    [InlineData("ÁO01", false)]
    [InlineData("AO/01", false)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    public void ProductCode_Pattern(string code, bool expected) => ProductIsValid(code: code).Should().Be(expected);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void ProductName_MaxLength(int length, bool expected) => ProductIsValid(name: new string('x', length)).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ProductName_Required(string name) => ProductIsValid(name: name).Should().BeFalse();

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Description_MaxLength(int length, bool expected) => ProductIsValid(description: new string('x', length)).Should().Be(expected);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void SampleImageUrl_MaxLength(int length, bool expected) =>
        ProductIsValid(image: "https://a.vn/" + new string('x', length - 13)).Should().Be(expected);

    // ---------- ảnh phụ ----------

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void ExtraImages_AtMostFive(int count, bool expected) =>
        ProductIsValid(extraImages: Enumerable.Range(1, count).Select(ImageKey).ToList()).Should().Be(expected);

    [Fact]
    public void ExtraImages_CanBeOmitted() => ProductIsValid(extraImages: null).Should().BeTrue();

    [Theory]
    [InlineData("https://a.vn/x.jpg")]
    [InlineData("../secret.png")]
    [InlineData("PRODUCT/2026/11/short.jpg")]
    [InlineData("PRODUCT/2026/11/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.gif")]
    [InlineData("")]
    public void ExtraImages_MustBeUploadedImageKeys(string key) =>
        ProductIsValid(extraImages: [ImageKey(1), key]).Should().BeFalse();

    [Fact]
    public void ExtraImages_TheSameImageTwice_IsRejected_EvenWithSpaces() =>
        ProductIsValid(extraImages: [ImageKey(1), " " + ImageKey(1) + " "]).Should().BeFalse();

    [Fact]
    public void ExtraImages_ReportsOneMessageForTooMany()
    {
        var dto = new SaveProductDto("AO-01", "Áo", null, null, Enumerable.Range(1, 6).Select(ImageKey).ToList());

        new SaveProductDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage).Should().Equal("Tối đa 5 ảnh phụ.");
    }

    // ---------- định mức ----------

    private static SaveProductSpecDto Spec(short pieces = 1,
        decimal? normLength = null, decimal? normWidth = null, string color = "Đỏ", string size = "M", string? note = null,
        string component = "Thân", int materialId = 1) =>
        new([new SaveProductComponentDto(null, component, "Panel", pieces)],
            [new SaveProductVariantDto(null, color, size)],
            [new SaveProductNormDto("Thân", "Đỏ", "M", materialId, normLength, normWidth, note)], [], []);

    private static bool SpecIsValid(SaveProductSpecDto dto) => new SaveProductSpecDtoValidator().Validate(dto).IsValid;

    [Theory]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100, false)]
    public void PiecesPerGarment_Boundaries(short pieces, bool expected) => SpecIsValid(Spec(pieces: pieces)).Should().Be(expected);

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("99999.99", true)]
    [InlineData("0", false)]
    [InlineData("100000", false)]
    [InlineData("1.234", false)]
    public void NormSize_Boundaries(string value, bool expected)
    {
        var size = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        SpecIsValid(Spec(normLength: size)).Should().Be(expected);
        SpecIsValid(Spec(normWidth: size)).Should().Be(expected);
    }

    [Fact]
    public void Size_CanBeLeftEmpty() => SpecIsValid(Spec()).Should().BeTrue();

    [Fact]
    public void OversizedValue_ReturnsAVietnameseMessageOnTheRightField()
    {
        var errors = new SaveProductSpecDtoValidator().Validate(Spec(normWidth: 100_000m)).Errors;

        errors.Select(e => e.ErrorMessage).Should().Equal("Chiều rộng chi tiết tối đa 99.999,99 cm.");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ComponentName_MaxLength(int length, bool expected) => SpecIsValid(Spec(component: new string('x', length))).Should().Be(expected);

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void VariantColor_MaxLength(int length, bool expected) => SpecIsValid(Spec(color: new string('x', length))).Should().Be(expected);

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void VariantSizeCode_MaxLength(int length, bool expected) => SpecIsValid(Spec(size: new string('X', length))).Should().Be(expected);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void NormNote_MaxLength(int length, bool expected) => SpecIsValid(Spec(note: new string('x', length))).Should().Be(expected);

    private static SaveProductSpecDto Counts(int components, int variants, int norms) =>
        new(Enumerable.Range(0, components).Select(i => new SaveProductComponentDto(null, $"C{i}", "Panel", 1)).ToList(),
            Enumerable.Range(0, variants).Select(i => new SaveProductVariantDto(null, $"M{i}", "M")).ToList(),
            Enumerable.Range(0, norms).Select(i => new SaveProductNormDto("C0", "M0", "M", i + 1, null, null, null)).ToList(), [], []);

    [Theory]
    [InlineData(200, 200, 5000, true)]
    [InlineData(201, 0, 0, false)]
    [InlineData(0, 201, 0, false)]
    [InlineData(0, 0, 5001, false)]
    public void ListSizes_Boundaries(int components, int variants, int norms, bool expected) =>
        SpecIsValid(Counts(components, variants, norms)).Should().Be(expected);

    [Fact]
    public void OversizedList_ReportsOneMessageAndSkipsPerItemChecks()
    {
        var invalidItems = Enumerable.Range(0, 5001).Select(_ => new SaveProductNormDto("", "", "", 0, null, null, null)).ToList();
        var dto = new SaveProductSpecDto([], [], invalidItems, [], []);

        new SaveProductSpecDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage)
            .Should().Equal("Tối đa 5.000 dòng định mức (tính theo từng màu – size) cho mỗi lần lưu.");
    }

    [Fact]
    public void MissingLists_AreRejectedWithoutCrashing()
    {
        var result = new SaveProductSpecDtoValidator().Validate(new SaveProductSpecDto(null!, null!, null!, null!, null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(5);
    }

    private static SaveProductSpecDto WithMarker(int no = 1, decimal? length = 70, decimal? width = 50, string size = "M") =>
        Spec() with { Markers = [new SaveProductMarkerDto(no, size, length, width)], MarkerFabrics = [new SaveProductMarkerFabricDto(no, "Đỏ", 1)] };

    [Theory]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(0, false)]
    [InlineData(21, false)]
    public void MarkerNo_Boundaries(int no, bool expected) => SpecIsValid(WithMarker(no)).Should().Be(expected);

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("99999.99", true)]
    [InlineData("0", false)]
    [InlineData("100000", false)]
    [InlineData("1.234", false)]
    public void MarkerSize_Boundaries(string value, bool expected)
    {
        var size = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        SpecIsValid(WithMarker(length: size)).Should().Be(expected);
        SpecIsValid(WithMarker(width: size)).Should().Be(expected);
    }

    [Fact]
    public void MarkerSize_IsRequired()
    {
        SpecIsValid(WithMarker(length: null)).Should().BeFalse();
        SpecIsValid(WithMarker(width: null)).Should().BeFalse();
        SpecIsValid(WithMarker(size: " ")).Should().BeFalse();
    }

    [Fact]
    public void MarkerFabric_NeedsAColorAndAMaterial()
    {
        SpecIsValid(WithMarker() with { MarkerFabrics = [new SaveProductMarkerFabricDto(1, "", 1)] }).Should().BeFalse();
        SpecIsValid(WithMarker() with { MarkerFabrics = [new SaveProductMarkerFabricDto(1, "Đỏ", 0)] }).Should().BeFalse();
    }

    [Fact]
    public void OversizedMarkerList_ReportsOneMessage()
    {
        var markers = Enumerable.Range(0, 4001).Select(_ => new SaveProductMarkerDto(0, "", null, null)).ToList();

        new SaveProductSpecDtoValidator().Validate(Spec() with { Markers = markers }).Errors.Select(e => e.ErrorMessage)
            .Should().Equal("Danh sách bìa quá dài.");
    }

    [Fact]
    public void NormWithoutMaterial_Fails() => SpecIsValid(Spec(materialId: 0)).Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ComponentName_Required(string name) => SpecIsValid(Spec(component: name)).Should().BeFalse();
}
