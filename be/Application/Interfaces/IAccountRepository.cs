using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface IAccountRepository : IGenericRepository<Account>
{
    /// <summary>Đọc kèm Employee, Role, Team; AsNoTracking.</summary>
    Task<Account?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<Account?> GetWithEmployeeAsync(int accountId, CancellationToken ct);
}
