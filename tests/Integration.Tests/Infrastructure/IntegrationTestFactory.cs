using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;

namespace Integration.Tests.Infrastructure;

/// <summary>WebApplicationFactory gắn Test DB thật + transaction ambient (không mock Repository/DbContext).</summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly TestDbState _dbState = new();
    private readonly FixedClock _clock = FixedClock.Default;
    private readonly string _connectionString;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public TestDbState DbState => _dbState;
    public FixedClock Clock => _clock;

    public IntegrationTestFactory()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.Integration.json"),
                optional: false,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Thiếu ConnectionStrings:Default trong appsettings.Integration.json.");
        _jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Thiếu Jwt:Key trong appsettings.Integration.json.");
        _jwtIssuer = config["Jwt:Issuer"]!;
        _jwtAudience = config["Jwt:Audience"]!;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.Integration.json"),
                optional: false,
                reloadOnChange: false);
            config.AddEnvironmentVariables();
        });

        builder.ConfigureTestServices(services =>
        {
            // Tắt toàn bộ background job — chúng tranh connection/transaction ambient.
            foreach (var d in services.Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList())
                services.Remove(d);

            RemoveDbContextRegistrations(services);
            services.RemoveAll<IUnitOfWork>();
            services.RemoveAll<IClock>();

            services.AddSingleton(_dbState);
            services.AddSingleton<IClock>(_clock);

            services.AddScoped(provider =>
            {
                EnsureConnection();
                var options = new DbContextOptionsBuilder<NxMtdDbContext>()
                    .UseSqlServer(_dbState.Connection)
                    .Options;
                var ctx = new NxMtdDbContext(options);
                if (_dbState.Transaction is not null)
                    ctx.Database.UseTransaction(_dbState.Transaction);
                return ctx;
            });

            services.AddScoped<IUnitOfWork, IntegrationUnitOfWork>();

            // AddApiCore đọc Jwt:Key/Issuer/Audience ngay lúc dựng host (config của app), còn TokenService đọc muộn qua
            // IOptions (config Integration) ⇒ token cấp ra bị JwtBearer từ chối. Ép JwtBearer dùng đúng khóa của Test config.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.TokenValidationParameters.ValidIssuer = _jwtIssuer;
                o.TokenValidationParameters.ValidAudience = _jwtAudience;
                o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            });
        });
    }

    public void EnsureConnection()
    {
        if (_dbState.Connection is { State: System.Data.ConnectionState.Open })
            return;

        _dbState.Connection?.Dispose();
        _dbState.Connection = new SqlConnection(_connectionString);
        _dbState.Connection.Open();
    }

    public Task BeginAmbientTransactionAsync()
    {
        EnsureConnection();
        if (_dbState.Transaction is not null)
            return Task.CompletedTask;

        _dbState.Transaction = _dbState.Connection.BeginTransaction();
        return Task.CompletedTask;
    }

    public async Task RollbackAmbientTransactionAsync()
    {
        if (_dbState.Transaction is null)
            return;

        await _dbState.Transaction.RollbackAsync();
        await _dbState.Transaction.DisposeAsync();
        _dbState.Transaction = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbState.Transaction?.Dispose();
            _dbState.Connection?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(NxMtdDbContext)
                || d.ServiceType == typeof(DbContextOptions<NxMtdDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(NxMtdDbContext)))
            .ToList();

        foreach (var d in toRemove)
            services.Remove(d);
    }
}
