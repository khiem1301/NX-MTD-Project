using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Lịch KPI theo ngày. Chỉ tiêu cả kỳ chỉ so với số lượng đơn (<see cref="KpiService"/>); KPI ngày mới kiểm BTP: phần đã đặt mà
/// chưa hoàn thiện không được vượt BTP khả dụng của tổ, tức kho + tổ đang giữ − phần các tổ khác phải lấy từ kho.
/// </summary>
public sealed class KpiCalendarService : IKpiCalendarService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;
    private readonly ISystemSettingReader _settings;

    /// <summary>Giờ khóa KPI theo ngày khi thiếu khóa KPI_EDIT_CUTOFF_TIME.</summary>
    public static readonly TimeOnly DefaultEditCutoff = new(8, 0);

    public KpiCalendarService(IUnitOfWork uow, IClock clock, IAuditLogService audit, ISystemSettingReader settings)
    {
        _uow = uow; _clock = clock; _audit = audit; _settings = settings;
    }

    private IQueryable<KpiTarget> Targets => _uow.Repository<KpiTarget>().Query();

    private sealed record Head(
        int KpiTargetId, int OrderId, int ProductId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate, int TargetFinishedQty,
        string Status, string OrderStatus, DateOnly DueDate, string OrderCode, string ProductCode, string ProductName, string TeamName);

    private sealed record OtherKpi(int KpiTargetId, int SewingTeamId, DateOnly StartDate, DateOnly EndDate);

    private sealed record PlanRow(int KpiTargetId, DateOnly Date, int Qty);

    private sealed record QcRow(int TeamId, DateOnly Date, int Qty);

    /// <summary>Mọi dữ liệu cần để dựng lịch và kiểm tra BTP của một KPI.</summary>
    private sealed class Data
    {
        public required Head K { get; init; }
        public required TimeOnly EditCutoff { get; init; }
        /// <summary>Các KPI đang chạy khác của cùng đơn + mã hàng, kể cả chỉ tiêu kỳ khác của cùng tổ.</summary>
        public required List<OtherKpi> Others { get; init; }
        public required List<PlanRow> Plans { get; init; }
        public required List<QcRow> Qc { get; init; }
        public required int Warehouse { get; init; }
        /// <summary>BTP bộ đủ từng tổ đang giữ (đã nhận − hoàn thiện − hủy).</summary>
        public required Dictionary<int, int> Held { get; init; }
    }

    /// <summary>Số ngày trong kỳ (gồm hai đầu) trừ Chủ nhật, tối thiểu 1.</summary>
    public static short WorkingDaysIn(DateOnly start, DateOnly end)
    {
        var n = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday) n++;
        return (short)Math.Max(1, n);
    }

    /// <summary>Ngày sớm nhất còn sửa được KPI: hôm nay nếu chưa tới giờ khóa, không thì ngày mai.</summary>
    public static DateOnly FirstEditableDate(DateTime now, TimeOnly cutoff)
    {
        var today = DateOnly.FromDateTime(now);
        return TimeOnly.FromDateTime(now) < cutoff ? today : today.AddDays(1);
    }

    public async Task<Result<KpiCalendarDto>> GetAsync(int kpiTargetId, CancellationToken ct)
    {
        var data = await LoadAsync(kpiTargetId, ct);
        return data is null ? NotFound() : Result<KpiCalendarDto>.Success(Build(data));
    }

    public async Task<Result<KpiCalendarDto>> SaveDailyPlanAsync(int kpiTargetId, SaveKpiDailyPlanDto dto, CancellationToken ct)
    {
        var data = await LoadAsync(kpiTargetId, ct);
        if (data is null) return NotFound();
        if (data.K.Status != KpiStatuses.Active)
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForEdit);
        if (OrderStatuses.Closed.Contains(data.K.OrderStatus))
            return Result<KpiCalendarDto>.Failure(ErrorCodes.KpiInvalidReference, Messages.KpiOrderClosed);

        var rows = dto.DailyTargets.OrderBy(r => r.WorkDate).ToList();
        var invalid = CheckPlan(data, rows);
        if (invalid is not null) return invalid;

        var before = Snapshot(data.Plans.Where(p => p.KpiTargetId == kpiTargetId).OrderBy(p => p.Date).Select(p => (p.Date, p.Qty)));
        var result = await _uow.InTransactionAsync(async () =>
        {
            var days = _uow.Repository<KpiDailyTarget>();
            days.RemoveRange(await days.Query().Where(x => x.KpiTargetId == kpiTargetId).ToListAsync(ct));
            await _uow.SaveChangesAsync();   // xóa trước rồi thêm để khỏi đụng khóa UQ_KpiDaily (KpiTargetId, WorkDate)

            foreach (var r in rows)
                await days.AddAsync(new KpiDailyTarget { KpiTargetId = kpiTargetId, WorkDate = r.WorkDate, TargetQty = r.TargetQty });
            var target = await Targets.FirstAsync(x => x.KpiTargetId == kpiTargetId, ct);
            target.WorkingDays = rows.Count > 0 ? (short)rows.Count : WorkingDaysIn(data.K.StartDate, data.K.EndDate);
            _audit.Add(new AuditEntry(nameof(KpiTarget), kpiTargetId, AuditActions.Update, new { Days = before },
                new { Days = Snapshot(rows.Select(r => (r.WorkDate, r.TargetQty))) }, "Đặt KPI theo ngày"));
            await _uow.SaveChangesAsync();
            return Result.Success();
        });
        if (!result.IsSuccess) return Fail.As<KpiCalendarDto>(result);
        return await GetAsync(kpiTargetId, ct);
    }

    private Result<KpiCalendarDto>? CheckPlan(Data d, List<KpiDailyTargetInputDto> rows)
    {
        var k = d.K;
        if (rows.Any(r => r.WorkDate < k.StartDate || r.WorkDate > k.EndDate))
            return Invalid(Messages.KpiDailyTargetOutOfPeriod);

        var saved = PlanOf(d, k.KpiTargetId);
        var firstEditable = FirstEditableDate(_clock.Now, d.EditCutoff);
        var changedLocked = saved.Keys.Union(rows.Select(r => r.WorkDate)).Where(day => day < firstEditable)
            .Any(day => saved.GetValueOrDefault(day) != rows.Where(r => r.WorkDate == day).Sum(r => r.TargetQty));
        if (changedLocked) return Invalid(string.Format(Messages.KpiDayLocked, d.EditCutoff));

        var sum = rows.Sum(r => r.TargetQty);
        if (sum > k.TargetFinishedQty) return Invalid(string.Format(Messages.KpiPlanExceedsTotal, sum, k.TargetFinishedQty));

        // Đang giảm thì vẫn cho lưu dù BTP đã thiếu từ trước (nếu không sẽ không sửa lại được).
        var finished = FinishedOf(d, k.SewingTeamId, k.StartDate, k.EndDate);
        var pending = Math.Max(0, sum - finished);
        var btp = ComputeBtp(d);
        return pending > btp.Available && pending > Math.Max(0, saved.Values.Sum() - finished)
            ? Result<KpiCalendarDto>.Failure(ErrorCodes.KpiExceedsStock, string.Format(Messages.KpiExceedsStock, pending,
                btp.Available, btp.WarehouseBtp, btp.TeamHeld, btp.NeededByOthers))
            : null;
    }

    private KpiCalendarDto Build(Data d)
    {
        var k = d.K;
        var today = _clock.Today;
        var plan = PlanOf(d, k.KpiTargetId);
        var days = new List<KpiCalendarDayDto>();
        for (var date = k.StartDate; date <= k.EndDate; date = date.AddDays(1))
            days.Add(new KpiCalendarDayDto(date, plan.GetValueOrDefault(date),
                date > today ? null : d.Qc.Where(q => q.TeamId == k.SewingTeamId && q.Date == date).Sum(q => q.Qty)));

        return new KpiCalendarDto(k.KpiTargetId, k.OrderCode, k.ProductCode, k.ProductName, k.TeamName, k.StartDate, k.EndDate, today,
            k.DueDate, FirstEditableDate(_clock.Now, d.EditCutoff), d.EditCutoff, k.TargetFinishedQty, FinishedOf(d, k.SewingTeamId, k.StartDate, k.EndDate), k.Status == KpiStatuses.Active && !OrderStatuses.Closed.Contains(k.OrderStatus), ComputeBtp(d), days);
    }

    private static Dictionary<DateOnly, int> PlanOf(Data d, int kpiTargetId) =>
        d.Plans.Where(p => p.KpiTargetId == kpiTargetId).ToDictionary(p => p.Date, p => p.Qty);

    private static int FinishedOf(Data d, int teamId, DateOnly start, DateOnly end) =>
        d.Qc.Where(q => q.TeamId == teamId && q.Date >= start && q.Date <= end).Sum(q => q.Qty);

    private static KpiBtpDto ComputeBtp(Data d)
    {
        var teamHeld = d.Held.GetValueOrDefault(d.K.SewingTeamId);
        // Tổ khác dùng BTP đang giữ trước rồi mới lấy từ kho; chỉ tiêu kỳ khác của cùng tổ thì giành cả BTP tổ đang giữ.
        var neededByOthers = d.Others.GroupBy(o => o.SewingTeamId).Sum(team =>
        {
            var pending = team.Sum(o => Math.Max(0, PlanOf(d, o.KpiTargetId).Values.Sum() - FinishedOf(d, team.Key, o.StartDate, o.EndDate)));
            return team.Key == d.K.SewingTeamId ? pending : Math.Max(0, pending - d.Held.GetValueOrDefault(team.Key));
        });
        return new KpiBtpDto(d.Warehouse, teamHeld, neededByOthers, Math.Max(0, d.Warehouse + teamHeld - neededByOthers));
    }

    private async Task<Data?> LoadAsync(int kpiTargetId, CancellationToken ct)
    {
        var k = await Targets.AsNoTracking().Where(x => x.KpiTargetId == kpiTargetId)
            .Select(x => new Head(x.KpiTargetId, x.OrderId, x.ProductId, x.SewingTeamId, x.StartDate, x.EndDate, x.TargetFinishedQty,
                x.Status, x.Order.Status, x.Order.DueDate, x.Order.OrderCode, x.Product.ProductCode, x.Product.ProductName, x.SewingTeam.TeamName))
            .FirstOrDefaultAsync(ct);
        if (k is null) return null;

        var others = await Targets.AsNoTracking()
            .Where(x => x.OrderId == k.OrderId && x.ProductId == k.ProductId && x.Status == KpiStatuses.Active
                        && x.KpiTargetId != kpiTargetId)
            .Select(x => new OtherKpi(x.KpiTargetId, x.SewingTeamId, x.StartDate, x.EndDate)).ToListAsync(ct);
        var ids = others.Select(o => o.KpiTargetId).Append(kpiTargetId).ToList();
        var plans = await _uow.Repository<KpiDailyTarget>().Query().AsNoTracking()
            .Where(d => ids.Contains(d.KpiTargetId))
            .Select(d => new PlanRow(d.KpiTargetId, d.WorkDate, d.TargetQty)).ToListAsync(ct);
        var qc = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(b => b.OrderLine.OrderId == k.OrderId && b.OrderLine.ProductId == k.ProductId)
            .Select(b => new QcRow(b.SewingTeamId, b.WorkDate, b.FinishedQty)).ToListAsync(ct);

        return new Data
        {
            K = k, Others = others, Plans = plans, Qc = qc,
            EditCutoff = await _settings.GetTimeAsync(SettingKeys.KpiEditCutoffTime, DefaultEditCutoff, ct),
            Warehouse = await WarehouseBtpAsync(k.OrderId, k.ProductId, ct),
            Held = await TeamHeldAsync(k.OrderId, k.ProductId, qc, ct),
        };
    }

    /// <summary>BTP bộ đủ còn trong kho: đã nhận từ tổ cắt − đã cấp cho các tổ may (phiếu chờ nhận tính số cấp, đã nhận tính số xác nhận, bị từ chối thì trả lại kho).</summary>
    private async Task<int> WarehouseBtpAsync(int orderId, int productId, CancellationToken ct)
    {
        var handedIn = await _uow.Repository<CutHandoverLine>().Query().AsNoTracking()
            .Where(l => l.CutHandover.Status == HandoverStatuses.Confirmed && l.ProductComponentMaterialId == null
                        && l.OrderLine.OrderId == orderId && l.OrderLine.ProductId == productId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        var issuedOut = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.WarehouseType == WarehouseTypes.SemiFinished && l.ProductComponentMaterialId == null
                        && l.MaterialIssue.Status != IssueStatuses.Rejected && l.OrderLine != null
                        && l.OrderLine.OrderId == orderId && l.OrderLine.ProductId == productId)
            .SumAsync(l => l.MaterialIssue.Status == IssueStatuses.Received ? l.ConfirmedQty ?? 0 : l.IssuedQty, ct);
        return Math.Max(0, handedIn - (int)issuedOut);
    }

    private async Task<Dictionary<int, int>> TeamHeldAsync(int orderId, int productId, List<QcRow> qc, CancellationToken ct)
    {
        var received = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .ReceivedSets(null)
            .Where(l => l.OrderLine!.OrderId == orderId && l.OrderLine.ProductId == productId)
            .Select(l => new { TeamId = l.MaterialIssue.ReceivingTeamId, Qty = l.ConfirmedQty ?? 0 }).ToListAsync(ct);
        var scrap = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.DefectType == DefectTypes.Scrap && d.Status != DefectStatuses.Cancelled
                        && d.OrderLine.OrderId == orderId && d.OrderLine.ProductId == productId)
            .Select(d => new { d.SewingTeamId, d.Quantity }).ToListAsync(ct);

        return received.GroupBy(r => r.TeamId).ToDictionary(g => g.Key, g => Math.Max(0, (int)g.Sum(r => r.Qty)
            - qc.Where(q => q.TeamId == g.Key).Sum(q => q.Qty)
            - scrap.Where(s => s.SewingTeamId == g.Key).Sum(s => s.Quantity)));
    }

    private static string[] Snapshot(IEnumerable<(DateOnly Date, int Qty)> rows) => rows.Select(r => $"{r.Date:dd/MM}={r.Qty}").ToArray();

    private static Result<KpiCalendarDto> NotFound() =>
        Result<KpiCalendarDto>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

    private static Result<KpiCalendarDto> Invalid(string message) =>
        Result<KpiCalendarDto>.Failure(ErrorCodes.KpiPlanInvalid, message);
}
