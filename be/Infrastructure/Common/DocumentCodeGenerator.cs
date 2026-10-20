using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Common;

public sealed class DocumentCodeGenerator : IDocumentCodeGenerator
{
    private const int SequenceDigits = 3;
    private readonly NxMtdDbContext _context;
    private readonly IAppLock _locks;
    private readonly IClock _clock;

    public DocumentCodeGenerator(NxMtdDbContext context, IAppLock locks, IClock clock)
    {
        _context = context; _locks = locks; _clock = clock;
    }

    public async Task<string> NextAsync<T>(string prefix, Expression<Func<T, string>> codeSelector,
        CancellationToken ct) where T : class
    {
        var stem = $"{prefix}-{_clock.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-";
        await _locks.AcquireAsync($"doc-code:{stem}", ct);

        var last = await _context.Set<T>().AsNoTracking().Select(codeSelector)
            .Where(c => c.StartsWith(stem)).MaxAsync(c => (string?)c, ct);
        var next = last is not null && int.TryParse(last[stem.Length..], out var n) ? n + 1 : 1;
        return stem + next.ToString(new string('0', SequenceDigits), CultureInfo.InvariantCulture);
    }
}
