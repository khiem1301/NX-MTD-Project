using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Họ tên nhân viên chỉ gồm chữ cái và khoảng trắng: không số, không ký tự đặc biệt (DEV-ALL-74).</summary>
public class PersonNameValidatorTests
{
    private static CreateEmployeeDto Create(string name) =>
        new("NV001", name, null, null, null, "WORKER", null, "PieceRate", new DateOnly(2026, 1, 1), null);

    private static AdminUpdateEmployeeDto AdminUpdate(string name) =>
        new(name, null, null, null, "WORKER", null, "PieceRate", new DateOnly(2026, 1, 1), null, [1]);

    [Theory]
    [InlineData("Nguyễn Văn A")]
    [InlineData("Trần")]
    [InlineData("Lê Thị Mai")]
    [InlineData("  Phạm Văn B  ")]
    [InlineData("Nguyễn Văn Đức Anh")]
    public void ValidNames_AreAccepted(string name)
    {
        PersonNameRule.IsValid(name).Should().BeTrue();
        new CreateEmployeeDtoValidator().Validate(Create(name)).Errors.Should().NotContain(e => e.PropertyName == "FullName");
    }

    [Theory]
    [InlineData("Nguyễn Văn A1")]
    [InlineData("123")]
    [InlineData("An@")]
    [InlineData("An-Bình")]
    [InlineData("O'Connor")]
    [InlineData("Nguyễn.Văn")]
    [InlineData("Nguyễn  Văn")]
    [InlineData("Nguyễn\tVăn")]
    [InlineData("Nguyễn\nVăn")]
    [InlineData("An/Bình")]
    [InlineData("Công nhân 1")]
    [InlineData("😀")]
    public void NamesWithDigitsOrSpecialCharacters_AreRejected(string name)
    {
        PersonNameRule.IsValid(name).Should().BeFalse();
        var errors = new CreateEmployeeDtoValidator().Validate(Create(name)).Errors;
        errors.Should().Contain(e => e.PropertyName == "FullName" && e.ErrorMessage == PersonNameRule.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_ReportsOnlyTheRequiredMessage(string name)
    {
        var errors = new CreateEmployeeDtoValidator().Validate(Create(name)).Errors.Where(e => e.PropertyName == "FullName").ToList();
        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(Messages.Required("họ tên"));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Name_MaxLength100(int length, bool expected) =>
        new CreateEmployeeDtoValidator().Validate(Create(new string('a', length))).Errors
            .Any(e => e.PropertyName == "FullName").Should().Be(!expected);

    [Fact]
    public void AdminUpdate_AppliesTheSameRule()
    {
        new AdminUpdateEmployeeDtoValidator().Validate(AdminUpdate("Lê Thị Mai")).Errors.Should().NotContain(e => e.PropertyName == "FullName");
        new AdminUpdateEmployeeDtoValidator().Validate(AdminUpdate("Lê Thị Mai 2")).Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Fact]
    public void DecomposedVietnameseLetters_AreAccepted() =>
        PersonNameRule.IsValid("Nguyễn Văn A").Should().BeTrue();
}
