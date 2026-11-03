using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>KI-10: đọc sản lượng / Scrap / QC từ các view của DB cho luồng lương, KPI và tiến độ. Không ghi dữ liệu.</summary>
public sealed class OutputQueryService : IOutputQueryService
{
    private readonly IUnitOfWork _uow;

    public OutputQueryService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<MyEstimatedPayDayDto>> GetWorkerDailyPayAsync(
        int workerId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await _uow.Repository<VwWorkerDailyEstimatedPay>().Query().AsNoTracking()
            .Where(v => v.WorkerId == workerId && v.WorkDate >= from && v.WorkDate <= to)
            .OrderBy(v => v.WorkDate)
            .ToListAsync(ct);
        return rows.Select(v => new MyEstimatedPayDayDto(
            v.WorkDate, v.IsSunday ?? false, v.OutputQty, v.ScrapQty, v.OutputAmount, v.SundayBonus ?? 0m,
            v.ScrapAmount, v.EstimatedPay ?? 0m)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, int>> GetKpiFinishedTotalsAsync(
        IReadOnlyCollection<int> kpiTargetIds, CancellationToken ct)
    {
        if (kpiTargetIds.Count == 0) return new Dictionary<int, int>();
        var ids = kpiTargetIds.ToList();   // EF chỉ dịch được Contains trên List / mảng
        var totals = await _uow.Repository<VwKpiDailyResult>().Query().AsNoTracking()
            .Where(v => ids.Contains(v.KpiTargetId))
            .GroupBy(v => v.KpiTargetId)
            .Select(g => new { KpiTargetId = g.Key, Total = g.Sum(v => v.FinishedQty ?? 0) })
            .ToListAsync(ct);
        return totals.ToDictionary(x => x.KpiTargetId, x => x.Total);
    }

    public async Task<IReadOnlyDictionary<(int KpiTargetId, int OrderLineId), int>> GetKpiLineFinishedAsync(
        IReadOnlyCollection<int> kpiTargetIds, CancellationToken ct)
    {
        if (kpiTargetIds.Count == 0) return new Dictionary<(int, int), int>();
        var ids = kpiTargetIds.ToList();
        // Cùng cách đếm với vw_KpiDailyResult: QC của tổ, đúng đơn + mã hàng, trong kỳ của chỉ tiêu.
        var rows = await (
                from k in _uow.Repository<KpiTarget>().Query().AsNoTracking()
                where ids.Contains(k.KpiTargetId)
                join b in _uow.Repository<QcBatch>().Query().AsNoTracking() on k.SewingTeamId equals b.SewingTeamId
                join l in _uow.Repository<OrderLine>().Query().AsNoTracking() on b.OrderLineId equals l.OrderLineId
                where l.OrderId == k.OrderId && l.ProductId == k.ProductId && b.WorkDate >= k.StartDate && b.WorkDate <= k.EndDate
                group b by new { k.KpiTargetId, b.OrderLineId } into g
                select new { g.Key.KpiTargetId, g.Key.OrderLineId, Qty = g.Sum(b => b.FinishedQty) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => (r.KpiTargetId, r.OrderLineId), r => r.Qty);
    }

    public async Task<IReadOnlyList<KpiDailyResultDto>> GetKpiDailyResultsAsync(int kpiTargetId, CancellationToken ct) =>
        await _uow.Repository<VwKpiDailyResult>().Query().AsNoTracking()
            .Where(v => v.KpiTargetId == kpiTargetId)
            .OrderBy(v => v.WorkDate)
            .Select(v => new KpiDailyResultDto(v.KpiTargetId, v.SewingTeamId, v.OrderId, v.ProductId, v.WorkDate,
                v.DailyTarget, v.FinishedQty, v.DefectQty, v.DefectRatePct, v.PercentAchieved))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderLineProgressDto>> GetOrderLineProgressAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<VwOrderLineProgress>().Query().AsNoTracking()
            .Where(v => v.OrderId == orderId)
            .OrderBy(v => v.ProductCode).ThenBy(v => v.Color).ThenBy(v => v.SizeCode)
            .Select(v => new OrderLineProgressDto(v.OrderLineId, v.OrderId, v.OrderCode, v.ProductId, v.ProductCode,
                v.Color, v.SizeCode, v.OrderedQty, v.CutQty, v.CutHandedToWarehouseQty, v.IssuedToSewingQty,
                v.FinishedQty, v.FinishedReceivedQty, v.DeliveredQty, v.ReworkQty, v.ScrapQty, v.DefectRatePct))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, int>> GetQcFinishedByOrderLineAsync(
        IReadOnlyCollection<int> orderLineIds, CancellationToken ct)
    {
        if (orderLineIds.Count == 0) return new Dictionary<int, int>();
        var ids = orderLineIds.ToList();
        var totals = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(b => ids.Contains(b.OrderLineId))
            .GroupBy(b => b.OrderLineId)
            .Select(g => new { OrderLineId = g.Key, Total = g.Sum(b => b.FinishedQty) })
            .ToListAsync(ct);
        return totals.ToDictionary(x => x.OrderLineId, x => x.Total);
    }
}
