using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Giá trị biên của đợt QC: thêm, sửa, xóa số hàng đạt (UC57, UC22).</summary>
public class QcValidatorTests
{
    private static readonly byte[] RowVer = { 1 };

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1_000_001, false)]
    [InlineData(int.MaxValue, false)]
    public void Quantity_boundaries_on_create_and_update(int qty, bool valid)
    {
        new CreateQcBatchDtoValidator().Validate(new CreateQcBatchDto(new DateOnly(2026, 11, 1), "MORNING", new TimeOnly(9, 0), 1, 1, qty))
            .IsValid.Should().Be(valid);
        new UpdateQcBatchDtoValidator().Validate(new UpdateQcBatchDto(qty, null, RowVer)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Reason_is_limited_after_trimming_and_row_version_is_required()
    {
        var update = new UpdateQcBatchDtoValidator();
        update.Validate(new UpdateQcBatchDto(5, new string('a', 500), RowVer)).IsValid.Should().BeTrue();
        update.Validate(new UpdateQcBatchDto(5, "  " + new string('a', 500) + "  ", RowVer)).IsValid.Should().BeTrue();
        update.Validate(new UpdateQcBatchDto(5, new string('a', 501), RowVer)).IsValid.Should().BeFalse();
        update.Validate(new UpdateQcBatchDto(5, null, [])).IsValid.Should().BeFalse();

        var delete = new DeleteQcBatchDtoValidator();
        delete.Validate(new DeleteQcBatchDto(new string('a', 500), RowVer)).IsValid.Should().BeTrue();
        delete.Validate(new DeleteQcBatchDto(new string('a', 501), RowVer)).IsValid.Should().BeFalse();
        delete.Validate(new DeleteQcBatchDto(null, [])).IsValid.Should().BeFalse();
    }
}
