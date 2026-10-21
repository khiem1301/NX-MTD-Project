using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-19 — "Báo cáo kế toán" (UC36, CHỈ Kế toán, Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class AccountingReportModule
{
    public static IServiceCollection AddAccountingReportModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAccountingReportExcelWriter, ClosedXmlAccountingReportWriter>();
        services.AddScoped<IAccountingReportRepository, AccountingReportRepository>();
        services.AddScoped<IAccountingReportService, AccountingReportService>();
        return services;
    }
}
