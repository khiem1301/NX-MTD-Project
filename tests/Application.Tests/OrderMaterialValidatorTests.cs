using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>NVL của đơn: giá trị biên của số cần, số khách cấp, số lần giao dự kiến và ghi chú.</summary>
public class OrderMaterialValidatorTests
{
    private static CreateOrderMaterialDto Dto(decimal required = 100, decimal customer = 0, short? count = null, string? note = null,
        int orderId = 1, int materialId = 1) => new(orderId, materialId, required, customer, count, note);

    private static bool Valid(CreateOrderMaterialDto dto) => new CreateOrderMaterialDtoValidator().Validate(dto).IsValid;

    private static string[] Errors(CreateOrderMaterialDto dto) =>
        new CreateOrderMaterialDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage).ToArray();

    // ---------- số cần ----------

    [Theory]
    [InlineData("0.001", true)]
    [InlineData("1", true)]
    [InlineData("99999999.999", true)]
    [InlineData("100000000", true)]
    [InlineData("100000000.001", false)]
    [InlineData("100000001", false)]
    [InlineData("1000000000000000", false)]       // DECIMAL(18,3) tràn số: trước đây là lỗi hệ thống
    [InlineData("-0.001", false)]
    [InlineData("-1", false)]
    public void RequiredQty_Boundaries(string value, bool expected) =>
        Valid(Dto(required: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))).Should().Be(expected);

    [Theory]
    [InlineData("1.234", true)]
    [InlineData("1.2345", false)]        // trước đây bị làm tròn âm thầm thành 1,235
    [InlineData("0.0004", false)]        // trước đây lưu thành 0
    [InlineData("0.0001", false)]
    public void RequiredQty_AtMostThreeDecimals(string value, bool expected) =>
        Valid(Dto(required: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))).Should().Be(expected);

    [Fact]
    public void RequiredQty_ErrorMessages_AreVietnameseAndOnePerField()
    {
        Errors(Dto(required: -1)).Should().Equal("Số cần không được âm.");
        Errors(Dto(required: 100_000_001)).Should().Equal("Số cần tối đa 100.000.000.");
        Errors(Dto(required: 1.2345m)).Should().Equal("Số cần tối đa 3 số lẻ.");
    }

    // ---------- số khách cấp ----------

    [Theory]
    [InlineData("0.001", true)]
    [InlineData("100000000", true)]
    [InlineData("100000000.001", false)]
    [InlineData("1000000000000000", false)]
    [InlineData("1.2345", false)]
    [InlineData("-1", false)]
    public void CustomerQty_Boundaries(string value, bool expected) =>
        Valid(Dto(customer: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), count: 1)).Should().Be(expected);

    [Fact]
    public void CustomerQty_ErrorMessages_AreVietnamese()
    {
        Errors(Dto(customer: -1, count: 1)).Should().Equal("Số khách cấp không được âm.");
        Errors(Dto(customer: 100_000_001, count: 1)).Should().Equal("Số khách cấp tối đa 100.000.000.");
        Errors(Dto(customer: 1.2345m, count: 1)).Should().Equal("Số khách cấp tối đa 3 số lẻ.");
    }

    // ---------- cả hai số ----------

    [Fact]
    public void BothQuantitiesZero_IsRejected_WithOneMessage()
    {
        Errors(Dto(required: 0, customer: 0)).Should().Equal("Phải nhập số cần hoặc số khách cấp lớn hơn 0.");
    }

    [Fact]
    public void RequiredZero_IsFine_WhenTheCustomerSupplies() => Valid(Dto(required: 0, customer: 50, count: 1)).Should().BeTrue();

    [Fact]
    public void CustomerSupplyingMoreThanRequired_IsFine() => Valid(Dto(required: 10, customer: 50, count: 1)).Should().BeTrue();

    // ---------- số lần giao dự kiến ----------

    [Theory]
    [InlineData((short)1, true)]
    [InlineData((short)99, true)]
    [InlineData((short)100, false)]       // trước đây nhận tới 32767 trong khi giao diện chặn ở 99
    [InlineData((short)32767, false)]
    [InlineData((short)0, false)]
    [InlineData((short)-1, false)]
    public void DeliveryCount_Boundaries_WhenTheCustomerSupplies(short count, bool expected) =>
        Valid(Dto(customer: 50, count: count)).Should().Be(expected);

    [Fact]
    public void DeliveryCount_IsRequired_WhenTheCustomerSupplies_WithVietnameseMessage() =>
        Errors(Dto(customer: 50, count: null)).Should().Equal("Khách có cấp NVL thì phải nhập số lần dự kiến giao (từ 1 trở lên).");

    [Fact]
    public void DeliveryCount_OutOfRange_HasOneVietnameseMessage() =>
        Errors(Dto(customer: 50, count: 100)).Should().Equal("Số lần giao dự kiến phải từ 1 đến 99.");

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)99)]
    public void DeliveryCount_MustBeEmpty_WhenTheCustomerDoesNotSupply(short count) =>
        Errors(Dto(customer: 0, count: count)).Should().Equal("Khách không cấp NVL thì không nhập số lần giao.");

    // ---------- ghi chú, mã ----------

    [Theory]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Note_MaxLength500(int length, bool expected) => Valid(Dto(note: new string('n', length))).Should().Be(expected);

    [Fact]
    public void Note_IsCountedAfterTrim_AndBlankIsFine()
    {
        Valid(Dto(note: " " + new string('n', 500) + " ")).Should().BeTrue();
        Valid(Dto(note: " " + new string('n', 501) + " ")).Should().BeFalse();
        Valid(Dto(note: "   ")).Should().BeTrue();
        Errors(Dto(note: new string('n', 501))).Should().Equal("Ghi chú tối đa 500 ký tự.");
    }

    [Fact]
    public void Ids_MustBePositive_WithVietnameseMessages()
    {
        Errors(Dto(materialId: 0)).Should().Equal("Vui lòng chọn NVL.");
        Errors(Dto(orderId: 0)).Should().Equal("Vui lòng chọn đơn hàng.");
    }

    [Fact]
    public void UpdateValidator_AppliesTheSameRules()
    {
        var validator = new UpdateOrderMaterialDtoValidator();

        validator.Validate(new UpdateOrderMaterialDto(1, 100_000_000, 0, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new UpdateOrderMaterialDto(1, 100_000_001, 0, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new UpdateOrderMaterialDto(1, 1, 5, 100, null)).IsValid.Should().BeFalse();
    }

    // ---------- bộ lọc danh sách ----------

    [Fact]
    public void PageQueries_HaveVietnameseMessages()
    {
        var paged = new OrderMaterialPageQueryValidator().Validate(new OrderMaterialPageQuery { OrderId = 0, Page = 0, PageSize = 101, Supply = "x", ReceiptState = "y" });
        var pending = new PendingMaterialOrderQueryValidator().Validate(new PendingMaterialOrderQuery { MaterialState = "z" });

        paged.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo("Vui lòng chọn đơn hàng.", "Số trang phải từ 1 trở lên.",
            "Số dòng mỗi trang phải từ 1 đến 100.", "Nguồn cấp không hợp lệ.", "Tình trạng nhận không hợp lệ.");
        pending.Errors.Select(e => e.ErrorMessage).Should().Equal("Tình trạng NVL không hợp lệ.");
    }
}
