using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Khách hàng và nhà cung cấp: giá trị biên của từng trường (độ dài tính sau khi cắt khoảng trắng đầu cuối).</summary>
public class PartnerValidatorTests
{
    private static string Pad(int length, char c = 'x') => new(c, length);
    private static string EmailOfLength(int length) => Pad(length - 6, 'a') + "@b.com";

    private static bool Customer(string name = "Khách A", string? phone = "0901234567", string? email = "a@b.vn", string? address = "12 Lê Lợi") =>
        new SaveCustomerDtoValidator().Validate(new SaveCustomerDto(name, phone, email, address)).IsValid;

    private static bool Supplier(string name = "NCC A", string? phone = "0901234567", string? email = "a@b.vn", string? address = "12 Lê Lợi",
        string? materialTypes = "Vải chính", string? note = null) =>
        new SaveSupplierDtoValidator().Validate(new SaveSupplierDto(name, phone, email, address, materialTypes, note)).IsValid;

    // ---------- số điện thoại ----------

    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("+84901234567", true)]
    [InlineData("090123456", false)]        // 9 số
    [InlineData("09012345678", false)]      // 11 số
    [InlineData("+8490123456", false)]
    [InlineData("+840901234567", false)]
    [InlineData("1901234567", false)]       // không bắt đầu bằng 0
    [InlineData("090 123 4567", false)]
    [InlineData(" 0901234567", false)]
    [InlineData("0901234567 ", false)]
    [InlineData("0901234567\n", false)]     // $ cũ cho phép xuống dòng ở cuối
    [InlineData("09012345ab", false)]
    [InlineData("0\u0669\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667", false)]   // chữ số Ả Rập – Ấn Độ: \d của .NET khớp, [0-9] thì không
    public void Phone_Boundaries(string phone, bool expected)
    {
        Customer(phone: phone).Should().Be(expected);
        Supplier(phone: phone).Should().Be(expected);
    }

    // ---------- email ----------

    [Theory]
    [InlineData("a@b.vn", true)]
    [InlineData("ten.ho+nhan@mail.example.com", true)]
    [InlineData("a_b-c@sub-domain.co", true)]
    [InlineData(" a@b.vn ", true)]               // service cắt khoảng trắng khi lưu
    [InlineData("a@b", false)]                   // tên miền không có dấu chấm
    [InlineData("a b@c.vn", false)]
    [InlineData("a@@b.vn", false)]
    [InlineData("@b.vn", false)]
    [InlineData("a@.vn", false)]
    [InlineData("a@b..vn", false)]
    [InlineData("a@-b.vn", false)]
    [InlineData("a@b.vn.", false)]
    [InlineData("nguyễn@b.vn", false)]           // cột VARCHAR sẽ đổi chữ có dấu thành "?"
    [InlineData("a@bé.vn", false)]
    [InlineData("a@b.vn\n", true)]               // đã cắt khoảng trắng
    public void Email_Format(string email, bool expected)
    {
        Customer(email: email).Should().Be(expected);
        Supplier(email: email).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Email_MaxLength(int length, bool expected)
    {
        Customer(email: EmailOfLength(length)).Should().Be(expected);
        Supplier(email: EmailOfLength(length)).Should().Be(expected);
    }

    [Fact]
    public void Email_MaxLength_IsCountedAfterTrim()
    {
        Customer(email: " " + EmailOfLength(100) + " ").Should().BeTrue();
        Supplier(email: " " + EmailOfLength(100) + " ").Should().BeTrue();
    }

    // ---------- tên, địa chỉ, loại NVL, ghi chú ----------

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Name_MaxLength200(int length, bool expected)
    {
        Customer(name: Pad(length)).Should().Be(expected);
        Supplier(name: Pad(length)).Should().Be(expected);
    }

    [Fact]
    public void Name_200CharsWithSurroundingSpaces_Passes_BecauseServiceStoresTheTrimmedValue()
    {
        Customer(name: " " + Pad(200) + " ").Should().BeTrue();
        Supplier(name: " " + Pad(200) + " ").Should().BeTrue();
        Customer(name: " " + Pad(201) + " ").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_Required(string name)
    {
        Customer(name: name).Should().BeFalse();
        Supplier(name: name).Should().BeFalse();
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Address_MaxLength500(int length, bool expected)
    {
        Customer(address: Pad(length)).Should().Be(expected);
        Supplier(address: Pad(length)).Should().Be(expected);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void SupplierMaterialTypes_MaxLength200(int length, bool expected) =>
        Supplier(materialTypes: Pad(length)).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void SupplierNote_OptionalAndMaxLength500(int length, bool expected) =>
        Supplier(note: Pad(length)).Should().Be(expected);

    [Fact]
    public void SupplierNote_CanBeNullOrBlank()
    {
        Supplier(note: null).Should().BeTrue();
        Supplier(note: "   ").Should().BeTrue();
    }

    [Fact]
    public void TooLongFields_ReportVietnameseMessages()
    {
        var customer = new SaveCustomerDtoValidator().Validate(new SaveCustomerDto(Pad(201), "0901234567", EmailOfLength(101), Pad(501)));
        var supplier = new SaveSupplierDtoValidator().Validate(new SaveSupplierDto("NCC", "0901234567", "a@b.vn", "d", Pad(201), Pad(501)));

        customer.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo(
            "Tên khách hàng tối đa 200 ký tự.", "Email tối đa 100 ký tự.", "Địa chỉ tối đa 500 ký tự.");
        supplier.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo(
            "Loại NVL cung cấp tối đa 200 ký tự.", "Ghi chú tối đa 500 ký tự.");
    }

    // ---------- lý do ngừng hợp tác ----------

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    [InlineData(0, false)]
    public void DiscontinueReason_Boundaries(int length, bool expected)
    {
        var reason = Pad(length);

        new DiscontinueCustomerDtoValidator().Validate(new DiscontinueCustomerDto(reason)).IsValid.Should().Be(expected);
        new DiscontinueSupplierDtoValidator().Validate(new DiscontinueSupplierDto(reason)).IsValid.Should().Be(expected);
    }

    [Fact]
    public void DiscontinueReason_BlankIsRejected_AndLengthIsCountedAfterTrim()
    {
        new DiscontinueCustomerDtoValidator().Validate(new DiscontinueCustomerDto("   ")).IsValid.Should().BeFalse();
        new DiscontinueCustomerDtoValidator().Validate(new DiscontinueCustomerDto(" " + Pad(500) + " ")).IsValid.Should().BeTrue();
        new DiscontinueSupplierDtoValidator().Validate(new DiscontinueSupplierDto(" " + Pad(500) + " ")).IsValid.Should().BeTrue();
    }

    // ---------- tìm kiếm ----------

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void CustomerQuery_KeywordMaxLength100(int length, bool expected) =>
        new CustomerQueryValidator().Validate(new CustomerQuery { Keyword = Pad(length) }).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void CustomerQuery_PageBoundaries(int page, int pageSize, bool expected) =>
        new CustomerQueryValidator().Validate(new CustomerQuery { Page = page, PageSize = pageSize }).IsValid.Should().Be(expected);
}
