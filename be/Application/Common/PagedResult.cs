using Microsoft.EntityFrameworkCore;

namespace NX_MTD.Application.Common;

/// <summary>Khớp type Paginated&lt;T&gt; ở FE (src/types/api.ts).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

/// <summary>Tham số phân trang chung: ?page=1&amp;pageSize=20&amp;keyword=...</summary>
public record PageQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string? Keyword { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }

    public int Skip => (SafePage - 1) * SafePageSize;
    public bool Desc => !string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase);

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> source, PageQuery query,
        CancellationToken ct)
    {
        var total = await source.CountAsync(ct);
        var items = await source.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, query.SafePage, query.SafePageSize, total);
    }
}
