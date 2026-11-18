using FluentAssertions;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-125: SĐT / email không trùng chéo giữa khách hàng và nhà cung cấp (kể cả đã ngừng).</summary>
public class PartnerContactCheckerTests : ProductionTestBase
{
    public PartnerContactCheckerTests()
    {
        SetupQueryable(new List<Customer>
        {
            new() { CustomerId = 1, CustomerName = "Cá Mắm", Phone = "0901 234 567", Email = "camam@mail.vn", Status = RecordStatus.Active },
            new() { CustomerId = 2, CustomerName = "Khách cũ", Phone = "0911000111", Status = RecordStatus.Discontinued },
        });
        SetupQueryable(new List<Supplier>
        {
            new() { SupplierId = 7, SupplierName = "Phương Mắm", Phone = "0988.777.666", Email = "NCC@Mail.vn", Status = RecordStatus.Active },
        });
    }

    private PartnerContactChecker Sut() => new(Uow);

    [Fact]
    public async Task CheckAsync_PhoneOfASupplier_IsDuplicateForANewCustomer()
    {
        var result = await Sut().CheckAsync("0988 777 666", null, null, null, default);

        result!.ErrorCode.Should().Be(ErrorCodes.PartnerPhoneDuplicate);
        result.Errors!["phone"].Single().Should().Contain("nhà cung cấp Phương Mắm");
    }

    [Fact]
    public async Task CheckAsync_EmailComparesIgnoringCase_AcrossCustomersAndSuppliers()
    {
        var result = await Sut().CheckAsync(null, "ncc@MAIL.vn", null, null, default);

        result!.ErrorCode.Should().Be(ErrorCodes.PartnerEmailDuplicate);
        result.Errors!.Should().ContainKey("email");
    }

    [Fact]
    public async Task CheckAsync_DiscontinuedCustomerStillCounts()
    {
        var result = await Sut().CheckAsync("0911-000-111", null, null, 7, default);

        result!.Errors!["phone"].Single().Should().Contain("khách hàng Khách cũ");
    }

    [Fact]
    public async Task CheckAsync_EditingItself_IsNotDuplicate()
    {
        (await Sut().CheckAsync("0901234567", "camam@mail.vn", 1, null, default)).Should().BeNull();
        (await Sut().CheckAsync("0988777666", "ncc@mail.vn", null, 7, default)).Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_BothDuplicate_ReturnsBothFieldErrors()
    {
        var result = await Sut().CheckAsync("0901234567", "ncc@mail.vn", null, null, default);

        result!.Kind.Should().Be(ResultKind.Validation);
        result.Errors!.Keys.Should().BeEquivalentTo(["phone", "email"]);
    }

    [Fact]
    public async Task CheckAsync_NoPhoneNoEmail_IsNotDuplicate() =>
        (await Sut().CheckAsync(" ", null, null, null, default)).Should().BeNull();
}
