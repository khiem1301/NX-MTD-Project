using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC21 (KH-16): dashboard tiến độ đơn hàng cho Admin / Quản đốc — cắt, vào kho BTP, cấp tổ may, hoàn thiện, nhập kho thành
/// phẩm, đã giao, Rework / Scrap theo đơn và theo dòng đơn (<c>vw_OrderLineProgress</c>) kèm cờ nguy cơ trễ.
/// JOB-04 (KH-23) đặt cờ <c>IsDelayRisk</c>; JOB-05 xóa thông báo quá hạn lưu.
/// </summary>
public sealed class OrderProgressService : IOrderProgressService
{
    private const int DefaultAvgDays = 7;
    private const int DefaultRetentionDays = 90;

    private readonly IUnitOfWork _uow;
    private readonly IOutputQueryService _output;
    private readonly ISystemSettingReader _settings;
    private readonly INotificationService _notifier;
    private readonly ILogger<OrderProgressService> _logger;

    public OrderProgressService(IUnitOfWork uow, IOutputQueryService output, ISystemSettingReader settings,
        INotificationService notifier, ILogger<OrderProgressService> logger)
    {
        _uow = uow; _output = output; _settings = settings; _notifier = notifier; _logger = logger;
    }

    public async Task<Result<IReadOnlyList<OrderProgressDto>>> GetOverviewAsync(bool includeClosed, CancellationToken ct)
    {
        // Mảng (không phải IReadOnlySet) để EF dịch được thành IN (...).
        var running = OrderStatuses.Running.ToArray();
        var orders = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => includeClosed || running.Contains(o.Status))
            .Select(o => new { o.OrderId, o.OrderCode, o.Customer.CustomerName, o.DueDate, o.Status, o.MaterialStatus, o.IsDelayRisk, o.DelayRiskSince })
            .ToListAsync(ct);
        var ids = orders.Select(o => o.OrderId).ToList();
        var lines = await _uow.Repository<VwOrderLineProgress>().Query().AsNoTracking().Where(v => ids.Contains(v.OrderId)).ToListAsync(ct);
        var byOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var result = orders.OrderBy(o => o.DueDate).ThenBy(o => o.OrderCode).Select(o =>
        {
            var l = byOrder.GetValueOrDefault(o.OrderId) ?? new List<VwOrderLineProgress>();
            var finished = l.Sum(x => x.FinishedQty);
            var rework = l.Sum(x => x.ReworkQty);
            var scrap = l.Sum(x => x.ScrapQty);
            var defectPct = finished + rework + scrap == 0 ? 0m : Math.Round(100m * (rework + scrap) / (finished + rework + scrap), 2);
            return new OrderProgressDto(o.OrderId, o.OrderCode, o.CustomerName, o.DueDate, o.DueDate.DayNumber - today.DayNumber, CodeText.ToPascal(o.Status),
                CodeText.ToPascal(o.MaterialStatus), o.IsDelayRisk, o.DelayRiskSince, l.Sum(x => x.OrderedQty), l.Sum(x => x.CutQty),
                l.Sum(x => x.CutHandedToWarehouseQty), l.Sum(x => x.IssuedToSewingQty), finished, l.Sum(x => x.FinishedReceivedQty),
                l.Sum(x => x.DeliveredQty), rework, scrap, defectPct);
        }).ToList();
        return Result<IReadOnlyList<OrderProgressDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<OrderLineProgressDto>>> GetLinesAsync(int orderId, CancellationToken ct)
    {
        if (!await _uow.Repository<CustomerOrder>().Query().AnyAsync(o => o.OrderId == orderId, ct))
            return WarehouseFailures.NotFound<IReadOnlyList<OrderLineProgressDto>>();
        return Result<IReadOnlyList<OrderLineProgressDto>>.Success(await _output.GetOrderLineProgressAsync(orderId, ct));
    }

    // ---------- JOB-04: nguy cơ trễ đơn ----------

