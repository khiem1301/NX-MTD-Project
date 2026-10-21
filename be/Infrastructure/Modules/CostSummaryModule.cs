using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-19 — "Tổng hợp chi phí" (UC12, CHỈ Admin, Minh). Dựa trên MonthlyCostModule + PayrollModule
/// đã đăng ký trước (IMonthlyCostService, IPayrollRepository) — đăng ký module này sau hai module đó trong
/// Program.cs. Đăng ký bằng một dòng trong Program.cs.</summary>
public static class CostSummaryModule
{
    public static IServiceCollection AddCostSummaryModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<ICostSummaryExcelWriter, ClosedXmlCostSummaryWriter>();
        services.AddScoped<ICostSummaryService, CostSummaryService>();
        return services;
    }
}
