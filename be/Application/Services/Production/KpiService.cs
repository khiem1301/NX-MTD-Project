using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Quản đốc lập / sửa / hủy chỉ tiêu KPI tổ may. Một đơn hàng + mã hàng + tổ may có nhiều chỉ tiêu nhưng các kỳ không được trùng
/// ngày nào (trừ chỉ tiêu đã hủy); một đơn + mã hàng có thể chia cho nhiều tổ. KPI chỉ được chốt khi kết thúc đơn của tổ (<see cref="Production.EndOfOrderService"/>),
/// không tự đóng theo ngày hết kỳ.
/// </summary>
public sealed class KpiService : IKpiService
{
    private readonly IUnitOfWork _uow;
    private readonly IOutputQueryService _output;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;
    private readonly ISystemSettingReader _settings;
    private readonly ILogger<KpiService> _logger;

    public KpiService(IUnitOfWork uow, IOutputQueryService output, IAuditLogService audit, INotificationService notifier,
        IClock clock, ISystemSettingReader settings, ILogger<KpiService> logger)
    {
        _uow = uow;
        _output = output;
        _audit = audit;
        _notifier = notifier;
        _clock = clock;
        _settings = settings;
        _logger = logger;
    }

    private const string KpiLink = "/kpi/targets";
    private const int DefaultDueWarningDays = 3;

    private IQueryable<KpiTarget> Targets => _uow.Repository<KpiTarget>().Query();

    private static readonly Expression<Func<KpiTarget, KpiTargetDto>> ToDto = k => new KpiTargetDto(
        k.KpiTargetId, k.OrderId, k.Order.OrderCode, k.ProductId, k.Product.ProductCode, k.Product.ProductName,
        k.SewingTeamId, k.SewingTeam.TeamName, k.StartDate, k.EndDate, k.TargetFinishedQty, k.WorkingDays,
        Math.Round(k.TargetFinishedQty / (decimal)k.WorkingDays, 2), k.Status, k.Result, k.CreatedAt, 0, 0m);   // thực tế điền sau (WithActualAsync)

    public async Task<Result<PagedResult<KpiTargetDto>>> GetPagedAsync(KpiTargetQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var rawStatus = ToRawStatus(query.Status);
        var page = await Targets.AsNoTracking()
            .Where(k => string.IsNullOrEmpty(rawStatus) || k.Status == rawStatus)
            .Where(k => query.OrderId == null || k.OrderId == query.OrderId)
            .Where(k => query.SewingTeamId == null || k.SewingTeamId == query.SewingTeamId)
            .Where(k => string.IsNullOrEmpty(keyword) || k.Order.OrderCode.Contains(keyword)
                        || k.Product.ProductCode.Contains(keyword) || k.SewingTeam.TeamName.Contains(keyword))
            .OrderByDescending(k => k.KpiTargetId)
            .Select(ToDto)
            .ToPagedAsync(query, ct);
        var items = await WithActualAsync(page.Items.Select(Normalize).ToList(), ct);
        return Result<PagedResult<KpiTargetDto>>.Success(page with { Items = items });
    }

    public async Task<Result<KpiTargetDto>> GetByIdAsync(int kpiTargetId, CancellationToken ct)
    {
        var dto = await Targets.AsNoTracking().Where(k => k.KpiTargetId == kpiTargetId).Select(ToDto)
            .FirstOrDefaultAsync(ct);
        if (dto is null) return NotFound();
        return Result<KpiTargetDto>.Success((await WithActualAsync([Normalize(dto)], ct))[0]);
    }

    /// <summary>
    /// MI-17: điền thực tế (Σ FinishedQty cả kỳ từ vw_KpiDailyResult) và % đạt cho các chỉ tiêu của trang — một
    /// truy vấn nhóm theo KpiTargetId, không N+1. Chưa có sản lượng hoàn thiện thì thực tế = 0, % đạt = 0.
    /// </summary>
    private async Task<List<KpiTargetDto>> WithActualAsync(List<KpiTargetDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;
        var totals = await _output.GetKpiFinishedTotalsAsync(items.Select(i => i.KpiTargetId).ToList(), ct);
        return items.Select(i =>
        {
            var actual = totals.GetValueOrDefault(i.KpiTargetId);
            var percent = i.TargetFinishedQty > 0
                ? Math.Round(100m * actual / i.TargetFinishedQty, 2, MidpointRounding.AwayFromZero)
                : 0m;
            return i with { ActualFinishedQty = actual, PercentAchieved = percent };
        }).ToList();
    }

