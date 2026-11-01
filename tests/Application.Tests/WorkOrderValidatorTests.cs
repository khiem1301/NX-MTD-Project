using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>DEV-ALL-35: giá trị biên của lệnh cắt (ghi chú, hạn, lý do cắt thiếu, bộ lọc danh sách).</summary>
public class WorkOrderValidatorTests
{
    private static readonly DateOnly Due = new(2026, 11, 20);
    private static readonly SaveWorkOrderLineDto Line = new(1, 5);

    private static SaveWorkOrderDto Save(string? note = null, DateOnly? due = null) =>
        new(1, 1, 1, due ?? Due, note, new[] { Line });

    [Theory]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Note_is_limited_so_that_the_cut_short_reason_still_fits_the_column(int length, bool valid) =>
        new SaveWorkOrderDtoValidator().Validate(Save(new string('a', length))).IsValid.Should().Be(valid);

    [Fact]
    public void Note_length_is_counted_after_trimming_and_a_blank_note_is_fine()
    {
        var validator = new SaveWorkOrderDtoValidator();

        validator.Validate(Save("  " + new string('a', 600) + "  ")).IsValid.Should().BeTrue();
        validator.Validate(Save("   ")).IsValid.Should().BeTrue();
        validator.Validate(Save(null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void The_longest_note_plus_the_longest_reason_fits_the_database_column() =>
        (WorkOrderLimits.MaxNote + "\nCắt thiếu: ".Length + WorkOrderLimits.MaxFinishReason).Should().BeLessThanOrEqualTo(1000);

    [Fact]
    public void Due_date_ids_and_line_count_are_checked()
    {
        var validator = new SaveWorkOrderDtoValidator();
        var many = Enumerable.Range(1, 1001).Select(i => new SaveWorkOrderLineDto(i, 1)).ToList();

        validator.Validate(Save(due: default(DateOnly))).IsValid.Should().BeFalse();
        validator.Validate(Save() with { OrderId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { ProductId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { CuttingTeamId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Save() with { Lines = many.Take(1000).ToList() }).IsValid.Should().BeTrue();
        validator.Validate(Save() with { Lines = many }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Finish_reason_is_limited(int length, bool valid) =>
        new FinishCuttingDtoValidator().Validate(new FinishCuttingDto(new string('a', length))).IsValid.Should().Be(valid);

    [Fact]
    public void Finish_reason_may_be_empty_when_the_plan_was_met() =>
        new FinishCuttingDtoValidator().Validate(new FinishCuttingDto(null)).IsValid.Should().BeTrue();

    [Fact]
    public void Replacement_work_order_needs_request_team_date_and_a_bounded_note()
    {
        var validator = new CreateReplacementWorkOrderDtoValidator();
        var ok = new CreateReplacementWorkOrderDto(1, 1, Due, null);

        validator.Validate(ok).IsValid.Should().BeTrue();
        validator.Validate(ok with { RequestId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { CuttingTeamId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(ok with { DueDate = default }).IsValid.Should().BeFalse();
        validator.Validate(ok with { Note = new string('a', 601) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void List_query_rejects_unknown_status_bad_order_and_bad_paging()
    {
        var validator = new WorkOrderQueryValidator();

        validator.Validate(new WorkOrderQuery()).IsValid.Should().BeTrue();
        validator.Validate(new WorkOrderQuery { Status = WorkOrderStatuses.Cutting, OrderId = 3 }).IsValid.Should().BeTrue();
        validator.Validate(new WorkOrderQuery { Status = "X" }).IsValid.Should().BeFalse();
        validator.Validate(new WorkOrderQuery { OrderId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(new WorkOrderQuery { PageSize = 101 }).IsValid.Should().BeFalse();
        validator.Validate(new WorkOrderQuery { Keyword = new string('a', 101) }).IsValid.Should().BeFalse();
    }
}
