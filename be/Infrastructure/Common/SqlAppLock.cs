using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Common;

public sealed class SqlAppLock : IAppLock
{
    private const int LockTimeoutMs = 10000;
    private readonly NxMtdDbContext _context;

    public SqlAppLock(NxMtdDbContext context) => _context = context;

    public Task AcquireAsync(string resource, CancellationToken ct)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("IAppLock phải được gọi trong transaction.");

        return _context.Database.ExecuteSqlRawAsync(
            "DECLARE @r INT; " +
            "EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {1}; " +
            "IF @r < 0 THROW 50010, N'Không lấy được khóa dữ liệu, vui lòng thử lại.', 1;",
            new object[] { resource, LockTimeoutMs }, ct);
    }
}
