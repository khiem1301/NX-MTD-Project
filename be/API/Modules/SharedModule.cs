using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Audit;
using NX_MTD.Infrastructure.Notifications;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Settings;
using NX_MTD.Infrastructure.Storage;

namespace NX_MTD.API.Modules;

/// <summary>Dịch vụ dùng chung của Khiêm. IClock, IGenericRepository đã đăng ký ở AddInfrastructure.</summary>
public static class SharedModule
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<AttachmentOptions>(config.GetSection("Attachment"));
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
