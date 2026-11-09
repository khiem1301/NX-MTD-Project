using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Giá trị biên của phiếu xuất hàng: lập phiếu, xác nhận khách nhận, bộ lọc danh sách (UC45, UC46, UC47).</summary>
public class DispatchValidatorTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);
    private static readonly byte[] RowVer = { 1 };

    private static CreateDispatchNoteDto Create(int qty = 5, string address = "Kho khách", string? note = null, int orderId = 7,
        params int[] lineIds) =>
        new(orderId, Day, address, note,
            (lineIds.Length == 0 ? new[] { 101 } : lineIds).Select(id => new DispatchLineInputDto(id, qty)).ToList());

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1_000_001, false)]
    [InlineData(int.MaxValue, false)]
    public void Issue_qty_boundaries(int qty, bool valid) =>
        new CreateDispatchNoteDtoValidator().Validate(Create(qty)).IsValid.Should().Be(valid);

    [Fact]
    public void Create_checks_order_address_note_and_lines()
    {
        var validator = new CreateDispatchNoteDtoValidator();

        validator.Validate(Create()).IsValid.Should().BeTrue();
        validator.Validate(Create(orderId: 0)).IsValid.Should().BeFalse();
        validator.Validate(Create(address: "   ")).IsValid.Should().BeFalse();
        validator.Validate(Create(address: new string('a', 500))).IsValid.Should().BeTrue();
        validator.Validate(Create(address: "  " + new string('a', 500) + "  ")).IsValid.Should().BeTrue();   // tính sau khi cắt khoảng trắng
        validator.Validate(Create(address: new string('a', 501))).IsValid.Should().BeFalse();
        validator.Validate(Create(note: new string('b', 1000))).IsValid.Should().BeTrue();
        validator.Validate(Create(note: new string('b', 1001))).IsValid.Should().BeFalse();
        validator.Validate(Create(lineIds: [101, 101])).IsValid.Should().BeFalse();
        validator.Validate(Create() with { Lines = [] }).IsValid.Should().BeFalse();
        validator.Validate(Create() with { Lines = [null!] }).IsValid.Should().BeFalse();
        validator.Validate(Create() with { Lines = [new DispatchLineInputDto(0, 5)] }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Line_count_is_limited(int count, bool valid) =>
        new CreateDispatchNoteDtoValidator().Validate(Create(lineIds: Enumerable.Range(1, count).ToArray())).IsValid.Should().Be(valid);

    private static ConfirmDeliveryDto Confirm() => new(Day, RowVer);

    [Fact]
    public void Confirm_checks_date_and_row_version()
    {
        var validator = new ConfirmDeliveryDtoValidator();

        validator.Validate(Confirm()).IsValid.Should().BeTrue();
        validator.Validate(Confirm() with { CustomerReceivedDate = default }).IsValid.Should().BeFalse();
        validator.Validate(Confirm() with { RowVer = [] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Cancel_export_and_reject_need_a_row_version_and_reject_a_bounded_reason()
    {
        new DispatchActionDtoValidator().Validate(new DispatchActionDto(RowVer)).IsValid.Should().BeTrue();
        new DispatchActionDtoValidator().Validate(new DispatchActionDto([])).IsValid.Should().BeFalse();

        var reject = new RejectDispatchDtoValidator();
        reject.Validate(new RejectDispatchDto("không đủ hàng", RowVer)).IsValid.Should().BeTrue();
        reject.Validate(new RejectDispatchDto("   ", RowVer)).IsValid.Should().BeFalse();
        reject.Validate(new RejectDispatchDto(new string('a', 500), RowVer)).IsValid.Should().BeTrue();
        reject.Validate(new RejectDispatchDto(new string('a', 501), RowVer)).IsValid.Should().BeFalse();
        reject.Validate(new RejectDispatchDto("x", [])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void List_query_checks_status_order_keyword_and_paging()
    {
        var validator = new DispatchNoteQueryValidator();

        validator.Validate(new DispatchNoteQuery()).IsValid.Should().BeTrue();
        validator.Validate(new DispatchNoteQuery { Status = "IN_DELIVERY", OrderId = 3 }).IsValid.Should().BeTrue();
        validator.Validate(new DispatchNoteQuery { Status = "DONE" }).IsValid.Should().BeFalse();
        validator.Validate(new DispatchNoteQuery { OrderId = 0 }).IsValid.Should().BeFalse();
        validator.Validate(new DispatchNoteQuery { Keyword = new string('a', 101) }).IsValid.Should().BeFalse();
        validator.Validate(new DispatchNoteQuery { PageSize = 101 }).IsValid.Should().BeFalse();
    }
}
