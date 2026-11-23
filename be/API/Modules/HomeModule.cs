using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;

namespace NX_MTD.API.Modules;

/// <summary>DEV-ALL-170: trang chủ theo vai trò. Dùng service của các module khác nên đăng ký sau cùng trong Program.cs.</summary>
public static class HomeModule
{
    public static IServiceCollection AddHomeModule(this IServiceCollection services)
    {
        services.AddScoped<IHomeDashboardService, HomeDashboardService>();
        return services;
    }
}
