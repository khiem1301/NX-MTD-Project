using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>DEV-ALL-37: giá trị biên của phiếu hàng hỏng và phần chấp nhận phiếu của Quản đốc (UC56, UC22).</summary>
public class DefectValidatorTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);
    private static readonly byte[] RowVer = { 1 };

    private static CreateDefectDto Create(int qty = 3, string type = "Scrap", string reason = "Torn", string? note = null,
        string? shift = "MORNING", DateOnly? day = null) =>
        new(day ?? Day, shift, 10, 1, 7, type, qty, reason, note);

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1_000_001, false)]
    [InlineData(int.MaxValue, false)]
    public void Quantity_boundaries(int qty, bool valid) =>
        new CreateDefectDtoValidator().Validate(Create(qty)).IsValid.Should().Be(valid);

    [Fact]
    public void Create_checks_type_reason_shift_note_and_date()
    {
        var validator = new CreateDefectDtoValidator();

        validator.Validate(Create()).IsValid.Should().BeTrue();
        validator.Validate(Create(type: "rework", reason: "skipped_stitch", shift: null)).IsValid.Should().BeTrue();
        validator.Validate(Create(type: "Other")).IsValid.Should().BeFalse();
        validator.Validate(Create(reason: "Burnt")).IsValid.Should().BeFalse();
        validator.Validate(Create(shift: "NIGHT")).IsValid.Should().BeFalse();
        validator.Validate(Create(day: default(DateOnly))).IsValid.Should().BeFalse();
        validator.Validate(Create() with { OrderLineId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Create() with { OperationId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Create() with { ResponsibleWorkerId = 0 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_other_reason_needs_a_note_and_any_note_is_limited_to_the_column()
    {
        var validator = new CreateDefectDtoValidator();

        validator.Validate(Create(reason: "Other", note: null)).IsValid.Should().BeFalse();
        validator.Validate(Create(reason: "Other", note: "   ")).IsValid.Should().BeFalse();
        validator.Validate(Create(reason: "Other", note: "Vải loang")).IsValid.Should().BeTrue();
        validator.Validate(Create(note: new string('a', 500))).IsValid.Should().BeTrue();
        validator.Validate(Create(note: new string('a', 501))).IsValid.Should().BeFalse();
        validator.Validate(Create(note: "  " + new string('a', 500) + "  ")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Cancel_needs_a_reason_and_a_row_version()
    {
        new CancelDefectDtoValidator().Validate(new CancelDefectDto("Ghi nhầm", RowVer)).IsValid.Should().BeTrue();
        new CancelDefectDtoValidator().Validate(new CancelDefectDto(new string('a', 500), RowVer)).IsValid.Should().BeTrue();
        new CancelDefectDtoValidator().Validate(new CancelDefectDto(new string('a', 501), RowVer)).IsValid.Should().BeFalse();
        new CancelDefectDtoValidator().Validate(new CancelDefectDto(null, RowVer)).IsValid.Should().BeFalse();
        new CancelDefectDtoValidator().Validate(new CancelDefectDto("   ", RowVer)).IsValid.Should().BeFalse();
        new CancelDefectDtoValidator().Validate(new CancelDefectDto("Ghi nhầm", [])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_checks_the_same_fields_as_create_and_needs_a_row_version()
    {
        var validator = new UpdateDefectDtoValidator();
        UpdateDefectDto Update(int qty = 2, string reason = "Torn", string? note = null, byte[]? rowVer = null) =>
            new("MORNING", 1, 7, "Scrap", qty, reason, note, null, rowVer ?? RowVer);

        validator.Validate(Update()).IsValid.Should().BeTrue();
        validator.Validate(Update(qty: 0)).IsValid.Should().BeFalse();
        validator.Validate(Update(reason: "X")).IsValid.Should().BeFalse();
        validator.Validate(Update(reason: "Other")).IsValid.Should().BeFalse();
        validator.Validate(Update(reason: "Other", note: "Vải loang")).IsValid.Should().BeTrue();
        validator.Validate(Update(rowVer: [])).IsValid.Should().BeFalse();
        validator.Validate(Update() with { DefectType = "X" }).IsValid.Should().BeFalse();
        validator.Validate(Update() with { Reason = new string('a', 501) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void List_query_checks_filters_team_and_date_range()
    {
        var validator = new DefectQueryValidator();

        validator.Validate(new DefectQuery()).IsValid.Should().BeTrue();
        validator.Validate(new DefectQuery
        {
            Status = "Confirmed", DefectType = "Scrap", SewingTeamId = 2,
        }).IsValid.Should().BeTrue();
        validator.Validate(new DefectQuery { Status = "X" }).IsValid.Should().BeFalse();
        validator.Validate(new DefectQuery { DefectType = "X" }).IsValid.Should().BeFalse();
        validator.Validate(new DefectQuery { SewingTeamId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(new DefectQuery { PageSize = 101 }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(366, true)]
    [InlineData(367, false)]
    public void Date_range_is_limited_to_a_year(int days, bool valid) =>
        new DefectQueryValidator().Validate(new DefectQuery { From = Day, To = Day.AddDays(days) }).IsValid.Should().Be(valid);

    [Fact]
    public void Date_range_must_run_forward()
    {
        var validator = new DefectQueryValidator();

        validator.Validate(new DefectQuery { From = Day, To = Day.AddDays(-1) }).IsValid.Should().BeFalse();
        validator.Validate(new DefectQuery { From = Day }).IsValid.Should().BeTrue();
        validator.Validate(new DefectQuery { To = Day }).IsValid.Should().BeTrue();
    }
}
