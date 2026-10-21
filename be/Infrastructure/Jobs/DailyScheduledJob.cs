using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// Khung dùng chung cho job chạy đúng một lần mỗi ngày vào một giờ lấy từ SystemSetting
/// (JOB-02 08:15, JOB-03 08:30). Dò theo chu kỳ ngắn thay vì hẹn giờ tuyệt đối để không cần job
/// nền kiểu Hangfire — đúng stack hiện tại (BackgroundService thuần theo Coding Rules).
/// </summary>
public abstract class DailyScheduledJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private DateOnly _lastRunDate;

    protected DailyScheduledJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Khóa SystemSetting chứa giờ chạy, vd SettingKeys.MealReminderTime.</summary>
    protected abstract string ScheduleSettingKey { get; }

    protected abstract TimeOnly DefaultScheduleTime { get; }

    /// <summary>Việc cần làm lúc đến giờ; nhận provider đã mở scope để lấy service scoped (DbContext...).</summary>
    protected abstract Task RunAsync(IServiceProvider scopedProvider, DateOnly today, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi chạy job {Job}", GetType().Name);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // dừng ứng dụng
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        if (_lastRunDate == today) return;

        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingReader>();
        var scheduleTime = await settings.GetTimeAsync(ScheduleSettingKey, DefaultScheduleTime, ct);
        if (TimeOnly.FromDateTime(_clock.Now) < scheduleTime) return;

        await RunAsync(scope.ServiceProvider, today, ct);
        _lastRunDate = today;
    }
}
