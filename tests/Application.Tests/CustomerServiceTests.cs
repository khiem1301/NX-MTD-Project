using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Khách hàng ngừng / hợp tác lại (như nhà cung cấp): idempotent, ghi nhật ký, lọc theo trạng thái.</summary>
public class CustomerServiceTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CustomerService Sut() => new(_customers, _audit, _uow);

    private Customer Existing(string status)
    {
        var c = new Customer { CustomerId = 3, CustomerName = "Công ty mẹ", Status = status };
        _customers.GetByIdAsync(3).Returns(c);
        return c;
    }

    [Fact]
    public async Task Discontinue_setsStatus_andAudits()
    {
        var c = Existing(RecordStatus.Active);

        var result = await Sut().DiscontinueAsync(3, new DiscontinueCustomerDto("Không còn đặt hàng"), default);

        result.IsSuccess.Should().BeTrue();
        c.DiscontinueReason.Should().Be("Không còn đặt hàng");
        c.Status.Should().Be(RecordStatus.Discontinued);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Customer) && e.Action == AuditActions.Update));
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Discontinue_whenAlreadyDiscontinued_changesNothing()
    {
        Existing(RecordStatus.Discontinued);

        var result = await Sut().DiscontinueAsync(3, new DiscontinueCustomerDto("x"), default);

        result.IsSuccess.Should().BeTrue();
        _audit.DidNotReceive().Add(Arg.Any<AuditEntry>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Reactivate_setsActive_andUnknownIdIsNotFound()
    {
        var c = Existing(RecordStatus.Discontinued);

        (await Sut().ReactivateAsync(3, default)).IsSuccess.Should().BeTrue();
        c.Status.Should().Be(RecordStatus.Active);

        _customers.GetByIdAsync(99).Returns((Customer?)null);
        (await Sut().ReactivateAsync(99, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task List_passesTheStatusAsDbCode()
    {
        _customers.ListAsync(default!, default, default).ReturnsForAnyArgs(new PagedResult<CustomerDto>([], 1, 20, 0));

        await Sut().ListAsync(new CustomerQuery { Keyword = "mẹ", Status = "Discontinued" }, default);

        await _customers.Received(1).ListAsync(Arg.Is<CustomerQuery>(q => q.Keyword == "mẹ"), RecordStatus.Discontinued, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void QueryValidator_acceptsOnlyActiveOrDiscontinued()
    {
        var v = new CustomerQueryValidator();
        v.Validate(new CustomerQuery()).IsValid.Should().BeTrue();
        v.Validate(new CustomerQuery { Status = "Active" }).IsValid.Should().BeTrue();
        v.Validate(new CustomerQuery { Status = "Foo" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task List_withoutStatus_doesNotFilterByStatus()
    {
        _customers.ListAsync(default!, default, default).ReturnsForAnyArgs(new PagedResult<CustomerDto>([], 1, 20, 0));

        await Sut().ListAsync(new CustomerQuery { Status = "  " }, default);

        await _customers.Received(1).ListAsync(Arg.Any<CustomerQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Options_returnsTheRepositoryList()
    {
        var all = new List<CustomerOptionDto> { new(1, "A", "Active"), new(2, "B", "Discontinued") };
        _customers.ListOptionsAsync(default).ReturnsForAnyArgs(all);

        var result = await Sut().GetOptionsAsync(default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Equal(all);
    }
}
