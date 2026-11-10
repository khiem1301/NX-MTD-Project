using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Settings;

/// <summary>
/// Đọc SystemSetting, cache ngắn vì tham số đổi rất ít nhưng được gọi trong mọi job chấm công.
/// </summary>
public sealed class SystemSettingReader : ISystemSettingReader
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly NxMtdDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;

    public SystemSettingReader(NxMtdDbContext context, IMemoryCache cache, IClock clock)
    {
        _context = context;
        _cache = cache;
        _clock = clock;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(key), out string? cached)) return cached;

        var value = await _context.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == key)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        _cache.Set(CacheKey(key), value, CacheDuration);
        return value;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await GetAsync(key, ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct)
    {
        var raw = await GetAsync(key, ct);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public async Task<TimeOnly> GetTimeAsync(string key, TimeOnly fallback, CancellationToken ct)
    {
        var raw = await GetAsync(key, ct);
        return TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task SetAsync(string key, string value, string? description, CancellationToken ct)
    {
        var row = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key, ct);
        if (row is null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description,
                UpdatedAt = _clock.Now,
            });
        }
        else
        {
            row.SettingValue = value;
            row.UpdatedAt = _clock.Now;
            if (description is not null) row.Description = description;
        }

        await _context.SaveChangesAsync(ct);
        Invalidate(key);
    }

    public void Invalidate(string key) => _cache.Remove(CacheKey(key));

    private static string CacheKey(string key) => $"setting:{key}";
}
