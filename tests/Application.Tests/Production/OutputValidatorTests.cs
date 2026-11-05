using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>DEV-ALL-36: giá trị biên của lưu sản lượng (màn Sửa sản lượng sau hạn, UC22).</summary>
public class OutputValidatorTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);

    private static SaveOutputDto Save(int quantity = 5, string? reason = null, int? teamId = 3, DateOnly? day = null) =>
        new(day ?? Day, "AFTERNOON", 10, teamId, reason, new[] { new OutputLineInput(1, 2, quantity) });

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(1_000_001, false)]
    [InlineData(-1, false)]
    [InlineData(int.MaxValue, false)]
    public void Quantity_boundaries(int quantity, bool valid) =>
        new SaveOutputDtoValidator().Validate(Save(quantity)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_is_limited_to_the_audit_column(int length, bool valid) =>
        new SaveOutputDtoValidator().Validate(Save(reason: new string('a', length))).IsValid.Should().Be(valid);

    [Fact]
    public void Reason_is_counted_after_trimming_and_may_be_empty()
    {
        var validator = new SaveOutputDtoValidator();

        validator.Validate(Save(reason: "  " + new string('a', 500) + "  ")).IsValid.Should().BeTrue();
        validator.Validate(Save(reason: "   ")).IsValid.Should().BeTrue();
        validator.Validate(Save(reason: null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Ids_date_and_row_count_are_checked()
    {
        var validator = new SaveOutputDtoValidator();
        var rows = Enumerable.Range(1, 2001).Select(i => new OutputLineInput(i, i, 1)).ToList();

        validator.Validate(Save(day: default(DateOnly))).IsValid.Should().BeFalse();
        validator.Validate(Save(teamId: 0)).IsValid.Should().BeFalse();
        validator.Validate(Save(teamId: null)).IsValid.Should().BeTrue();
        validator.Validate(Save() with { OrderLineId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { ShiftCode = "" }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { Rows = new[] { new OutputLineInput(0, 2, 1) } }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { Rows = new[] { new OutputLineInput(1, 0, 1) } }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { Rows = rows.Take(2000).ToList() }).IsValid.Should().BeTrue();
        validator.Validate(Save() with { Rows = rows }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { Rows = Array.Empty<OutputLineInput>() }).IsValid.Should().BeFalse();
    }
}
