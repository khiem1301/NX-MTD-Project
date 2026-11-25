using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Data;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// Host dành riêng cho test đồng thời: KHÔNG có ambient transaction. Mỗi DbContext (mỗi request) có connection riêng tới Test DB thật,
/// UnitOfWork là bản thật của ứng dụng. Dữ liệu mồi được commit nên request song song nhìn thấy nhau; test tự dọn sau khi chạy.
/// </summary>
public sealed class ConcurrentTestFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; }
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public ConcurrentTestFactory()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Integration.json"), optional: false)
            .AddEnvironmentVariables()
            .Build();
        ConnectionString = config.GetConnectionString("Default") ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default.");
        _jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Thiếu Jwt:Key.");
        _jwtIssuer = config["Jwt:Issuer"]!;
        _jwtAudience = config["Jwt:Audience"]!;
    }

    public NxMtdDbContext NewContext() =>
        new(new DbContextOptionsBuilder<NxMtdDbContext>().UseSqlServer(ConnectionString).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Integration.json"), optional: false);
            config.AddEnvironmentVariables();
        });

        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList())
                services.Remove(d);

            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(NxMtdDbContext)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                             && d.ServiceType.GenericTypeArguments[0] == typeof(NxMtdDbContext))).ToList())
                services.Remove(d);

            services.AddDbContext<NxMtdDbContext>(o => o.UseSqlServer(ConnectionString));
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(FixedClock.Default);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.TokenValidationParameters.ValidIssuer = _jwtIssuer;
                o.TokenValidationParameters.ValidAudience = _jwtAudience;
                o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            });
        });
    }
}
