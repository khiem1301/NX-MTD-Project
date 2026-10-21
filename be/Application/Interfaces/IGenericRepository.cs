using System.Linq.Expressions;

namespace NX_MTD.Application.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> Query();
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);

        /// <summary>Gắn RowVer client gửi lên làm giá trị gốc để EF kiểm tra đồng thời (R12) → 409 khi lệch.</summary>
        void SetOriginalRowVersion(T entity, byte[] rowVer);
    }
}
