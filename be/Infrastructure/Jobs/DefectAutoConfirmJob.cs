using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// Mỗi 15 phút chuyển phiếu hàng hỏng quá 48 giờ không bị phản đối sang CONFIRMED (xem DefectAutoConfirmService).
/// Chạy theo chu kỳ ngắn thay vì một giờ cố định trong ngày vì hạn 48 giờ tính theo từng phiếu.
/// </summary>
public sealed class DefectAutoConfirmJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DefectAutoConfirmJob> _logger;

    public DefectAutoConfirmJob(IServiceScopeFactory scopeFactory, ILogger<DefectAutoConfirmJob> logger)
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
                await scope.ServiceProvider.GetRequiredService<IDefectAutoConfirmService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi tự chốt phiếu hàng hỏng quá hạn phản đối");
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
