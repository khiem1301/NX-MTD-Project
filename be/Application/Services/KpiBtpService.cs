using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// So sánh BTP – KPI theo ngày. BTP cắt vào kho = giao dịch kho BTP loại CUT_HANDOVER của các dòng đơn thuộc (đơn, mã hàng) của KPI;
/// thực tế = hàng hoàn thiện QA/QC của tổ (cùng thước đo KPI, vw_KpiDailyResult). BTP cắt tính chung cho (đơn, mã hàng), không tách theo tổ.
/// </summary>
public sealed class KpiBtpService : IKpiBtpService
{
    private readonly IUnitOfWork _uow;
    private readonly IOutputQueryService _output;
    private readonly IClock _clock;

    public KpiBtpService(IUnitOfWork uow, IOutputQueryService output, IClock clock)
    {
        _uow = uow; _output = output; _clock = clock;
    }

    private IQueryable<KpiTarget> Targets => _uow.Repository<KpiTarget>().Query();

    public async Task<Result<IReadOnlyList<KpiBtpOptionDto>>> GetOptionsAsync(int? teamId, CancellationToken ct)
    {
        var rows = await Targets.AsNoTracking()
            .Where(k => k.Status != KpiStatuses.Cancelled)
            .Where(k => teamId == null || k.SewingTeamId == teamId)
            .OrderByDescending(k => k.StartDate).ThenByDescending(k => k.KpiTargetId)
            .Select(k => new
            {
                k.KpiTargetId, k.Order.OrderCode, k.Product.ProductCode, k.Product.ProductName, TeamName = k.SewingTeam.TeamName,
                k.StartDate, k.EndDate, k.Status,
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<KpiBtpOptionDto>>.Success(rows
            .Select(r => new KpiBtpOptionDto(r.KpiTargetId, r.OrderCode, r.ProductCode, r.ProductName, r.TeamName, r.StartDate, r.EndDate,
                CodeText.ToPascal(r.Status)))
            .ToList());
    }

    public async Task<Result<KpiBtpComparisonDto>> GetAsync(int kpiTargetId, int? teamId, CancellationToken ct)
    {
        var k = await Targets.AsNoTracking()
            .Where(x => x.KpiTargetId == kpiTargetId && (teamId == null || x.SewingTeamId == teamId))
            .Select(x => new
            {
                x.KpiTargetId, x.OrderId, x.ProductId, x.SewingTeamId, x.StartDate, x.EndDate, x.TargetFinishedQty, x.WorkingDays, x.Status,
                x.Order.OrderCode, x.Product.ProductCode, x.Product.ProductName, TeamName = x.SewingTeam.TeamName,
            })
            .FirstOrDefaultAsync(ct);
        if (k is null) return Fail.NotFound<KpiBtpComparisonDto>();

        var plan = await _uow.Repository<KpiDailyTarget>().Query().AsNoTracking()
            .Where(d => d.KpiTargetId == kpiTargetId)
            .Select(d => new { d.WorkDate, d.TargetQty })
            .ToListAsync(ct);
        // KPI cũ chưa có chỉ tiêu từng ngày: chia đều như vw_KpiDailyResult.
        var evenTarget = (decimal)k.TargetFinishedQty / k.WorkingDays;
        var planByDate = plan.ToDictionary(p => p.WorkDate, p => (decimal)p.TargetQty);

        var cuts = await _uow.Repository<StockTransaction>().Query().AsNoTracking()
            .Where(t => t.WarehouseType == WarehouseTypes.SemiFinished && t.SourceDocType == StockDocTypes.CutHandover
                        && t.OrderLine!.OrderId == k.OrderId && t.OrderLine.ProductId == k.ProductId && t.TransactionDate <= k.EndDate)
            .Select(t => new { t.TransactionDate, t.QtyChange })
            .ToListAsync(ct);
        var openingCut = (int)cuts.Where(c => c.TransactionDate < k.StartDate).Sum(c => c.QtyChange);
        var cutByDate = cuts.Where(c => c.TransactionDate >= k.StartDate)
            .GroupBy(c => c.TransactionDate).ToDictionary(g => g.Key, g => (int)g.Sum(c => c.QtyChange));

        var openingActual = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(b => b.SewingTeamId == k.SewingTeamId && b.OrderLine.OrderId == k.OrderId && b.OrderLine.ProductId == k.ProductId
                        && b.WorkDate < k.StartDate)
            .SumAsync(b => (int?)b.FinishedQty, ct) ?? 0;
        var daily = await _output.GetKpiDailyResultsAsync(kpiTargetId, ct);
        var actualByDate = daily.GroupBy(d => d.WorkDate).ToDictionary(g => g.Key, g => g.Sum(d => d.FinishedQty ?? 0));

        var dates = planByDate.Keys.Concat(cutByDate.Keys).Concat(actualByDate.Keys)
            .Where(d => d >= k.StartDate && d <= k.EndDate).Distinct().OrderBy(d => d).ToList();

        var today = _clock.Today;
        var openingBtp = openingCut - openingActual;
        decimal DayTarget(DateOnly d) => planByDate.TryGetValue(d, out var q) ? q : plan.Count == 0 ? evenTarget : 0m;

        var days = new List<KpiBtpDayDto>();
        decimal cumTarget = 0m, projectedActual = 0m;   // projectedActual: Σ KPI các ngày tương lai, giả định tổ đạt đúng KPI
        var cumCut = 0;
        var cumActualReal = 0;
        for (var i = 0; i < dates.Count; i++)
        {
            var date = dates[i];
            var future = date > today;
            var target = DayTarget(date);
            cumTarget += target;
            var cut = cutByDate.GetValueOrDefault(date);
            cumCut += cut;
            int? actual = null;
            if (!future)
            {
                actual = actualByDate.GetValueOrDefault(date);
                cumActualReal += actual.Value;
            }
            else projectedActual += target;

            var remaining = openingBtp + cumCut - cumActualReal - projectedActual;
            var shortfall = Math.Max(0m, cumTarget - (openingBtp + cumCut));
            var nextTarget = i + 1 < dates.Count ? DayTarget(dates[i + 1]) : 0m;
            days.Add(new KpiBtpDayDto(date, future, Math.Round(target, 2), Math.Round(cumTarget, 2), cut, cumCut + openingCut,
                actual, cumActualReal + openingActual, Math.Round(remaining, 2), Math.Round(shortfall, 2),
                LowBtp: !future && nextTarget > 0 && remaining < nextTarget,
                BehindTarget: !future && cumActualReal < cumTarget));
        }

        var lastReal = days.LastOrDefault(d => !d.IsFuture);
        var cutTotal = cutByDate.Values.Sum();
        var summary = new KpiBtpSummaryDto(
            k.TargetFinishedQty, openingBtp, cutTotal, cumActualReal,
            lastReal?.BtpRemaining ?? openingBtp,
            Math.Max(0m, k.TargetFinishedQty - (openingBtp + cutTotal)));
        var option = new KpiBtpOptionDto(k.KpiTargetId, k.OrderCode, k.ProductCode, k.ProductName, k.TeamName, k.StartDate, k.EndDate,
            CodeText.ToPascal(k.Status));
        return Result<KpiBtpComparisonDto>.Success(new KpiBtpComparisonDto(option, summary, days));
    }
}
