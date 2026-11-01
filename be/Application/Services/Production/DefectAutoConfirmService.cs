using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Phiếu hàng hỏng quá <see cref="DefectObjectionWindow.Hours"/> giờ kể từ lúc ghi mà công nhân không phản đối thì tự chuyển
/// sang CONFIRMED (hết hạn phản đối, phiếu được chốt). Chỉ đụng phiếu còn RECORDED và chưa có phản đối; phiếu đã bị phản đối
/// vẫn chờ Quản đốc quyết định. Thời điểm chuyển nằm ở UpdatedAt.
/// </summary>
public sealed class DefectAutoConfirmService : IDefectAutoConfirmService
{
    private const int BatchSize = 500;

    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<DefectAutoConfirmService> _logger;

    public DefectAutoConfirmService(IUnitOfWork uow, IClock clock, ILogger<DefectAutoConfirmService> logger)
    {
        _uow = uow; _clock = clock; _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = _clock.Now;
        var cutoff = now.AddHours(-DefectObjectionWindow.Hours);
        var due = await _uow.Repository<DefectRecord>().Query()
            .Where(d => d.Status == DefectStatuses.Recorded && d.WorkerObjection == null && d.RecordedAt < cutoff)
            .OrderBy(d => d.DefectRecordId)
            .Take(BatchSize)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        foreach (var defect in due)
        {
            defect.Status = DefectStatuses.Confirmed;
            defect.UpdatedAt = now;
        }
        await _uow.SaveChangesAsync();
        _logger.LogInformation("Đã tự chốt {Count} phiếu hàng hỏng quá {Hours} giờ không bị phản đối", due.Count, DefectObjectionWindow.Hours);
        return due.Count;
    }
}
