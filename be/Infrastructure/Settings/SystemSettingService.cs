using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Settings;

public sealed class SystemSettingService : ISystemSettingService
{
    private readonly NxMtdDbContext _db;
    public SystemSettingService(NxMtdDbContext db) => _db = db;

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct)
    {
        var value = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == key).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }
}
