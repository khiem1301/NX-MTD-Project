using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Tính số liệu dùng chung cho KPI theo ngày.</summary>
public static class KpiCalendarMath
{
    /// <summary>Số ngày trong kỳ (gồm hai đầu) trừ Chủ nhật, tối thiểu 1.</summary>
    public static short WorkingDaysIn(DateOnly start, DateOnly end)
    {
        var n = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday) n++;
        return (short)Math.Max(1, n);
    }
}

/// <summary>
/// Lịch KPI theo ngày. BTP là kho chung của (đơn, mã hàng): Tổng BTP = BTP cắt vào kho (giao dịch kho BTP loại CUT_HANDOVER)
/// − hàng hoàn thiện QA/QC nhập ra chuyền của mọi tổ. KPI đã đặt mà chưa ra chuyền của mọi KPI đang chạy cùng (đơn, mã hàng)
/// được giữ chỗ, số còn lại mới đặt thêm được.
/// </summary>
public sealed class KpiCalendarService : IKpiCalendarService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public KpiCalendarService(IUnitOfWork uow, IClock clock, IAuditLogService audit)
    {
        _uow = uow; _clock = clock; _audit = audit;
    }

    private IQueryable<KpiTarget> Targets => _uow.Repository<KpiTarget>().Query();

    private sealed record Head(
        int KpiTargetId, int OrderId, int ProductId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate, int TargetFinishedQty,
        string Status, string OrderCode, string ProductCode, string ProductName, string TeamName);

    private sealed record PlanRow(int KpiTargetId, DateOnly Date, int Qty);

    /// <summary>Mọi dữ liệu cần để dựng lịch và kiểm tra BTP của một KPI.</summary>
    private sealed class Data
    {
        public required Head K { get; init; }
        public required Dictionary<DateOnly, int> CutByDate { get; init; }
        public required Dictionary<(int Team, DateOnly Date), int> Qc { get; init; }
        /// <summary>Tổ may của từng KPI đang chạy cùng (đơn, mã hàng), kể cả KPI này.</summary>
        public required Dictionary<int, int> ActiveTeams { get; init; }
        public required List<PlanRow> Plans { get; init; }
    }

    private async Task<Data?> LoadAsync(int kpiTargetId, CancellationToken ct)
    {
        var k = await Targets.AsNoTracking().Where(x => x.KpiTargetId == kpiTargetId)
            .Select(x => new Head(x.KpiTargetId, x.OrderId, x.ProductId, x.SewingTeamId, x.StartDate, x.EndDate, x.TargetFinishedQty,
                x.Status, x.Order.OrderCode, x.Product.ProductCode, x.Product.ProductName, x.SewingTeam.TeamName))
            .FirstOrDefaultAsync(ct);
        if (k is null) return null;

        var active = await Targets.AsNoTracking()
            .Where(x => x.OrderId == k.OrderId && x.ProductId == k.ProductId && x.Status == KpiStatuses.Active)
            .Select(x => new { x.KpiTargetId, x.SewingTeamId }).ToListAsync(ct);
        var activeTeams = active.ToDictionary(a => a.KpiTargetId, a => a.SewingTeamId);

        var ids = activeTeams.Keys.Append(k.KpiTargetId).Distinct().ToArray();
        var plans = (await _uow.Repository<KpiDailyTarget>().Query().AsNoTracking()
                .Where(d => ids.Contains(d.KpiTargetId))
                .Select(d => new { d.KpiTargetId, d.WorkDate, d.TargetQty }).ToListAsync(ct))
            .Select(d => new PlanRow(d.KpiTargetId, d.WorkDate, d.TargetQty)).ToList();

        var cuts = await _uow.Repository<StockTransaction>().Query().AsNoTracking()
            .Where(t => t.WarehouseType == WarehouseTypes.SemiFinished && t.SourceDocType == StockDocTypes.CutHandover
                        && t.OrderLine!.OrderId == k.OrderId && t.OrderLine.ProductId == k.ProductId)
            .Select(t => new { t.TransactionDate, t.QtyChange }).ToListAsync(ct);
        var cutByDate = cuts.GroupBy(c => c.TransactionDate).ToDictionary(g => g.Key, g => (int)g.Sum(c => c.QtyChange));

        var qc = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(b => b.OrderLine.OrderId == k.OrderId && b.OrderLine.ProductId == k.ProductId)
            .Select(b => new { b.SewingTeamId, b.WorkDate, b.FinishedQty }).ToListAsync(ct);
        var qcMap = qc.GroupBy(b => (b.SewingTeamId, b.WorkDate)).ToDictionary(g => g.Key, g => g.Sum(b => b.FinishedQty));

        return new Data { K = k, CutByDate = cutByDate, Qc = qcMap, ActiveTeams = activeTeams, Plans = plans };
    }

    private static int CutUpTo(Data d, DateOnly day) => d.CutByDate.Where(c => c.Key <= day).Sum(c => c.Value);
    private static int OutUpTo(Data d, DateOnly day) => d.Qc.Where(q => q.Key.Date <= day).Sum(q => q.Value);

    /// <summary>KPI đã đặt mà chưa ra chuyền: các ngày sau hôm nay + phần còn thiếu của hôm nay (KPI hôm nay − đã ra chuyền hôm nay).</summary>
    private static int Committed(Data d, int team, IEnumerable<PlanRow> rows, DateOnly today)
    {
        var list = rows.ToList();
        var todayOut = d.Qc.GetValueOrDefault((team, today));
        return list.Where(r => r.Date > today).Sum(r => r.Qty)
               + list.Where(r => r.Date == today).Sum(r => Math.Max(0, r.Qty - todayOut));
    }

    private int CommittedOf(Data d, int kpiTargetId, DateOnly today) =>
        d.ActiveTeams.TryGetValue(kpiTargetId, out var team)
            ? Committed(d, team, d.Plans.Where(p => p.KpiTargetId == kpiTargetId), today)
            : 0;

    public async Task<Result<KpiCalendarDto>> GetAsync(int kpiTargetId, CancellationToken ct)
    {
        var data = await LoadAsync(kpiTargetId, ct);
        return data is null ? Fail.NotFound<KpiCalendarDto>() : Result<KpiCalendarDto>.Success(Build(data));
    }

    private KpiCalendarDto Build(Data d)
    {
        var k = d.K;
        var today = _clock.Today;
        var mine = d.Plans.Where(p => p.KpiTargetId == k.KpiTargetId).ToDictionary(p => p.Date, p => p.Qty);

        var cutTotal = CutUpTo(d, today);
        var outTotal = OutUpTo(d, today);
        var totalBtp = cutTotal - outTotal;
        var committedAll = d.ActiveTeams.Keys.Sum(id => CommittedOf(d, id, today));
        var committedThis = CommittedOf(d, k.KpiTargetId, today);

        // Hôm nay các KPI đang chạy còn thiếu bao nhiêu: số này sẽ còn ra chuyền trong ngày, trừ vào BTP dự báo.
        var pendingToday = d.ActiveTeams.Sum(a =>
            Math.Max(0, d.Plans.Where(p => p.KpiTargetId == a.Key && p.Date == today).Sum(p => p.Qty) - d.Qc.GetValueOrDefault((a.Value, today))));

        var days = new List<KpiCalendarDayDto>();
        var cumTarget = 0;
        var cumActual = 0;
        for (var date = k.StartDate; date <= k.EndDate; date = date.AddDays(1))
        {
            var future = date > today;
            var target = mine.GetValueOrDefault(date);
            cumTarget += target;
            int? actual = future ? null : d.Qc.GetValueOrDefault((k.SewingTeamId, date));
            cumActual += actual ?? 0;

            int remaining;
            if (!future) remaining = CutUpTo(d, date) - OutUpTo(d, date);
            else
            {
                var planned = d.ActiveTeams.Keys.Sum(id => d.Plans.Where(p => p.KpiTargetId == id && p.Date > today && p.Date <= date).Sum(p => p.Qty));
                var cutsAhead = d.CutByDate.Where(c => c.Key > today && c.Key <= date).Sum(c => c.Value);
                remaining = totalBtp - pendingToday + cutsAhead - planned;
            }

            var next = mine.Where(p => p.Key > date).OrderBy(p => p.Key).Select(p => p.Value).FirstOrDefault();
            days.Add(new KpiCalendarDayDto(date, future, date == today, target, cumTarget, d.CutByDate.GetValueOrDefault(date), actual, remaining,
                LowBtp: !future && next > 0 && remaining < next,
                BehindTarget: date < today && target > 0 && cumActual < cumTarget));
        }

        var allocated = mine.Values.Sum();
        var summary = new KpiCalendarSummaryDto(today, k.TargetFinishedQty, allocated, Math.Max(0, k.TargetFinishedQty - allocated), cutTotal, outTotal,
            totalBtp, committedAll, committedThis, totalBtp - committedAll, k.Status == KpiStatuses.Active);
        return new KpiCalendarDto(k.KpiTargetId, k.OrderCode, k.ProductCode, k.ProductName, k.TeamName, k.StartDate, k.EndDate,
            CodeText.ToPascal(k.Status), summary, days);
    }

    public async Task<Result<KpiCalendarDto>> SaveDailyPlanAsync(int kpiTargetId, SaveKpiDailyPlanDto dto, CancellationToken ct)
    {
        var data = await LoadAsync(kpiTargetId, ct);
        if (data is null) return Fail.NotFound<KpiCalendarDto>();
        var k = data.K;
        if (k.Status != KpiStatuses.Active)
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForEdit);

        var rows = dto.DailyTargets.OrderBy(r => r.WorkDate).ToList();
        if (rows.Any(r => r.WorkDate < k.StartDate || r.WorkDate > k.EndDate))
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiPlanInvalid, Messages.KpiDailyTargetOutOfPeriod, ResultKind.Validation);
        var sum = rows.Sum(r => r.TargetQty);
        if (sum > k.TargetFinishedQty)
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiPlanInvalid, string.Format(Messages.KpiPlanExceedsTotal, sum, k.TargetFinishedQty));

        // Giữ chỗ BTP: phần KPI chưa ra chuyền của KPI này (sau khi sửa) cộng với của các KPI khác không được vượt Tổng BTP.
        var today = _clock.Today;
        var newPlan = rows.Select(r => new PlanRow(k.KpiTargetId, r.WorkDate, r.TargetQty)).ToList();
        var committedNew = Committed(data, k.SewingTeamId, newPlan, today);
        var committedOld = CommittedOf(data, k.KpiTargetId, today);
        var others = data.ActiveTeams.Keys.Where(id => id != k.KpiTargetId).Sum(id => CommittedOf(data, id, today));
        var available = CutUpTo(data, today) - OutUpTo(data, today) - others;
        // Đang giảm thì vẫn cho lưu dù BTP đã thiếu từ trước (nếu không sẽ không sửa lại được).
        if (committedNew > available && committedNew > committedOld)
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiPlanInvalid, string.Format(Messages.KpiPlanExceedsBtp, Math.Max(0, available)));

        var before = Snapshot(data.Plans.Where(p => p.KpiTargetId == k.KpiTargetId).OrderBy(p => p.Date).Select(p => (p.Date, p.Qty)));
        var result = await _uow.InTransactionAsync(async () =>
        {
            var days = _uow.Repository<KpiDailyTarget>();
            var old = await days.Query().Where(x => x.KpiTargetId == kpiTargetId).ToListAsync(ct);
            days.RemoveRange(old);
            await _uow.SaveChangesAsync();   // xóa trước rồi thêm để khỏi đụng khóa UQ_KpiDaily (KpiTargetId, WorkDate)

            foreach (var r in rows)
                await days.AddAsync(new KpiDailyTarget { KpiTargetId = kpiTargetId, WorkDate = r.WorkDate, TargetQty = r.TargetQty });
            var target = await Targets.FirstAsync(x => x.KpiTargetId == kpiTargetId, ct);
            target.WorkingDays = rows.Count > 0 ? (short)rows.Count : KpiCalendarMath.WorkingDaysIn(k.StartDate, k.EndDate);
            _audit.Add(new AuditEntry(nameof(KpiTarget), kpiTargetId, AuditActions.Update, new { Days = before },
                new { Days = Snapshot(rows.Select(r => (r.WorkDate, r.TargetQty))), Total = sum }, "Đặt KPI theo ngày"));
            await _uow.SaveChangesAsync();
            return Result.Success();
        });
        if (!result.IsSuccess) return Fail.As<KpiCalendarDto>(result);
        return await GetAsync(kpiTargetId, ct);
    }

    private static string[] Snapshot(IEnumerable<(DateOnly Date, int Qty)> rows) => rows.Select(r => $"{r.Date:dd/MM}={r.Qty}").ToArray();
}
