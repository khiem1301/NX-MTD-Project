using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>JOB-04 (17:00): đặt cờ IsDelayRisk cho đơn có nguy cơ trễ và báo Admin + Quản đốc ngày đầu tiên bị gắn cờ.</summary>
public sealed class DelayRiskJob : DailyScheduledJob
{
    public DelayRiskJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<DelayRiskJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.DelayCheckTime;
    protected override TimeOnly DefaultScheduleTime => new(17, 0);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IOrderProgressService>().RunDelayCheckAsync(today, ct);
}

/// <summary>JOB-05 (01:00): xóa thông báo quá hạn lưu (mặc định 90 ngày).</summary>
public sealed class NotificationCleanupJob : DailyScheduledJob
{
    public NotificationCleanupJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<NotificationCleanupJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.NotificationCleanupTime;
    protected override TimeOnly DefaultScheduleTime => new(1, 0);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IOrderProgressService>().PurgeOldNotificationsAsync(today, ct);
}

/// <summary>Dọn ảnh mồ côi của mã hàng (02:00): xóa file ảnh quá 24 giờ mà không còn mã hàng nào dùng.</summary>
public sealed class ProductImageCleanupJob : DailyScheduledJob
{
    public ProductImageCleanupJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<ProductImageCleanupJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.ProductImageCleanupTime;
    protected override TimeOnly DefaultScheduleTime => new(2, 0);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IProductImageCleanupService>().RunAsync(DateTime.UtcNow, ct);
}

/// <summary>Dọn tệp chứng từ (02:30): xóa ảnh đã gỡ quá 15 ngày và file không còn dòng Attachment nào trỏ tới quá 15 ngày.</summary>
public sealed class AttachmentCleanupJob : DailyScheduledJob
{
    public AttachmentCleanupJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<AttachmentCleanupJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.AttachmentCleanupTime;
    protected override TimeOnly DefaultScheduleTime => new(2, 30);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IAttachmentCleanupService>().RunAsync(DateTime.UtcNow, ct);
}
