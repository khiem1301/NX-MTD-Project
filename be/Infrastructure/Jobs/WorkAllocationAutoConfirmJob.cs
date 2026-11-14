using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Jobs;

/// <summary>
/// DEV-ALL-101: mỗi 5 phút tự xác nhận phiếu giao việc công nhân để quá 60 phút không xác nhận (xem WorkAllocationAutoConfirmService).
/// Chạy theo chu kỳ ngắn vì hạn tính theo từng phiếu; phiếu được xác nhận trễ nhất khoảng một chu kỳ sau khi hết hạn.
/// </summary>
public sealed class WorkAllocationAutoConfirmJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkAllocationAutoConfirmJob> _logger;

    public WorkAllocationAutoConfirmJob(IServiceScopeFactory scopeFactory, ILogger<WorkAllocationAutoConfirmJob> logger)
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
                await scope.ServiceProvider.GetRequiredService<IWorkAllocationAutoConfirmService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi tự xác nhận phiếu giao việc quá hạn chờ công nhân");
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
