using System.Linq.Expressions;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace NX_MTD.Application.Common;

/// <summary>
/// Sắp xếp phía máy chủ theo cột của DTO (DEV-ALL-197). EF không sắp được sau khi đã chiếu sang DTO dựng bằng constructor, nên
/// helper đọc chính biểu thức <c>Select</c>: tên tham số constructor (hoặc thành viên) ↔ biểu thức trên entity, rồi sắp trước khi chiếu.
/// Cột không đọc được hoặc không dịch được sang SQL thì bỏ qua và giữ thứ tự mặc định của danh sách.
/// </summary>
public static class SortableSelect
{
    private const string EntityNamespace = "NX_MTD.Domain.Entities";

    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(string), typeof(decimal), typeof(DateTime), typeof(DateOnly), typeof(TimeOnly), typeof(DateTimeOffset), typeof(Guid),
    ];

    /// <summary>Hàm EF dịch được sang SQL trong biểu thức sắp xếp (theo tên, trong LINQ, chuỗi và Math); hàm khác thì bỏ qua cột.</summary>
    private static readonly HashSet<string> TranslatableCalls =
    [
        nameof(Queryable.Sum), nameof(Queryable.Count), nameof(Queryable.LongCount), nameof(Queryable.Any), nameof(Queryable.All),
        nameof(Queryable.Max), nameof(Queryable.Min), nameof(Queryable.Average), nameof(Queryable.Where), nameof(Queryable.Select),
        nameof(Queryable.Contains), nameof(Queryable.FirstOrDefault), nameof(Queryable.First),
        nameof(string.StartsWith), nameof(string.EndsWith), nameof(string.ToUpper), nameof(string.ToLower), nameof(string.Trim),
        nameof(string.Substring), nameof(string.Concat), nameof(string.IsNullOrEmpty),
        nameof(Math.Round), nameof(Math.Abs), nameof(Math.Floor), nameof(Math.Ceiling),
    ];

    private static readonly HashSet<Type> TranslatableCallers = [typeof(Queryable), typeof(Enumerable), typeof(string), typeof(Math)];

    /// <summary>Danh sách phân trang có sắp xếp theo <c>query.SortBy</c> / <c>query.Desc</c>; thứ tự có sẵn của <paramref name="source"/> là mặc định.</summary>
    public static async Task<PagedResult<TDto>> ToPagedAsync<TEntity, TDto>(this IQueryable<TEntity> source,
        Expression<Func<TEntity, TDto>> selector, PageQuery query, CancellationToken ct)
    {
        var sorted = OrderByRequested(source, selector, query.SortBy, query.Desc);
        if (ReferenceEquals(sorted, source)) return await source.Select(selector).ToPagedAsync(query, ct);
        try
        {
            return await sorted.Select(selector).ToPagedAsync(query, ct);
        }
        catch (InvalidOperationException)
        {
            // EF không dịch được cột người dùng chọn sang SQL: danh sách vẫn phải hiện, giữ thứ tự mặc định.
            return await source.Select(selector).ToPagedAsync(query, ct);
        }
    }

    public static IQueryable<TEntity> OrderByRequested<TEntity, TDto>(IQueryable<TEntity> source,
        Expression<Func<TEntity, TDto>> selector, string? sortBy, bool desc)
    {
        var members = MemberExpressions(selector.Body);
        if (string.IsNullOrWhiteSpace(sortBy) || members is null || !members.TryGetValue(sortBy.Trim(), out var raw)) return source;
        var key = Unwrap(raw);
        if (!IsSortable(key)) return source;

        var sorted = ApplyOrder(source, Expression.Lambda(key, selector.Parameters), desc, thenBy: false);
        // Cột đầu của DTO (thường là Id) làm khóa phụ để các dòng trùng giá trị có thứ tự ổn định khi qua trang.
        var tie = members.Values.Select(Unwrap).FirstOrDefault();
        return tie is not null && IsSortable(tie) && !ReferenceEquals(tie, key)
            ? ApplyOrder(sorted, Expression.Lambda(tie, selector.Parameters), desc, thenBy: true)
            : sorted;
    }

    /// <summary>Hướng sắp xếp: mặc định giảm dần, "asc" thì tăng dần (giống <see cref="PageQuery.Desc"/>).</summary>
    public static bool IsDescending(string? sortDir) => !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Một trang dòng đã chiếu sang DTO cho query không kế thừa <see cref="PageQuery"/> (bảng công, chấm bù); cùng quy tắc và đường lùi
    /// như <c>ToPagedAsync(selector, ...)</c>.
    /// </summary>
    public static async Task<List<TDto>> ToPageListAsync<TEntity, TDto>(IQueryable<TEntity> source,
        Expression<Func<TEntity, TDto>> selector, string? sortBy, string? sortDir, int skip, int take, CancellationToken ct)
    {
        var sorted = OrderByRequested(source, selector, sortBy, IsDescending(sortDir));
        try
        {
            return await sorted.Skip(skip).Take(take).Select(selector).ToListAsync(ct);
        }
        catch (InvalidOperationException) when (!ReferenceEquals(sorted, source))
        {
            // EF không dịch được cột người dùng chọn sang SQL: giữ thứ tự mặc định.
            return await source.Skip(skip).Take(take).Select(selector).ToListAsync(ct);
        }
    }

    private static readonly CompareInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN").CompareInfo;

    /// <summary>Sắp xếp các dòng đã tính trong bộ nhớ (số liệu không có trong SQL) theo thuộc tính cùng tên của DTO; không có thì giữ nguyên.</summary>
    public static IEnumerable<T> OrderInMemory<T>(IEnumerable<T> rows, string? sortBy, bool desc)
    {
        var property = string.IsNullOrWhiteSpace(sortBy)
            ? null
            : typeof(T).GetProperty(sortBy.Trim(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null || !property.CanRead) return rows;
        var comparer = Comparer<object?>.Create(CompareValues);
        return desc ? rows.OrderByDescending(r => property.GetValue(r), comparer) : rows.OrderBy(r => property.GetValue(r), comparer);
    }

    /// <summary>Ô trống xếp trước, chữ so theo tiếng Việt, kiểu so sánh được thì so trực tiếp.</summary>
    private static int CompareValues(object? a, object? b)
    {
        if (a is null) return b is null ? 0 : -1;
        if (b is null) return 1;
        if (a is string x && b is string y) return Vietnamese.Compare(x, y, CompareOptions.None);
        return a is IComparable comparable && a.GetType() == b.GetType() ? comparable.CompareTo(b) : 0;
    }

    /// <summary>
    /// Danh sách phân trang trả thẳng entity (chưa chiếu sang DTO): sắp theo thuộc tính cùng tên có kiểu đơn giản. Kiểu khác (DTO, kiểu
    /// ẩn danh) không đụng tới vì đã được sắp trước khi chiếu.
    /// </summary>
    public static IQueryable<T> OrderEntityByRequested<T>(IQueryable<T> source, string? sortBy, bool desc)
    {
        if (string.IsNullOrWhiteSpace(sortBy) || typeof(T).Namespace != EntityNamespace) return source;
        var property = typeof(T).GetProperty(sortBy.Trim(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null || !property.CanRead) return source;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!(type.IsPrimitive || type.IsEnum || ScalarTypes.Contains(type))) return source;
        var parameter = Expression.Parameter(typeof(T), "x");
        return ApplyOrder(source, Expression.Lambda(Expression.Property(parameter, property), parameter), desc, thenBy: false);
    }

    /// <summary>Tên cột DTO → biểu thức trên entity, theo thứ tự khai báo; null nếu DTO không dựng bằng constructor / khởi tạo thành viên.</summary>
    private static Dictionary<string, Expression>? MemberExpressions(Expression body)
    {
        var map = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);
        switch (body)
        {
            case NewExpression created:
                AddArguments(created, map);
                return map;
            case MemberInitExpression init:
                AddArguments(init.NewExpression, map);
                foreach (var binding in init.Bindings.OfType<MemberAssignment>()) map[binding.Member.Name] = binding.Expression;
                return map;
            default:
                return null;
        }
    }

    private static void AddArguments(NewExpression created, Dictionary<string, Expression> map)
    {
        var names = created.Members?.Select(m => m.Name).ToArray()
                    ?? created.Constructor?.GetParameters().Select(p => p.Name ?? string.Empty).ToArray() ?? [];
        for (var i = 0; i < names.Length && i < created.Arguments.Count; i++)
            if (names[i].Length > 0) map[names[i]] = created.Arguments[i];
    }

    /// <summary>Bỏ ép kiểu và hàm đổi mã trạng thái sang chữ hiển thị (CodeText.ToPascal): sắp theo giá trị gốc trong DB.</summary>
    private static Expression Unwrap(Expression e)
    {
        while (true)
        {
            if (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert) e = convert.Operand;
            else if (e is MethodCallExpression { Method.Name: "ToPascal", Arguments.Count: 1 } call) e = call.Arguments[0];
            else return e;
        }
    }

    private static bool IsSortable(Expression e)
    {
        var type = Nullable.GetUnderlyingType(e.Type) ?? e.Type;
        return (type.IsPrimitive || type.IsEnum || ScalarTypes.Contains(type)) && !new UntranslatableFinder().Find(e);
    }

    private static IQueryable<T> ApplyOrder<T>(IQueryable<T> source, LambdaExpression key, bool desc, bool thenBy)
    {
        var name = (thenBy, desc) switch
        {
            (false, false) => nameof(Queryable.OrderBy),
            (false, true) => nameof(Queryable.OrderByDescending),
            (true, false) => nameof(Queryable.ThenBy),
            (true, true) => nameof(Queryable.ThenByDescending),
        };
        var method = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == name && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), key.ReturnType);
        return (IQueryable<T>)method.Invoke(null, [source, key])!;
    }

    /// <summary>Tìm lời gọi hàm của mình (ngoài LINQ, chuỗi, Math) vì EF không dịch được sang SQL.</summary>
    private sealed class UntranslatableFinder : ExpressionVisitor
    {
        private bool _found;

        public bool Find(Expression e)
        {
            Visit(e);
            return _found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!TranslatableCallers.Contains(node.Method.DeclaringType!) || !TranslatableCalls.Contains(node.Method.Name)) _found = true;
            return base.VisitMethodCall(node);
        }
    }
}
