using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// JOB-03 (MI-04): 08:30 chốt suất ăn — tổ chưa xác nhận lấy theo số đã quẹt, văn phòng lấy theo
/// số đã quẹt, cộng suất khách, rồi khóa (trigger DB chặn sửa thêm, MSG22).
/// </summary>
public sealed class MealAutoCloseJob : DailyScheduledJob
{
    public MealAutoCloseJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<MealAutoCloseJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.MealCutoffTime;

    protected override TimeOnly DefaultScheduleTime => new(8, 30);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IMealService>().RunAutoCloseJobAsync(today, ct);
}