    /// <summary>DTO trả API phải là PascalCase (Coding Rules 2.2); DB lưu UPPER_SNAKE.</summary>
    private static KpiTargetDto Normalize(KpiTargetDto dto) => dto with
    {
        Status = CodeText.ToPascal(dto.Status),
        Result = dto.Result is null ? null : CodeText.ToPascal(dto.Result),
    };

    /// <summary>FE gửi lại đúng giá trị PascalCase đã nhận (vd "Active") → "ACTIVE" để so khớp cột DB.</summary>
    private static string? ToRawStatus(string? status) => status switch
    {
        null or "" => null,
        nameof(KpiStatuses.Active) => KpiStatuses.Active,
        nameof(KpiStatuses.Closed) => KpiStatuses.Closed,
        nameof(KpiStatuses.Cancelled) => KpiStatuses.Cancelled,
        _ => status,
    };

    public async Task<Result<KpiTargetDto>> CreateAsync(SaveKpiTargetDto dto, CancellationToken ct)
    {
        var check = await ValidateAsync(dto, null, ct);
        if (check is not null) return check;

        var target = new KpiTarget { Status = KpiStatuses.Active };
        Apply(target, dto);
        await _uow.InTransactionAsync(async () =>   // log cần KpiTargetId nên lưu trước (GBR-15: cùng transaction)
        {
            await _uow.Repository<KpiTarget>().AddAsync(target);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(KpiTarget), target.KpiTargetId, AuditActions.Insert, null, Snapshot(target)));
            // Tổ trưởng tổ may là người thực hiện chỉ tiêu.
            await _notifier.NotifyTeamLeadersAsync(dto.SewingTeamId, new NotificationMessage(NotificationSources.Production,
                "Có chỉ tiêu KPI mới", $"Quản đốc giao chỉ tiêu {dto.TargetFinishedQty} sản phẩm từ {dto.StartDate:dd/MM/yyyy} đến {dto.EndDate:dd/MM/yyyy}.",
                KpiLink), ct);
            await _uow.SaveChangesAsync();
            return Result.Success();
        });

        _logger.LogInformation(
            "Đã tạo chỉ tiêu KPI {KpiTargetId}: đơn {OrderId}, mã hàng {ProductId}, tổ may {TeamId}, {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}",
            target.KpiTargetId, dto.OrderId, dto.ProductId, dto.SewingTeamId, dto.StartDate, dto.EndDate);
        return await GetByIdAsync(target.KpiTargetId, ct);
    }

    public async Task<Result<KpiTargetDto>> UpdateAsync(int kpiTargetId, SaveKpiTargetDto dto, CancellationToken ct)
    {
        var target = await _uow.Repository<KpiTarget>().Query()
            .FirstOrDefaultAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (target is null) return NotFound();
        if (target.Status != KpiStatuses.Active)
            return Result<KpiTargetDto>.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForEdit);

        var check = await ValidateAsync(dto, target, ct);
        if (check is not null) return check;
        var actual = (await _output.GetKpiFinishedTotalsAsync([kpiTargetId], ct)).GetValueOrDefault(kpiTargetId);
        if (dto.TargetFinishedQty < actual)
            return Fail(ErrorCodes.KpiPlanInvalid, string.Format(Messages.KpiTargetBelowActual, actual));

        // Đổi kỳ thì bỏ các ngày nằm ngoài kỳ mới; chỉ tiêu cả kỳ mới không được nhỏ hơn tổng KPI các ngày còn lại.
        var days = _uow.Repository<KpiDailyTarget>();
        var oldDays = await days.Query().Where(d => d.KpiTargetId == kpiTargetId).ToListAsync(ct);
        var outside = oldDays.Where(d => d.WorkDate < dto.StartDate || d.WorkDate > dto.EndDate).ToList();
        var cutoff = await _settings.GetTimeAsync(SettingKeys.KpiEditCutoffTime, KpiCalendarService.DefaultEditCutoff, ct);
        var firstEditable = KpiCalendarService.FirstEditableDate(_clock.Now, cutoff);
        if (outside.Any(d => d.WorkDate < firstEditable))
            return Fail(ErrorCodes.KpiPlanInvalid, string.Format(Messages.KpiDayLocked, cutoff));

        var kept = oldDays.Except(outside).ToList();
        var keptSum = kept.Sum(d => d.TargetQty);
        if (keptSum > dto.TargetFinishedQty)
            return Fail(ErrorCodes.KpiPlanInvalid, string.Format(Messages.KpiTotalBelowPlan, keptSum));

        var before = Snapshot(target);
        days.RemoveRange(outside);
        Apply(target, dto);
        if (kept.Count > 0) target.WorkingDays = (short)kept.Count;
        _audit.Add(new AuditEntry(nameof(KpiTarget), kpiTargetId, AuditActions.Update, before, Snapshot(target)));
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(kpiTargetId, ct);
    }

    public async Task<Result> CancelAsync(int kpiTargetId, CancellationToken ct)
    {
        var target = await _uow.Repository<KpiTarget>().Query()
            .FirstOrDefaultAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (target is null)
            return Result.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);
        if (target.Status != KpiStatuses.Active)
            return Result.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForCancel);

        target.Status = KpiStatuses.Cancelled;
        _audit.Add(new AuditEntry(nameof(KpiTarget), kpiTargetId, AuditActions.Update,
            new { Status = KpiStatuses.Active }, new { target.Status }));
        await _notifier.NotifyTeamLeadersAsync(target.SewingTeamId, new NotificationMessage(NotificationSources.Production,
            "Chỉ tiêu KPI bị hủy", $"Quản đốc đã hủy chỉ tiêu KPI {target.TargetFinishedQty} sản phẩm của tổ.", KpiLink), ct);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Đã hủy chỉ tiêu KPI {KpiTargetId}", kpiTargetId);
        return Result.Success(Messages.Saved);
    }

    public async Task<Result<IReadOnlyList<KpiDailyResultDto>>> GetDailyResultsAsync(int kpiTargetId, CancellationToken ct)
    {
        var exists = await Targets.AsNoTracking().AnyAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (!exists)
            return Result<IReadOnlyList<KpiDailyResultDto>>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

        var rows = await _output.GetKpiDailyResultsAsync(kpiTargetId, ct);   // KI-10: đọc qua dịch vụ dùng chung
        return Result<IReadOnlyList<KpiDailyResultDto>>.Success(rows);
    }

    /// <summary>Kỳ không trước ngày nhận đơn và không ở quá khứ. Khi sửa chỉ kiểm mốc ngày bị đổi, để chỉ tiêu đang chạy vẫn sửa được số lượng.</summary>
    private string? CheckPeriod(SaveKpiTargetDto dto, KpiTarget? existing, DateOnly receivedDate)
    {
        var today = _clock.Today;
        if (existing?.StartDate != dto.StartDate)
        {
            if (dto.StartDate < receivedDate) return string.Format(Messages.KpiStartBeforeReceived, receivedDate);
            if (dto.StartDate < today) return Messages.KpiStartInPast;
        }
        return existing?.EndDate != dto.EndDate && dto.EndDate < today ? Messages.KpiEndInPast : null;
    }

    private async Task<Result<KpiTargetDto>?> ValidateAsync(SaveKpiTargetDto dto, KpiTarget? existing, CancellationToken ct)
    {
        var exceptKpiTargetId = existing?.KpiTargetId;
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.OrderId == dto.OrderId).Select(o => new { o.Status, o.ReceivedDate }).FirstOrDefaultAsync(ct);
        if (order is null)
            return Result<KpiTargetDto>.Failure(ErrorCodes.CommonNotFound, Messages.KpiOrderNotFound, ResultKind.NotFound);
        if (OrderStatuses.Closed.Contains(order.Status)) return Fail(ErrorCodes.KpiInvalidReference, Messages.KpiOrderClosed);

        var teamOk = await _uow.Repository<Team>().Query().AsNoTracking()
            .AnyAsync(t => t.TeamId == dto.SewingTeamId && t.TeamType == TeamTypes.Sewing
                           && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return Fail(ErrorCodes.KpiInvalidReference, Messages.KpiTeamInvalid);

        var productInOrder = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .AnyAsync(l => l.OrderId == dto.OrderId && l.ProductId == dto.ProductId, ct);
        if (!productInOrder) return Fail(ErrorCodes.KpiInvalidReference, Messages.KpiProductNotInOrder);

        var periodError = CheckPeriod(dto, existing, order.ReceivedDate);
        if (periodError is not null) return Fail(ErrorCodes.KpiPeriodInvalid, periodError);

        // Hai kỳ không được có chung ngày nào, kể cả ngày kết thúc kỳ này trùng ngày bắt đầu kỳ kia; chỉ tiêu đã hủy không tính.
        var overlap = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .Where(k => k.OrderId == dto.OrderId && k.ProductId == dto.ProductId && k.SewingTeamId == dto.SewingTeamId
                        && k.Status != KpiStatuses.Cancelled && k.StartDate <= dto.EndDate && k.EndDate >= dto.StartDate
                        && (exceptKpiTargetId == null || k.KpiTargetId != exceptKpiTargetId))
            .Select(k => new { k.StartDate, k.EndDate }).FirstOrDefaultAsync(ct);
        if (overlap is not null)
            return Fail(ErrorCodes.KpiPeriodOverlap, string.Format(Messages.KpiPeriodOverlap, overlap.StartDate, overlap.EndDate));

        var capacity = await ComputeOrderCapacityAsync(dto.OrderId, dto.ProductId, exceptKpiTargetId, ct);
        return dto.TargetFinishedQty > capacity.MaxTarget
            ? Fail(ErrorCodes.KpiExceedsOrderQty,
                string.Format(Messages.KpiExceedsOrderQty, capacity.OrderQty, capacity.AssignedToOthers, capacity.MaxTarget))
            : null;
    }

    public async Task<Result<KpiOrderCapacityDto>> GetOrderCapacityAsync(int orderId, int productId, int? exceptKpiTargetId,
        CancellationToken ct)
    {
        var capacity = await ComputeOrderCapacityAsync(orderId, productId, exceptKpiTargetId, ct);
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.OrderId == orderId).Select(o => new { o.DueDate, o.Status }).FirstOrDefaultAsync(ct);
        if (order is null)
            return Result<KpiOrderCapacityDto>.Failure(ErrorCodes.CommonNotFound, Messages.KpiOrderNotFound, ResultKind.NotFound);

        var warningDays = await _settings.GetIntAsync(SettingKeys.KpiDueWarningDays, DefaultDueWarningDays, ct);
        return Result<KpiOrderCapacityDto>.Success(capacity with
        {
            DueDate = order.DueDate, DueWarningDays = warningDays, OrderStatus = CodeText.ToPascal(order.Status),
            OrderClosed = OrderStatuses.Closed.Contains(order.Status),
        });
    }

    /// <summary>Tối đa = số lượng đơn đặt của mã hàng − phần đã giao ở các chỉ tiêu khác; chỉ tiêu đã đóng chỉ giữ số thực tế làm được.</summary>
    private async Task<KpiOrderCapacityDto> ComputeOrderCapacityAsync(int orderId, int productId, int? exceptKpiTargetId,
        CancellationToken ct)
    {
        var orderQty = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId && l.ProductId == productId)
            .SumAsync(l => l.OrderedQty, ct);
        var others = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .Where(k => k.OrderId == orderId && k.ProductId == productId && k.Status != KpiStatuses.Cancelled
                        && (exceptKpiTargetId == null || k.KpiTargetId != exceptKpiTargetId))
            .Select(k => new { k.KpiTargetId, k.TargetFinishedQty, k.Status }).ToListAsync(ct);
        var closedIds = others.Where(o => o.Status == KpiStatuses.Closed).Select(o => o.KpiTargetId).ToList();
        var done = await _output.GetKpiFinishedTotalsAsync(closedIds, ct);
        var assigned = others.Sum(o => o.Status == KpiStatuses.Closed ? done.GetValueOrDefault(o.KpiTargetId) : o.TargetFinishedQty);

        return new KpiOrderCapacityDto(orderQty, assigned, Math.Max(0, orderQty - assigned));
    }

    private static void Apply(KpiTarget target, SaveKpiTargetDto dto)
    {
        target.OrderId = dto.OrderId;
        target.ProductId = dto.ProductId;
        target.SewingTeamId = dto.SewingTeamId;
        target.StartDate = dto.StartDate;
        target.EndDate = dto.EndDate;
        target.TargetFinishedQty = dto.TargetFinishedQty;
        target.WorkingDays = KpiCalendarService.WorkingDaysIn(dto.StartDate, dto.EndDate);
    }

    private static object Snapshot(KpiTarget t) => new
    {
        t.OrderId, t.ProductId, t.SewingTeamId, t.StartDate, t.EndDate, t.TargetFinishedQty, t.WorkingDays, t.Status,
    };

    private static Result<KpiTargetDto> NotFound() =>
        Result<KpiTargetDto>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

    private static Result<KpiTargetDto> Fail(string code, string message) => Result<KpiTargetDto>.Failure(code, message);
}
