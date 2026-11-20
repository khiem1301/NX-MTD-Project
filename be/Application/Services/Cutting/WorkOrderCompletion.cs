using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Lệnh cắt tự hoàn thành khi kho đã xác nhận nhận đủ BTP theo kế hoạch của mọi dòng (DEV-ALL-52). Tổ cắt không còn nút
/// "Cắt xong"; cắt thiếu thì Quản đốc đóng lệnh (WorkOrderService.CloseShortAsync).
/// </summary>
public sealed class WorkOrderCompletion
{
    private const string WorkOrderLink = "/cutting/work-orders";

    private readonly IUnitOfWork _uow;
    private readonly FabricPlanner _planner;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;

    public WorkOrderCompletion(IUnitOfWork uow, FabricPlanner planner, INotificationService notifier, IClock clock)
    {
        _uow = uow; _planner = planner; _notifier = notifier; _clock = clock;
    }

    /// <summary>
    /// Gọi sau khi phiếu bàn giao đã lưu CONFIRMED. Mọi dòng (màu × size, kể cả chi tiết cắt lẻ) đã nhận đủ số kế hoạch thì
    /// lệnh chuyển Hoàn thành và báo Quản đốc kèm vải đã nhận, đã trả, thực dùng. Trả về true khi vừa hoàn thành.
    /// </summary>
    public async Task<bool> CompleteIfFullyReceivedAsync(int workOrderId, CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().Include(w => w.WorkOrderLines).Include(w => w.Order)
            .FirstAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo.Status is not (WorkOrderStatuses.Open or WorkOrderStatuses.Cutting) || wo.WorkOrderLines.Count == 0) return false;

        var received = await ReceivedAsync(workOrderId, ct);
        if (wo.WorkOrderLines.Any(l => received.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId)) < l.PlannedQty))
            return false;

        wo.Status = WorkOrderStatuses.Completed;
        wo.CompletedAt = _clock.Now;
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Production, "Lệnh cắt đã hoàn thành",
            $"Kho đã nhận đủ BTP theo kế hoạch của lệnh {wo.WorkOrderCode} (đơn {wo.Order.OrderCode}), lệnh cắt tự hoàn thành.{await FabricUsageAsync(workOrderId, ct)}",
            WorkOrderLink), ct);
        return true;
    }

    /// <summary>Số BTP kho đã xác nhận nhận theo (dòng đơn, chi tiết cắt lẻ).</summary>
    public async Task<Dictionary<(int OrderLineId, int? ProductComponentMaterialId), int>> ReceivedAsync(int workOrderId, CancellationToken ct) =>
        (await _uow.Repository<CutHandoverLine>().Query().AsNoTracking()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId && l.CutHandover.Status == HandoverStatuses.Confirmed)
            .Select(l => new { l.OrderLineId, l.ProductComponentMaterialId, Qty = l.ConfirmedQty ?? 0 }).ToListAsync(ct))
        .GroupBy(l => (l.OrderLineId, l.ProductComponentMaterialId)).ToDictionary(g => g.Key, g => g.Sum(l => l.Qty));

    /// <summary>Vải đã nhận, đã trả, thực dùng của lệnh (không so định mức), dạng " Vải: V01: nhận 120, trả 15, thực dùng 105 m."</summary>
    private async Task<string> FabricUsageAsync(int workOrderId, CancellationToken ct)
    {
        var (received, returned) = await _planner.GetUsageAsync(workOrderId, ct);
        var ids = received.Where(r => r.Value > 0).Select(r => r.Key).ToList();
        if (ids.Count == 0) return string.Empty;
        var materials = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => ids.Contains(m.MaterialId))
            .Select(m => new { m.MaterialId, m.MaterialCode, m.Unit }).ToListAsync(ct);
        var rows = materials.OrderBy(m => m.MaterialCode).Select(m =>
        {
            var got = received[m.MaterialId];
            var back = returned.GetValueOrDefault(m.MaterialId);
            return $"{m.MaterialCode}: nhận {Format(got)}, trả {Format(back)}, thực dùng {Format(Math.Max(0, got - back))} {m.Unit}";
        });
        return $" Vải: {string.Join("; ", rows)}.";
    }

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));
}
