using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Notifications;
using NX_MTD.Infrastructure.Settings;

namespace NX_MTD.API.Modules;

/// <summary>Dịch vụ dùng chung của Khiêm. IClock, IGenericRepository đã đăng ký ở AddInfrastructure.</summary>
public static class SharedModule
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
