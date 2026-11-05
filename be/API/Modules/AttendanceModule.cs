using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Excel;
using NX_MTD.Infrastructure.Jobs;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.API.Modules;

/// <summary>Luồng chấm công và suất ăn (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class AttendanceModule
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IExcelAttendanceParser, ClosedXmlAttendanceParser>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAttendanceService, AttendanceService>();

        // Suất ăn (UC30) + 3 job nền (MI-04)
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IMealService, MealService>();
        services.AddScoped<IMealSettingsService, MealSettingsService>();
        services.AddHostedService<AttendanceSyncMonitorJob>();
        services.AddHostedService<MealReminderJob>();
        services.AddHostedService<MealAutoCloseJob>();
        return services;
    }
}
