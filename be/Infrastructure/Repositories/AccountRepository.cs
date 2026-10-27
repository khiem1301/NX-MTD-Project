using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    public AccountRepository(NxMtdDbContext context) : base(context) { }

    public Task<Account?> GetByUsernameAsync(string username, CancellationToken ct) =>
        WithEmployee().FirstOrDefaultAsync(a => a.Employee.EmployeeCode == username, ct);

    public Task<Account?> GetWithEmployeeAsync(int accountId, CancellationToken ct) =>
        WithEmployee().FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    private IQueryable<Account> WithEmployee() =>
        _context.Accounts.AsNoTracking()
            .Include(a => a.Employee).ThenInclude(e => e.Role)
            .Include(a => a.Employee).ThenInclude(e => e.Team);
}