    /// <summary>
    /// Đơn đang sản xuất có nguy cơ trễ khi SL còn lại ÷ trung bình hoàn thiện N ngày gần nhất &gt; số ngày còn lại.
    /// Chưa hoàn thiện được cái nào trong N ngày thì chỉ coi là nguy cơ khi đã quá hạn giao (DEV-KH-23-1).
    /// Báo Admin + Quản đốc vào ngày đầu tiên đơn bị gắn cờ; hết nguy cơ thì gỡ cờ.
    /// </summary>
    public async Task<int> RunDelayCheckAsync(DateOnly today, CancellationToken ct)
    {
        var avgDays = Math.Max(1, await _settings.GetIntAsync(SettingKeys.DelayAvgDays, DefaultAvgDays, ct));
        var from = today.AddDays(-avgDays);
        var orders = await _uow.Repository<CustomerOrder>().Query().Where(o => o.Status == OrderStatuses.InProduction).ToListAsync(ct);
        if (orders.Count == 0) return 0;

        var ids = orders.Select(o => o.OrderId).ToList();
        var ordered = await _uow.Repository<OrderLine>().Query().Where(l => ids.Contains(l.OrderId))
            .GroupBy(l => l.OrderId).Select(g => new { g.Key, Qty = g.Sum(l => l.OrderedQty) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var finished = await _uow.Repository<QcBatch>().Query().Where(q => ids.Contains(q.OrderLine.OrderId))
            .GroupBy(q => q.OrderLine.OrderId).Select(g => new { g.Key, Qty = g.Sum(q => q.FinishedQty) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var recent = await _uow.Repository<QcBatch>().Query().Where(q => ids.Contains(q.OrderLine.OrderId) && q.WorkDate > from && q.WorkDate <= today)
            .GroupBy(q => q.OrderLine.OrderId).Select(g => new { g.Key, Qty = g.Sum(q => q.FinishedQty) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var newlyFlagged = new List<CustomerOrder>();
        foreach (var o in orders)
        {
            var risk = IsDelayRisk(ordered.GetValueOrDefault(o.OrderId), finished.GetValueOrDefault(o.OrderId),
                recent.GetValueOrDefault(o.OrderId), avgDays, o.DueDate.DayNumber - today.DayNumber);
            if (risk && !o.IsDelayRisk) { o.IsDelayRisk = true; o.DelayRiskSince = today; newlyFlagged.Add(o); }
            else if (!risk && o.IsDelayRisk) { o.IsDelayRisk = false; o.DelayRiskSince = null; }
        }

        foreach (var o in newlyFlagged)
        {
            var message = new NotificationMessage(NotificationSources.System, "Đơn hàng có nguy cơ trễ hạn",
                $"Đơn {o.OrderCode} (hạn giao {o.DueDate:dd/MM/yyyy}) có nguy cơ không kịp tiến độ.", null, nameof(CustomerOrder), o.OrderId);
            await _notifier.NotifyRoleAsync(RoleCodes.Admin, message, ct);
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, message, ct);
        }
        await _uow.SaveChangesAsync();
        _logger.LogInformation("JOB-04: kiểm tra {Count} đơn, {New} đơn mới bị gắn cờ nguy cơ trễ", orders.Count, newlyFlagged.Count);
        return newlyFlagged.Count;
    }

    /// <summary>Công thức nguy cơ trễ (tách ra để test).</summary>
    public static bool IsDelayRisk(int orderedQty, int finishedQty, int finishedInWindow, int avgDays, int daysLeft)
    {
        var remaining = orderedQty - finishedQty;
        if (remaining <= 0) return false;
        if (finishedInWindow <= 0) return daysLeft < 0;
        var avgPerDay = (decimal)finishedInWindow / avgDays;
        return daysLeft <= 0 || remaining / avgPerDay > daysLeft;
    }

    // ---------- JOB-05: xóa thông báo cũ ----------

    public async Task<int> PurgeOldNotificationsAsync(DateOnly today, CancellationToken ct)
    {
        var days = Math.Max(1, await _settings.GetIntAsync(SettingKeys.NotificationRetentionDays, DefaultRetentionDays, ct));
        var cutoff = today.AddDays(-days).ToDateTime(TimeOnly.MinValue);
        var old = await _uow.Repository<Notification>().Query().Where(n => n.CreatedAt < cutoff).ToListAsync(ct);
        if (old.Count == 0) return 0;
        _uow.Repository<Notification>().RemoveRange(old);
        await _uow.SaveChangesAsync();
        _logger.LogInformation("JOB-05: đã xóa {Count} thông báo quá {Days} ngày", old.Count, days);
        return old.Count;
    }
}
