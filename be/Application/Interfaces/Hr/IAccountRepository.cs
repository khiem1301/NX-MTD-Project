using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

public interface IAccountRepository : IGenericRepository<Account>
{
    /// <summary>Đọc kèm Employee, Role, Team; AsNoTracking. Khớp Account.Username.</summary>
    Task<Account?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<Account?> GetWithEmployeeAsync(int accountId, CancellationToken ct);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);

    /// <summary>Email (không phân biệt hoa thường) đã gắn với một tài khoản.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
}
