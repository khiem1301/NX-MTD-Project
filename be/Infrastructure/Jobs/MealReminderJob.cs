using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>JOB-02 (MI-04): 08:15 nhắc các tổ chưa xác nhận số suất ăn.</summary>
public sealed class MealReminderJob : DailyScheduledJob
{
    public MealReminderJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<MealReminderJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.MealReminderTime;

    protected override TimeOnly DefaultScheduleTime => new(8, 15);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IMealService>().RunReminderJobAsync(today, ct);
}
