using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Jobs;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>Luồng chấm công và suất ăn (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class AttendanceModule
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.AddSingleton<IClock, VietnamClock>();
        services.AddScoped<ISystemSettingReader, SystemSettingReader>();
        services.AddSingleton<IExcelAttendanceParser, ClosedXmlAttendanceParser>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAttendanceService, AttendanceService>();

        // Suất ăn (UC30) + 3 job nền (MI-04)
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IMealService, MealService>();
        services.AddHostedService<AttendanceSyncMonitorJob>();
        services.AddHostedService<MealReminderJob>();
        services.AddHostedService<MealAutoCloseJob>();
        return services;
    }
}
