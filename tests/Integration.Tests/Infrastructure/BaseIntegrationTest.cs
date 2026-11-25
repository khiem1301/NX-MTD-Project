using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;
using Integration.Tests.Seed;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// Mỗi test case: mở ambient transaction → seed Minimum Test Data → chạy → Rollback.
/// Không test nào phụ thuộc dữ liệu sót lại từ test trước.
/// </summary>
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected IntegrationTestFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    protected NxMtdDbContext Db { get; private set; } = null!;
    protected TestContext Ctx { get; private set; } = null!;
    protected FixedClock Clock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new IntegrationTestFactory();
        Clock = Factory.Clock;

        // Mở connection + ambient transaction TRƯỚC khi host/HTTP client tạo DbContext.
        Factory.EnsureConnection();
        await Factory.BeginAmbientTransactionAsync();

        Client = Factory.CreateClient();

        Scope = Factory.Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
        var hasher = Scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var seeder = new TestDataSeeder(Db, hasher, Clock);
        Ctx = await seeder.SeedAsync();
    }

    public async Task DisposeAsync()
    {
        Scope?.Dispose();
        Client?.Dispose();

        if (Factory is not null)
        {
            await Factory.RollbackAmbientTransactionAsync();
            await Factory.DisposeAsync();
        }
    }

    /// <summary>DbContext mới gắn cùng ambient transaction — dùng để đọc lại DB sau khi gọi API.</summary>
    protected NxMtdDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<NxMtdDbContext>();
    }
}
