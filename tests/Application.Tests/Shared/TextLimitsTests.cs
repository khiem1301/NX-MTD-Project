using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>Giá trị biên của lý do (tối đa 500 ký tự) và mật khẩu mới (tối đa 50 ký tự), DEV-ALL-72.</summary>
public class TextLimitsTests
{
    private static string Text(int length) => new('x', length);

    [Fact]
    public void Limits_MatchAgreedValues()
    {
        TextLimits.Reason.Should().Be(500);
        TextLimits.Password.Should().Be(50);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(49, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void NewPassword_MaxLength50(int length, bool expected) =>
        new ChangePasswordDtoValidator().Validate(new ChangePasswordDto(null, Text(length), Text(length))).IsValid.Should().Be(expected);

    [Fact]
    public void NewPassword_Empty_IsInvalid() =>
        new ChangePasswordDtoValidator().Validate(new ChangePasswordDto(null, "", "")).IsValid.Should().BeFalse();

    [Fact]
    public void NewPassword_TooLong_ReportsLimitInMessage() =>
        new ChangePasswordDtoValidator().Validate(new ChangePasswordDto(null, Text(51), Text(51))).Errors
            .Should().Contain(e => e.PropertyName == "NewPassword" && e.ErrorMessage.Contains("50"));

    [Fact]
    public void ConfirmPassword_Mismatch_IsInvalid() =>
        new ChangePasswordDtoValidator().Validate(new ChangePasswordDto(null, "abc", "abd")).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(1, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_RejectPayroll_MaxLength500(int length, bool expected) =>
        new RejectPayrollDtoValidator().Validate(new RejectPayrollDto(Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_ReopenPayroll_MaxLength500(int length, bool expected) =>
        new ReopenPayrollDtoValidator().Validate(new ReopenPayrollDto(Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_DiscontinueCustomer_MaxLength500(int length, bool expected) =>
        new DiscontinueCustomerDtoValidator().Validate(new DiscontinueCustomerDto(Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_DiscontinueSupplier_MaxLength500(int length, bool expected) =>
        new DiscontinueSupplierDtoValidator().Validate(new DiscontinueSupplierDto(Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Reason_LockAccount_MaxLength300(int length, bool expected) =>
        new LockAccountDtoValidator().Validate(new LockAccountDto(Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Reason_ProposeGrade_MaxLength500(int length, bool expected) =>
        new ProposeGradeDtoValidator().Validate(new ProposeGradeDto(1, "B", Text(length))).IsValid.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reason_RejectPayroll_EmptyOrBlank_IsInvalid(string reason) =>
        new RejectPayrollDtoValidator().Validate(new RejectPayrollDto(reason)).IsValid.Should().BeFalse();
}
