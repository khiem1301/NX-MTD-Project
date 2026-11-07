using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Đơn hủy / dừng thì các việc đang chờ của đơn không còn dùng được: hủy phiếu xuất chưa xuất kho, lệnh cắt chưa
/// làm gì và chỉ tiêu KPI đang chạy. Việc đã có số liệu thật (lệnh cắt đã cấp vải / cắt, phiếu đã xuất) giữ nguyên.
/// Chỉ thêm thay đổi vào DbContext, người gọi lưu cùng giao dịch với đổi trạng thái đơn.
/// </summary>
internal sealed class OrderCloseCascade
{
    private const string WorkOrderLink = "/cutting/work-orders";
    private const string KpiLink = "/kpi/targets";
    private const int MaxNoteLength = 1000;

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;

    public OrderCloseCascade(IUnitOfWork uow, IAuditLogService audit, INotificationService notifier)
    {
        _uow = uow; _audit = audit; _notifier = notifier;
    }

    public async Task RunAsync(CustomerOrder order, string orderStatus, DateTime now, CancellationToken ct)
    {
        var reason = orderStatus == OrderStatuses.Cancelled ? Messages.OrderCloseReasonCancelled : Messages.OrderCloseReasonStopped;
        await CancelPendingDispatchesAsync(order, reason, ct);
        await CloseWorkOrdersAsync(order, reason, now, ct);
        await CancelActiveKpiAsync(order, reason, ct);
    }

    /// <summary>Phiếu xuất chưa xuất kho: Thủ kho không xuất được nữa nên hủy để nhả số giữ chỗ. Phiếu đã xuất giữ nguyên.</summary>
    private async Task CancelPendingDispatchesAsync(CustomerOrder order, string reason, CancellationToken ct)
    {
        var pending = await _uow.Repository<DispatchNote>().Query()
            .Where(d => d.OrderId == order.OrderId && d.Status == DispatchStatuses.Pending).ToListAsync(ct);
        if (pending.Count == 0) return;
        foreach (var note in pending)
        {
            note.Status = DispatchStatuses.Cancelled;
            _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Update,
                new { Status = DispatchStatuses.Pending }, new { note.Status }, reason));
        }
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            Messages.NotifyDispatchAutoCancelTitle,
            string.Format(Messages.NotifyDispatchAutoCancelBody, string.Join(", ", pending.Select(p => p.DispatchCode)), order.OrderCode),
            null), ct);
    }

    /// <summary>
    /// Lệnh cắt của đơn: lệnh chưa làm gì (OPEN, chưa có bàn cắt, chưa có phiếu cấp vải trừ phiếu bị từ chối) thì hủy; lệnh đã
    /// có việc thì đóng (Hoàn thành, ghi chú lý do) nếu đủ điều kiện như Quản đốc đóng tay, tức hết phiếu bàn giao / trả vải
    /// chờ kho và phần đã cắt đã vào kho. Chưa đủ điều kiện thì để nguyên và báo Quản đốc đóng sau.
    /// </summary>
    private async Task CloseWorkOrdersAsync(CustomerOrder order, string reason, DateTime now, CancellationToken ct)
    {
        var workOrders = await _uow.Repository<WorkOrder>().Query()
            .Include(w => w.WorkOrderLines).Include(w => w.CuttingLays).Include(w => w.MaterialIssues)
            .Where(w => w.OrderId == order.OrderId && (w.Status == WorkOrderStatuses.Open || w.Status == WorkOrderStatuses.Cutting))
            .ToListAsync(ct);
        var notClosed = new List<string>();
        foreach (var wo in workOrders)
        {
            var from = wo.Status;
            if (from == WorkOrderStatuses.Open && !wo.CuttingLays.Any() && !wo.MaterialIssues.Any(i => i.Status != IssueStatuses.Rejected))
                wo.Status = WorkOrderStatuses.Cancelled;
            else if ((await WorkOrderFinishCheck.RunAsync(_uow, wo, reason, ct)).Failure is null)
            {
                wo.Status = WorkOrderStatuses.Completed;
                wo.CompletedAt = now;
                var note = string.IsNullOrEmpty(wo.Note) ? reason : $"{wo.Note}\n{reason}";
                if (note.Length <= MaxNoteLength) wo.Note = note;
            }
            else
            {
                notClosed.Add(wo.WorkOrderCode);
                continue;
            }
            _audit.Add(new AuditEntry(nameof(WorkOrder), wo.WorkOrderId, AuditActions.Update,
                new { Status = from }, new { wo.Status }, reason));
            await _notifier.NotifyTeamLeadersAsync(wo.CuttingTeamId, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyWorkOrderAutoClosedTitle, string.Format(Messages.NotifyWorkOrderAutoClosedBody, wo.WorkOrderCode, order.OrderCode),
                WorkOrderLink), ct);
        }
        if (notClosed.Count > 0)
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyWorkOrderStillOpenTitle,
                string.Format(Messages.NotifyWorkOrderStillOpenBody, string.Join(", ", notClosed), order.OrderCode), WorkOrderLink), ct);
    }

    /// <summary>Chỉ tiêu KPI đang chạy của đơn: hủy. Chỉ tiêu đã đóng kỳ (có kết quả) giữ nguyên.</summary>
    private async Task CancelActiveKpiAsync(CustomerOrder order, string reason, CancellationToken ct)
    {
        var active = await _uow.Repository<KpiTarget>().Query()
            .Where(k => k.OrderId == order.OrderId && k.Status == KpiStatuses.Active).ToListAsync(ct);
        foreach (var target in active)
        {
            target.Status = KpiStatuses.Cancelled;
            _audit.Add(new AuditEntry(nameof(KpiTarget), target.KpiTargetId, AuditActions.Update,
                new { Status = KpiStatuses.Active }, new { target.Status }, reason));
            await _notifier.NotifyTeamLeadersAsync(target.SewingTeamId, new NotificationMessage(NotificationSources.Production,
                "Chỉ tiêu KPI bị hủy", string.Format(Messages.NotifyKpiAutoCancelBody, target.TargetFinishedQty, order.OrderCode),
                KpiLink), ct);
        }
    }
}
