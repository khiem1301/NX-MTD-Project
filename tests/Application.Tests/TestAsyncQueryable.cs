using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Application.Tests;

/// <summary>
/// Cho phép NSubstitute trả về IQueryable&lt;T&gt; (từ IGenericRepository&lt;T&gt;.Query()) mà vẫn chạy được
/// các hàm async của EF Core (FirstOrDefaultAsync, ToListAsync, AnyAsync, SumAsync...) khi test — các hàm
/// này cần IAsyncQueryProvider, EnumerableQuery&lt;T&gt; bình thường không có. Dùng cho KpiServiceTests vì
/// KpiService (như WorkOrderService) truy vấn thẳng qua IUnitOfWork/IGenericRepository, không qua repository
/// interface riêng nên không mock bằng Task&lt;T&gt; trả thẳng như MealService/PayrollService được.
/// </summary>
internal static class TestAsyncQueryableExtensions
{
    public static IQueryable<T> AsTestAsyncQueryable<T>(this IEnumerable<T> source) => new TestAsyncEnumerable<T>(source);
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return default;
    }
}

internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    /// <summary>EF gọi khi hàm *Async trả Task&lt;TResult&gt; (ToListAsync...) hoặc Task&lt;TItem&gt; (FirstOrDefaultAsync...).</summary>
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).IsGenericType ? typeof(TResult).GetGenericArguments()[0] : typeof(TResult);
        var executionResult = typeof(IQueryProvider)
            .GetMethods().Single(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
            .MakeGenericMethod(resultType)
            .Invoke(this, [expression]);

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [executionResult])!;
    }
}
