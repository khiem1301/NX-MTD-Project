using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>
/// NVL của đơn: giá trị biên của số cần, số khách cấp và ghi chú. Lịch khách giao (số lần, ngày) khai ở cả đơn hàng (DEV-ALL-95).
/// </summary>
public class OrderMaterialValidatorTests
{
    private static readonly DateOnly Soon = new(2026, 12, 1);

    private static CreateOrderMaterialDto Dto(decimal required = 100, decimal customer = 0, string? note = null,
        int orderId = 1, int materialId = 1) => new(orderId, materialId, required, customer, note);

    private static bool Valid(CreateOrderMaterialDto dto) => new CreateOrderMaterialDtoValidator().Validate(dto).IsValid;

    private static string[] Errors(CreateOrderMaterialDto dto) =>
        new CreateOrderMaterialDtoValidator().Validate(dto).Errors.Select(e => e.ErrorMessage).ToArray();

    // ---------- thêm nhiều NVL cùng lúc ----------

    private static CreateOrderMaterialLineDto Line(int materialId, decimal required, decimal customer, string? note = null) =>
        new(materialId, required, customer, note);

    private static string[] BatchErrors(params CreateOrderMaterialLineDto[] lines) =>
        new CreateOrderMaterialBatchDtoValidator().Validate(new CreateOrderMaterialBatchDto(1, lines))
            .Errors.Select(e => e.ErrorMessage).ToArray();

    [Fact]
    public void Batch_ValidLines_NoError() =>
        BatchErrors(Line(1, 10, 0), Line(2, 0, 5)).Should().BeEmpty();

    [Fact]
    public void Batch_NoLines_Rejected() =>
        BatchErrors().Should().Equal("Phải có ít nhất một NVL.");

    [Fact]
    public void Batch_SameMaterialTwice_Rejected() =>
        BatchErrors(Line(1, 10, 0), Line(1, 5, 0)).Should().Equal("Một dòng bị khai báo nhiều lần.");

    [Fact]
    public void Batch_LineBreaksRules_UsesSameRulesAsSingleLine() =>
        BatchErrors(Line(1, -1, 0)).Should().Equal("Số cần không được âm.");

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
        Valid(Dto(customer: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))).Should().Be(expected);

    [Fact]
    public void CustomerQty_ErrorMessages_AreVietnamese()
    {
        Errors(Dto(customer: -1)).Should().Equal("Số khách cấp không được âm.");
        Errors(Dto(customer: 100_000_001)).Should().Equal("Số khách cấp tối đa 100.000.000.");
        Errors(Dto(customer: 1.2345m)).Should().Equal("Số khách cấp tối đa 3 số lẻ.");
    }

    // ---------- cả hai số ----------

    [Fact]
    public void BothQuantitiesZero_IsRejected_WithOneMessage()
    {
        Errors(Dto(required: 0, customer: 0)).Should().Equal("Phải nhập số cần hoặc số khách cấp lớn hơn 0.");
    }

    [Fact]
    public void RequiredZero_IsFine_WhenTheCustomerSupplies() => Valid(Dto(required: 0, customer: 50)).Should().BeTrue();

    [Fact]
    public void CustomerSupplyingMoreThanRequired_IsFine() => Valid(Dto(required: 10, customer: 50)).Should().BeTrue();

    // ---------- lịch khách giao của cả đơn (DEV-ALL-95) ----------

    private static string[] PlanErrors(short count, DateOnly date, DateOnly? dateTo = null) =>
        new SaveOrderCustomerDeliveryDtoValidator().Validate(new SaveOrderCustomerDeliveryDto(count, date, dateTo))
            .Errors.Select(e => e.ErrorMessage).ToArray();

    [Theory]
    [InlineData((short)1, true)]
    [InlineData((short)99, true)]
    [InlineData((short)100, false)]
    [InlineData((short)0, false)]
    public void Validate_CustomerDeliveryCount_Boundaries(short count, bool expected) =>
        (PlanErrors(count, Soon, count >= 2 ? Soon.AddDays(9) : null).Length == 0).Should().Be(expected);

    [Fact]
    public void Validate_CustomerDeliveryCountOutOfRange_HasOneVietnameseMessage() =>
        PlanErrors(100, Soon, Soon.AddDays(9)).Should().Equal("Số lần khách giao NVL phải từ 1 đến 99.");

    [Fact]
    public void Validate_CustomerDeliversTwiceWithoutDateTo_ReturnsDateToRequired() =>
        PlanErrors(2, Soon).Should().Equal("Giao từ 2 lần trở lên thì nhập khoảng ngày giao (từ ngày – đến ngày).");

    [Fact]
    public void Validate_DateToBeforeDateFrom_ReturnsBeforeFromError() =>
        PlanErrors(2, Soon, Soon.AddDays(-1)).Should().Equal("Ngày giao đến không được trước ngày giao từ.");

    [Fact]
    public void Validate_CustomerDeliversOnceWithDateTo_IgnoresDateTo() =>
        PlanErrors(1, Soon, Soon.AddDays(-1)).Should().BeEmpty();

    [Fact]
    public void Validate_CustomerDeliveryWithoutDate_ReturnsDateRequired() =>
        PlanErrors(1, default).Should().Equal("Vui lòng nhập ngày dự kiến giao hàng.");

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

        validator.Validate(new UpdateOrderMaterialDto(1, 100_000_000, 0, null)).IsValid.Should().BeTrue();
        validator.Validate(new UpdateOrderMaterialDto(1, 100_000_001, 0, null)).IsValid.Should().BeFalse();
        validator.Validate(new UpdateOrderMaterialDto(1, 1, -5, null)).IsValid.Should().BeFalse();
    }

    // ---------- bộ lọc danh sách ----------

    [Fact]
    public void PageQueries_HaveVietnameseMessages()
    {
        var paged = new OrderMaterialPageQueryValidator().Validate(new OrderMaterialPageQuery { OrderId = 0, Page = 0, PageSize = 101, Supply = "x", ReceiptState = "y" });

        paged.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo("Vui lòng chọn đơn hàng.", "Số trang phải từ 1 trở lên.",
            "Số dòng mỗi trang phải từ 1 đến 100.", "Nguồn cấp không hợp lệ.", "Tình trạng nhận không hợp lệ.");
    }
}
