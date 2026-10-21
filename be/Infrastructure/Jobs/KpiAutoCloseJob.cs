using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// KPI tổ may (Minh): tự động đóng chỉ tiêu KPI khi đã qua EndDate — tính Result so với
/// vw_KpiDailyResult rồi chuyển Status sang CLOSED. Giờ chạy lấy từ SystemSetting
/// KPI_AUTO_CLOSE_TIME (DEV-MI-16-2: khóa này chưa được seed trong DB, dùng giá trị mặc định
/// bên dưới cho tới khi có dòng seed).
/// </summary>
public sealed class KpiAutoCloseJob : DailyScheduledJob
{
    public KpiAutoCloseJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger<KpiAutoCloseJob> logger)
        : base(scopeFactory, clock, logger)
    {
    }

    protected override string ScheduleSettingKey => SettingKeys.KpiAutoCloseTime;

    /// <summary>00:05 — chạy sớm đầu ngày để đóng xong kỳ "hết hạn hôm qua" trước khi ai mở màn KPI buổi sáng.</summary>
    protected override TimeOnly DefaultScheduleTime => new(0, 5);

    protected override Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct) =>
        scopedProvider.GetRequiredService<IKpiService>().CloseExpiredAsync(today, ct);
}
