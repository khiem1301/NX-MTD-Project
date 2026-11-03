using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// Mỗi 15 phút thu hồi phần việc chưa làm hết của các ngày đã qua hạn sửa sản lượng mà tổ trưởng chưa thu hồi
/// (xem DailyWorkAllocationService.AutoRetrieveAsync). Phiếu ghi lý do, ConfirmMode = SYSTEM.
/// </summary>
public sealed class WorkAllocationAutoRetrieveJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkAllocationAutoRetrieveJob> _logger;

    public WorkAllocationAutoRetrieveJob(IServiceScopeFactory scopeFactory, ILogger<WorkAllocationAutoRetrieveJob> logger)
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
                using var scope = _scopeFactory.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<IDailyWorkAllocationService>().AutoRetrieveAsync(stoppingToken);
                if (count > 0) _logger.LogInformation("Đã tự thu hồi {Count} phần việc chưa làm hết cuối ngày", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi tự thu hồi việc chưa làm hết cuối ngày");
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
}
