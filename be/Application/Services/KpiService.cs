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
/// Quản đốc lập / sửa / hủy chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ; KpiAutoCloseJob
/// gọi <see cref="CloseExpiredAsync"/> mỗi ngày để tự đóng các chỉ tiêu đã hết kỳ (xem IKpiService).
/// </summary>
public sealed class KpiService : IKpiService
{
    private readonly IUnitOfWork _uow;
    private readonly IOutputQueryService _output;
    private readonly IAuditLogService _audit;
    private readonly ILogger<KpiService> _logger;

    public KpiService(IUnitOfWork uow, IOutputQueryService output, IAuditLogService audit, ILogger<KpiService> logger)
    {
        _uow = uow;
        _output = output;
        _audit = audit;
        _logger = logger;
    }

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

        var check = await ValidateAsync(dto, kpiTargetId, ct);
        if (check is not null) return check;

        // Đổi kỳ thì bỏ các ngày nằm ngoài kỳ mới; tổng cả kỳ mới không được nhỏ hơn tổng KPI đã đặt cho các ngày còn lại.
        var days = _uow.Repository<KpiDailyTarget>();
        var oldDays = await days.Query().Where(d => d.KpiTargetId == kpiTargetId).ToListAsync(ct);
        var outside = oldDays.Where(d => d.WorkDate < dto.StartDate || d.WorkDate > dto.EndDate).ToList();
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
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Đã hủy chỉ tiêu KPI {KpiTargetId}", kpiTargetId);
        return Result.Success(Messages.Saved);
    }

    /// <summary>
    /// Mỗi chỉ tiêu ACTIVE đã qua EndDate: Σ FinishedQty cả kỳ (vw_KpiDailyResult, đã lọc theo
    /// StartDate..EndDate sẵn trong view) so với TargetFinishedQty → ACHIEVED nếu đạt, ngược lại NOT_ACHIEVED.
    /// Gộp một truy vấn nhóm theo KpiTargetId thay vì lặp từng dòng để tránh N+1.
    /// </summary>
    public async Task<int> CloseExpiredAsync(DateOnly today, CancellationToken ct)
    {
        var expired = await _uow.Repository<KpiTarget>().Query()
            .Where(k => k.Status == KpiStatuses.Active && k.EndDate < today)
            .ToListAsync(ct);
        if (expired.Count == 0) return 0;

        var totalByKpiTargetId = await _output.GetKpiFinishedTotalsAsync(expired.Select(k => k.KpiTargetId).ToList(), ct);

        foreach (var target in expired)
        {
            var total = totalByKpiTargetId.GetValueOrDefault(target.KpiTargetId);
            target.Result = total >= target.TargetFinishedQty ? KpiResults.Achieved : KpiResults.NotAchieved;
            target.Status = KpiStatuses.Closed;
            // Job nền: không có người đăng nhập nên AuditLogService ghi ChangedBy = NULL.
            _audit.Add(new AuditEntry(nameof(KpiTarget), target.KpiTargetId, AuditActions.Update,
                new { Status = KpiStatuses.Active }, new { target.Status, target.Result, ActualQty = total }));
        }

        await _uow.SaveChangesAsync();
        _logger.LogInformation("KpiAutoCloseJob: đã đóng {Count} chỉ tiêu KPI hết hạn trước {Today:yyyy-MM-dd}",
            expired.Count, today);
        return expired.Count;
    }

    public async Task<Result<IReadOnlyList<KpiDailyResultDto>>> GetDailyResultsAsync(int kpiTargetId, CancellationToken ct)
    {
        var exists = await Targets.AsNoTracking().AnyAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (!exists)
            return Result<IReadOnlyList<KpiDailyResultDto>>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

        var rows = await _output.GetKpiDailyResultsAsync(kpiTargetId, ct);   // KI-10: đọc qua dịch vụ dùng chung
        return Result<IReadOnlyList<KpiDailyResultDto>>.Success(rows);
    }

    private async Task<Result<KpiTargetDto>?> ValidateAsync(SaveKpiTargetDto dto, int? exceptKpiTargetId,
        CancellationToken ct)
    {
        var orderExists = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .AnyAsync(o => o.OrderId == dto.OrderId, ct);
        if (!orderExists)
            return Result<KpiTargetDto>.Failure(ErrorCodes.CommonNotFound, Messages.KpiOrderNotFound, ResultKind.NotFound);

        var teamOk = await _uow.Repository<Team>().Query().AsNoTracking()
            .AnyAsync(t => t.TeamId == dto.SewingTeamId && t.TeamType == TeamTypes.Sewing
                           && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return Fail(ErrorCodes.KpiInvalidReference, Messages.KpiTeamInvalid);

        var productInOrder = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .AnyAsync(l => l.OrderId == dto.OrderId && l.ProductId == dto.ProductId, ct);
        if (!productInOrder) return Fail(ErrorCodes.KpiInvalidReference, Messages.KpiProductNotInOrder);

        // Khoá UNIQUE (OrderId, ProductId, SewingTeamId, StartDate) của DB không lọc theo Status,
        // nên kiểm tra trùng trên mọi trạng thái (kể cả CANCELLED/CLOSED) trước khi lưu.
        var duplicate = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .AnyAsync(k => k.OrderId == dto.OrderId && k.ProductId == dto.ProductId && k.SewingTeamId == dto.SewingTeamId
                           && k.StartDate == dto.StartDate
                           && (exceptKpiTargetId == null || k.KpiTargetId != exceptKpiTargetId), ct);
        return duplicate ? Fail(ErrorCodes.KpiDuplicatePeriod, Messages.KpiDuplicatePeriod) : null;
    }

    private static void Apply(KpiTarget target, SaveKpiTargetDto dto)
    {
        target.OrderId = dto.OrderId;
        target.ProductId = dto.ProductId;
        target.SewingTeamId = dto.SewingTeamId;
        target.StartDate = dto.StartDate;
        target.EndDate = dto.EndDate;
        target.TargetFinishedQty = dto.TargetFinishedQty;
        // Chưa đặt chỉ tiêu ngày: số ngày làm việc = số ngày trong kỳ trừ Chủ nhật (dịch vụ lịch KPI cập nhật lại khi đặt ngày).
        target.WorkingDays = KpiCalendarMath.WorkingDaysIn(dto.StartDate, dto.EndDate);
    }

    private static object Snapshot(KpiTarget t) => new
    {
        t.OrderId, t.ProductId, t.SewingTeamId, t.StartDate, t.EndDate, t.TargetFinishedQty, t.WorkingDays, t.Status,
    };

    private static Result<KpiTargetDto> NotFound() =>
        Result<KpiTargetDto>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

    private static Result<KpiTargetDto> Fail(string code, string message) => Result<KpiTargetDto>.Failure(code, message);
}
