using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Jobs;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-16 — KPI tổ may (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class KpiModule
{
    public static IServiceCollection AddKpiModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IKpiService, KpiService>();
        services.AddHostedService<KpiAutoCloseJob>();
        services.AddHostedService<DelayRiskJob>();           // JOB-04 (KH-23)
        services.AddHostedService<NotificationCleanupJob>(); // JOB-05 (KH-23)
        return services;
    }
}
