namespace NX_MTD.Application.Common;

/// <summary>Khối data của response phân trang (Rules mục 2.3).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

/// <summary>Chặn trên của pageSize theo Rules mục 2.1.</summary>
public static class Paging
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
