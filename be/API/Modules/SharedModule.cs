using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Audit;
using NX_MTD.Infrastructure.Jobs;
using NX_MTD.Infrastructure.Notifications;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Storage;

namespace NX_MTD.API.Modules;

/// <summary>Dịch vụ dùng chung của Khiêm. IClock, ISystemSettingReader, IGenericRepository đã đăng ký ở AddInfrastructure.</summary>
public static class SharedModule
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.Configure<AttachmentOptions>(config.GetSection("Attachment"));
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IAttachmentCleanupService, AttachmentCleanupService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddHostedService<DelayRiskJob>();           // JOB-04 (KH-23)
        services.AddHostedService<NotificationCleanupJob>(); // JOB-05 (KH-23)
        services.AddHostedService<ProductImageCleanupJob>(); // dọn ảnh mồ côi của mã hàng
        services.AddHostedService<AttachmentCleanupJob>();   // dọn ảnh chứng từ đã gỡ quá 15 ngày
        return services;
    }
}
