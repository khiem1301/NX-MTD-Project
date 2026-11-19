using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogService _audit;
    private readonly IUnitOfWork _uow;

    public CustomerService(ICustomerRepository customers, IAuditLogService audit, IUnitOfWork uow)
    {
        _customers = customers; _audit = audit; _uow = uow;
    }

    public async Task<Result<PagedResult<CustomerDto>>> ListAsync(CustomerQuery query, CancellationToken ct) =>
        Result<PagedResult<CustomerDto>>.Success(
            await _customers.ListAsync(query, string.IsNullOrWhiteSpace(query.Status) ? null : CodeText.ToCode(query.Status), ct));

    public async Task<Result<IReadOnlyList<CustomerOptionDto>>> GetOptionsAsync(CancellationToken ct) =>
        Result<IReadOnlyList<CustomerOptionDto>>.Success(await _customers.ListOptionsAsync(ct));

    public async Task<Result<int>> CreateAsync(SaveCustomerDto dto, CancellationToken ct)
    {
        var name = dto.CustomerName.Trim();
        if (await _customers.NameExistsAsync(name, null, ct))
            return Result<int>.Failure(ErrorCodes.CustomerNameDuplicate, Messages.CustomerNameDuplicate);
        if (await ContactTakenAsync(dto, null, ct) is { } taken)
            return Result<int>.Failure(taken.Code, taken.Message);

        var customer = new Customer { CustomerName = name, Status = RecordStatus.Active };
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
        if (await ContactTakenAsync(dto, customerId, ct) is { } taken)
            return Result.Failure(taken.Code, taken.Message);

        var before = Snapshot(customer);
        customer.CustomerName = name;
        Apply(customer, dto);
        _audit.Add(new AuditEntry(nameof(Customer), customerId, AuditActions.Update, before, Snapshot(customer)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> DiscontinueAsync(int customerId, DiscontinueCustomerDto dto, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(customerId);
        if (customer is null) return Fail.NotFound();
        if (customer.Status == RecordStatus.Discontinued) return Result.Success(Messages.CustomerDiscontinued);

        customer.Status = RecordStatus.Discontinued;
        customer.DiscontinueReason = Clean(dto.Reason);
        _audit.Add(new AuditEntry(nameof(Customer), customerId, AuditActions.Update,
            new { Status = RecordStatus.Active }, new { customer.Status }, customer.DiscontinueReason,
            $"Ngừng hợp tác với khách hàng {customer.CustomerName}"));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.CustomerDiscontinued);
    }

    /// <summary>Gọi lại khi đã ACTIVE không lỗi, không đổi gì.</summary>
    public async Task<Result> ReactivateAsync(int customerId, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(customerId);
        if (customer is null) return Fail.NotFound();
        if (customer.Status == RecordStatus.Active) return Result.Success(Messages.Saved);

        customer.Status = RecordStatus.Active;
        customer.DiscontinueReason = null;
        _audit.Add(new AuditEntry(nameof(Customer), customerId, AuditActions.Update,
            new { Status = RecordStatus.Discontinued }, new { customer.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    /// <summary>Hai khách hàng không dùng chung số điện thoại hoặc email (bỏ trống thì không kiểm).</summary>
    private async Task<(string Code, string Message)?> ContactTakenAsync(SaveCustomerDto dto, int? exceptId, CancellationToken ct)
    {
        var phone = Clean(dto.Phone);
        if (phone is not null && await _customers.PhoneExistsAsync(phone, exceptId, ct))
            return (ErrorCodes.CustomerPhoneDuplicate, Messages.CustomerPhoneDuplicate);
        var email = Clean(dto.Email);
        if (email is not null && await _customers.EmailExistsAsync(email, exceptId, ct))
            return (ErrorCodes.CustomerEmailDuplicate, Messages.CustomerEmailDuplicate);
        return null;
    }

    private static void Apply(Customer c, SaveCustomerDto dto)
    {
        c.Phone = Clean(dto.Phone);
        c.Email = Clean(dto.Email);
        c.DefaultAddress = Clean(dto.DefaultAddress);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Customer c) => new { c.CustomerName, c.Phone, c.Email, c.DefaultAddress, c.Status };
}
