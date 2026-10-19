using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Data;

public static class DbSeeder
{
    public const string PasswordPlaceholder = "__SET_BY_BACKEND_SEED__";

    /// <summary>Thay placeholder trong SQL seed bằng hash BCrypt của Seed:DefaultPassword (mặc định 123456).</summary>
    public static async Task SeedPasswordsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        var pending = await db.Accounts.Where(a => a.PasswordHash == PasswordPlaceholder).ToListAsync(ct);
        if (pending.Count == 0) return;

        var password = config["Seed:DefaultPassword"] ?? "123456";
        foreach (var account in pending) account.PasswordHash = hasher.Hash(password);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded password for {Count} account(s)", pending.Count);
    }
}
