using Microsoft.EntityFrameworkCore.Storage;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly Dictionary<Type, object> _repositories = new();
        private IDbContextTransaction? _transaction;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IGenericRepository<T> Repository<T>() where T : class
        {
            if (!_repositories.TryGetValue(typeof(T), out var repo))
            {
                repo = new GenericRepository<T>(_context);
                _repositories[typeof(T)] = repo;
            }
            return (IGenericRepository<T>)repo;
        }

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null) return;
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null) return;
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}
