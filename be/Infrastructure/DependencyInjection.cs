using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Common;
using NX_MTD.Infrastructure.Data;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Security;

namespace NX_MTD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<NxMtdDbContext>(o => o.UseSqlServer(config.GetConnectionString("Default")));
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        // Kho, cắt (Thái)
        services.AddSingleton<IClock, VnClock>();
        services.AddScoped<IAppLock, SqlAppLock>();
        services.AddScoped<IDocumentCodeGenerator, DocumentCodeGenerator>();
        services.AddScoped<IStockRepository, StockRepository>();
        return services;
    }
}
