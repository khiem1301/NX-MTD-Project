using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Orders;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;

    public CustomerService(ICustomerRepository customers, IAuditLogService audit, IUnitOfWork uow)
    {
        _customers = customers; _audit = audit; _uow = uow;
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> ListAsync(string? keyword, CancellationToken ct) =>
        Result<IReadOnlyList<CustomerDto>>.Success(await _customers.ListAsync(keyword, ct));

    public async Task<Result<int>> CreateAsync(SaveCustomerDto dto, CancellationToken ct)
    {
        var name = dto.CustomerName.Trim();
        if (await _customers.NameExistsAsync(name, null, ct))
            return Result<int>.Failure(ErrorCodes.CustomerNameDuplicate, Messages.CustomerNameDuplicate);

        var customer = new Customer { CustomerName = name };
        Apply(customer, dto);
        return await _uow.InTransactionAsync(async () =>   // D17: log cần CustomerId
        {
            await _customers.AddAsync(customer);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Customer), customer.CustomerId, AuditActions.Insert, null, Snapshot(customer)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(customer.CustomerId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int customerId, SaveCustomerDto dto, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(customerId);
        if (customer is null) return Fail.NotFound();
        var name = dto.CustomerName.Trim();
        if (await _customers.NameExistsAsync(name, customerId, ct))
            return Result.Failure(ErrorCodes.CustomerNameDuplicate, Messages.CustomerNameDuplicate);

        var before = Snapshot(customer);
        customer.CustomerName = name;
        Apply(customer, dto);
        _audit.Add(new AuditEntry(nameof(Customer), customerId, AuditActions.Update, before, Snapshot(customer)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    private static void Apply(Customer c, SaveCustomerDto dto)
    {
        c.Phone = Clean(dto.Phone);
        c.Email = Clean(dto.Email);
        c.DefaultAddress = Clean(dto.DefaultAddress);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Customer c) => new { c.CustomerName, c.Phone, c.Email, c.DefaultAddress };
}
