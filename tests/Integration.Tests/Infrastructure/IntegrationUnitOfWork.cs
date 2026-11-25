using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;
using NX_MTD.Infrastructure.Repositories;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// UoW cho IT: SaveChanges ghi vào transaction ambient.
/// Begin/Commit dùng SAVE TRANSACTION để service vẫn rollback được nhánh lỗi mà không phá ambient.
/// </summary>
public sealed class IntegrationUnitOfWork : IUnitOfWork
{
    private readonly NxMtdDbContext _context;
    private readonly TestDbState _state;
    private int _savepointDepth;

    public IntegrationUnitOfWork(NxMtdDbContext context, TestDbState state)
    {
        _context = context;
        _state = state;
    }

    public IGenericRepository<T> Repository<T>() where T : class =>
        new GenericRepository<T>(_context);

    /// <summary>
    /// Production: EF tự bọc SaveChanges trong transaction riêng nên lỗi giữa batch (vd. RowVer lệch) rollback toàn bộ.
    /// Với transaction ambient EF không làm vậy, nên dùng savepoint để SaveChanges lỗi vẫn atomic.
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        var name = $"it_sc_{Guid.NewGuid():N}"[..20];
        await _context.Database.ExecuteSqlRawAsync($"SAVE TRANSACTION {name}");
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch
        {
            await _context.Database.ExecuteSqlRawAsync($"ROLLBACK TRANSACTION {name}");
            throw;
        }
    }

    public async Task BeginTransactionAsync()
    {
        if (_state.Transaction is null)
            throw new InvalidOperationException("Ambient IT transaction chưa được BaseIntegrationTest mở.");

        _savepointDepth++;
        var name = SavepointName(_savepointDepth);
        await _context.Database.ExecuteSqlAsync($"SAVE TRANSACTION {name}");
    }

    public Task CommitTransactionAsync()
    {
        if (_savepointDepth > 0)
            _savepointDepth--;
        return Task.CompletedTask;
    }

    public async Task RollbackTransactionAsync()
    {
        if (_savepointDepth <= 0)
            return;

        var name = SavepointName(_savepointDepth);
        await _context.Database.ExecuteSqlAsync($"ROLLBACK TRANSACTION {name}");
        _savepointDepth--;
    }

    public void Dispose()
    {
        // DbContext do DI quản lý — không dispose ở đây.
    }

    private static string SavepointName(int depth) => $"it_sp_{depth}";
}
