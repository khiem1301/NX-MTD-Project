using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>DEV-ALL-90, DEV-ALL-93: số mét từng vải đã dùng và số lượng sau cắt của bàn cắt; giá trị biên của bàn cắt và phiếu trả vải thừa.</summary>
public class CuttingLayValidatorTests
{
    private static SaveCuttingLayDto Lay(decimal? usedM, int qty = 10, string shift = "MORNING",
        DateOnly? date = null, string? note = null) =>
        new(1, date ?? new DateOnly(2026, 11, 1), shift, [new SaveLayFabricDto(1, usedM)], note, [new SaveLayLineDto(1, null, qty)]);

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
    [InlineData("10000", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("10000.01", false)]
    [InlineData("1.234", false)]
    public void FabricUsed_Boundaries(string value, bool expected) =>
        new SaveCuttingLayDtoValidator().Validate(Lay(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)))
            .IsValid.Should().Be(expected);

    [Fact]
    public void FabricUsed_IsRequired() =>
        Errors(Lay(null)).Should().Equal(WarehouseMessages.LayFabricUsedRequired);

    [Theory]
    [InlineData(1, true)]
    [InlineData(100000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100001, false)]
    public void LineQty_Boundaries(int qty, bool expected) => Errors(Lay(2m, qty: qty)).Any().Should().Be(!expected);

    [Fact]
    public void NoLines_AskForAtLeastOneQty() =>
        Errors(new SaveCuttingLayDto(1, new DateOnly(2026, 11, 1), "MORNING", [new SaveLayFabricDto(1, 2m)], null, []))
            .Should().Equal(WarehouseMessages.LayQtyRequired);

    [Fact]
    public void DuplicateLines_AreRefused() =>
        Errors(new SaveCuttingLayDto(1, new DateOnly(2026, 11, 1), "MORNING", [new SaveLayFabricDto(1, 2m)], null,
                [new SaveLayLineDto(1, null, 5), new SaveLayLineDto(1, null, 6)]))
            .Should().Equal(WarehouseMessages.DuplicateLine);

    [Fact]
    public void Fabrics_RequiredAndNotDuplicated()
    {
        Errors(new SaveCuttingLayDto(1, new DateOnly(2026, 11, 1), "MORNING", [], null, [new SaveLayLineDto(1, null, 5)]))
            .Should().Equal(WarehouseMessages.FabricRequired);
        Errors(new SaveCuttingLayDto(1, new DateOnly(2026, 11, 1), "MORNING",
                [new SaveLayFabricDto(1, 2m), new SaveLayFabricDto(1, 3m)], null, [new SaveLayLineDto(1, null, 5)]))
            .Should().Equal(WarehouseMessages.DuplicateLine);
    }

    [Fact]
    public void Shift_Qty_FabricUsed_AndMissingDate_ReportInVietnamese()
    {
        Errors(Lay(2m, shift: "NIGHT")).Should().Equal(WarehouseMessages.LayShiftInvalid);
        Errors(Lay(2m, qty: 0)).Should().Equal(WarehouseMessages.LayQtyRange);
        Errors(Lay(0m)).Should().Equal(WarehouseMessages.LayFabricUsedInvalid);
        Errors(Lay(10000.01m)).Should().Equal(WarehouseMessages.LayFabricUsedMax);
        Errors(Lay(1.234m)).Should().Equal(WarehouseMessages.LayFabricUsedDecimals);
        Errors(Lay(2m, date: default(DateOnly))).Should().Equal(WarehouseMessages.LayWorkDateRequired);
    }

    [Fact]
    public void Note_IsMeasuredAfterTrimming()
    {
        Errors(Lay(2m, note: new string('a', 500) + "   ")).Should().BeEmpty();
        Errors(Lay(2m, note: new string('a', 501))).Should().ContainSingle();
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
