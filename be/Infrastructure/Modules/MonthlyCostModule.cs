using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-12 — Chi phí tháng (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class MonthlyCostModule
{
    public static IServiceCollection AddMonthlyCostModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IExcelMonthlyCostParser, ClosedXmlMonthlyCostParser>();
        services.AddSingleton<IExcelEmployeeParser, ClosedXmlEmployeeParser>();   // KH-22
        services.AddSingleton<IMonthlyCostExcelWriter, ClosedXmlMonthlyCostWriter>();
        services.AddScoped<IMonthlyCostRepository, MonthlyCostRepository>();
        services.AddScoped<IMonthlyCostService, MonthlyCostService>();
        return services;
    }
}
