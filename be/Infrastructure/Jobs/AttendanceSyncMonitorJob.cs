using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// Theo dõi đồng bộ chấm công Ronald Jack Pro không bị treo quá lâu (MSG23). JOB-01 (đọc RJData.mdb
/// mỗi 10 phút) chạy ở agent ngoài; job này đọc lại <see cref="IAttendanceService.GetHealthAsync"/>
/// theo chu kỳ và gửi thông báo hộp thư cho Admin khi quá hạn (một lần mỗi đợt).
/// </summary>
public sealed class AttendanceSyncMonitorJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AttendanceSyncMonitorJob> _logger;

    public AttendanceSyncMonitorJob(IServiceScopeFactory scopeFactory, ILogger<AttendanceSyncMonitorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra tình trạng đồng bộ chấm công");
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

    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAttendanceService>().AlertSyncOverdueAsync(ct);
    }
}
