using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-101: phiếu giao việc (HANDOVER) chờ công nhân xác nhận quá <see cref="AllocationAutoConfirmWindow.Minutes"/> phút kể từ
/// lúc ghi thì hệ thống tự xác nhận đủ số giao (ConfirmMode = SYSTEM), để phần giao vào trần sản lượng. Phiếu thu hồi đang chờ không
/// tự xác nhận. Khóa theo tổ × dòng đơn như khi công nhân / tổ trưởng xác nhận, nên không ghi đè một lần xác nhận đang diễn ra.
/// </summary>
public sealed class WorkAllocationAutoConfirmService : IWorkAllocationAutoConfirmService
{
    private const int BatchSize = 500;

    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IAppLock _locks;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly ILogger<WorkAllocationAutoConfirmService> _logger;

    public WorkAllocationAutoConfirmService(IUnitOfWork uow, IClock clock, IAppLock locks, IAuditLogService audit,
        INotificationService notifier, ILogger<WorkAllocationAutoConfirmService> logger)
    {
        _uow = uow; _clock = clock; _locks = locks; _audit = audit; _notifier = notifier; _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var result = await _uow.InTransactionAsync(() => ConfirmDueAsync(ct));
        if (result.Data > 0)
            _logger.LogInformation("Đã tự xác nhận {Count} phiếu giao việc quá {Minutes} phút công nhân không xác nhận",
                result.Data, AllocationAutoConfirmWindow.Minutes);
        return result.Data;
    }

    private async Task<Result<int>> ConfirmDueAsync(CancellationToken ct)
    {
        var now = _clock.Now;
        var cutoff = now.AddMinutes(-AllocationAutoConfirmWindow.Minutes);
        var due = await Due(cutoff).OrderBy(a => a.WorkAllocationId).Take(BatchSize).ToListAsync(ct);
        if (due.Count == 0) return Result<int>.Success(0);

        // Cùng thứ tự khóa với các luồng xác nhận khác; đọc lại sau khi khóa vì phiếu có thể vừa được xác nhận / từ chối.
        foreach (var (teamId, lineId) in due.Select(a => (a.SewingTeamId, a.OrderLineId)).Distinct().OrderBy(k => k.SewingTeamId).ThenBy(k => k.OrderLineId))
            await _locks.AcquireAsync($"work-allocation:{teamId}:{lineId}", ct);
        var ids = due.Select(a => a.WorkAllocationId).ToList();
        var rows = await Due(cutoff).Where(a => ids.Contains(a.WorkAllocationId)).ToListAsync(ct);
        if (rows.Count == 0) return Result<int>.Success(0);

        var reason = string.Format(WarehouseMessages.AllocationAutoConfirmedReason, AllocationAutoConfirmWindow.Minutes);
        foreach (var row in rows)
        {
            row.ConfirmedQty = row.Quantity;
            row.ConfirmMode = ConfirmModes.System;
            row.Status = HandoverStatuses.Confirmed;
            row.ConfirmedAt = now;
            _audit.Add(new AuditEntry(nameof(WorkAllocation), row.WorkAllocationId, AuditActions.Update,
                new { Status = HandoverStatuses.Pending }, new { row.Status, row.ConfirmedQty, row.ConfirmMode }, reason));
        }
        NotifyWorkers(rows);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(rows.Count);
    }

    private IQueryable<WorkAllocation> Due(DateTime cutoff) => _uow.Repository<WorkAllocation>().Query()
        .Where(a => a.Status == HandoverStatuses.Pending && a.MovementType == PieceMovementTypes.Handover && a.CreatedAt < cutoff);

    private void NotifyWorkers(IEnumerable<WorkAllocation> rows)
    {
        foreach (var group in rows.GroupBy(r => r.WorkerId))
            _notifier.NotifyEmployees(new[] { group.Key }, new NotificationMessage(NotificationSources.Production,
                WarehouseMessages.AllocationAutoConfirmedTitle,
                string.Format(WarehouseMessages.AllocationAutoConfirmedBody, group.Count(), AllocationAutoConfirmWindow.Minutes), null));
    }
}
