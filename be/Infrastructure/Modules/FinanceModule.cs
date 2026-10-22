using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-13 — Doanh thu (UC27) + Hòa vốn, giá trị thặng dư (UC11, Minh). Đăng ký bằng một dòng
/// trong Program.cs.</summary>
public static class FinanceModule
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IFinanceExcelWriter, ClosedXmlFinanceWriter>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();
        services.AddScoped<IFinanceService, FinanceService>();
        return services;
    }
}
