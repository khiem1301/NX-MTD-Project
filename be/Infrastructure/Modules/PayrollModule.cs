using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-09 — Bảng lương + khóa tháng (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class PayrollModule
{
    public static IServiceCollection AddPayrollModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IExcelPayrollParser, ClosedXmlPayrollParser>();
        services.AddSingleton<IPayrollExcelWriter, ClosedXmlPayrollWriter>();
        services.AddSingleton<IPayrollFileStore, LocalPayrollFileStore>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IPayrollService, PayrollService>();
        return services;
    }
}
