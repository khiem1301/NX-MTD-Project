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
