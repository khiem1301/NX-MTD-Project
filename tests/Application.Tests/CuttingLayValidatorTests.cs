using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>DEV-ALL-43: chiều dài bàn cắt; giá trị biên của bàn cắt và phiếu trả vải thừa.</summary>
public class CuttingLayValidatorTests
{
    private static SaveCuttingLayDto Lay(decimal? length, int ply = 5, int ratio = 2, string shift = "MORNING",
        DateOnly? date = null, string? note = null, string? reason = null) =>
        new(1, date ?? new DateOnly(2026, 11, 1), shift, 1, ply, length, note, [new SaveLayLineDto(1, null, ratio, null, reason)]);

    private static IEnumerable<string> Errors(SaveCuttingLayDto dto) =>
        new SaveCuttingLayDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage);

    [Theory]
    [InlineData(null, true)]
    [InlineData("RECORDED", true)]
    [InlineData("LOCKED", true)]
    [InlineData("Nope", false)]
    public void LayQuery_Status(string? status, bool expected) =>
        new CuttingLayQueryValidator().Validate(new CuttingLayQuery { Status = status })
            .IsValid.Should().Be(expected);

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("10", true)]
    [InlineData("1000", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1000.01", false)]
    [InlineData("1.234", false)]
    public void LayLength_Boundaries(string value, bool expected) =>
        new SaveCuttingLayDtoValidator().Validate(Lay(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)))
            .IsValid.Should().Be(expected);

    [Fact]
    public void LayLength_IsRequired() =>
        new SaveCuttingLayDtoValidator().Validate(Lay(null)).Errors.Select(e => e.ErrorMessage)
            .Should().Equal("Vui lòng nhập chiều dài bàn cắt.");

    [Theory]
    [InlineData(1, true)]
    [InlineData(32767, true)]
    [InlineData(0, false)]
    [InlineData(32768, false)]
    public void PlyCount_Boundaries(int ply, bool expected) => Errors(Lay(2m, ply: ply)).Any().Should().Be(!expected);

    [Theory]
    [InlineData(1, true)]
    [InlineData(32767, true)]
    [InlineData(0, false)]
    [InlineData(32768, false)]
    public void Ratio_Boundaries(int ratio, bool expected) => Errors(Lay(2m, ratio: ratio)).Any().Should().Be(!expected);

    [Fact]
    public void Shift_PlyCount_Ratio_AndMissingDate_ReportInVietnamese()
    {
        Errors(Lay(2m, shift: "NIGHT")).Should().Equal(NX_MTD.Application.Common.WarehouseMessages.LayShiftInvalid);
        Errors(Lay(2m, ply: 0)).Should().Equal(NX_MTD.Application.Common.WarehouseMessages.LayPlyCountRange);
        Errors(Lay(2m, ratio: 0)).Should().Equal(NX_MTD.Application.Common.WarehouseMessages.LayRatioRange);
        Errors(Lay(2m, date: default(DateOnly))).Should().Equal(NX_MTD.Application.Common.WarehouseMessages.LayWorkDateRequired);
    }

    [Fact]
    public void Note_AndReason_AreMeasuredAfterTrimming()
    {
        Errors(Lay(2m, note: new string('a', 500) + "   ", reason: "  " + new string('b', 500))).Should().BeEmpty();
        Errors(Lay(2m, note: new string('a', 501))).Should().ContainSingle();
        Errors(Lay(2m, reason: new string('b', 501))).Should().ContainSingle();
    }

    [Theory]
    [InlineData("0.001", true)]
    [InlineData("12.345", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1.2345", false)]
    public void ReturnQty_Boundaries(string value, bool expected) =>
        new CreateMaterialReturnDtoValidator().Validate(new CreateMaterialReturnDto(1, null,
                [new CreateReturnLineDto(1, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))]))
            .IsValid.Should().Be(expected);

    [Theory]
    [InlineData("0", true)]
    [InlineData("12.345", true)]
    [InlineData("-0.001", false)]
    [InlineData("1.2345", false)]
    public void ConfirmedReturnQty_Boundaries(string value, bool expected) =>
        new ConfirmMaterialReturnDtoValidator().Validate(new ConfirmMaterialReturnDto(
                [new ConfirmReturnLineDto(1, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), "lệch")]))
            .IsValid.Should().Be(expected);
}
