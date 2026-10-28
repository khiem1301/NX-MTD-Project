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

        var before = Snapshot(target);
        Apply(target, dto);
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
        if (duplicate) return Fail(ErrorCodes.KpiDuplicatePeriod, Messages.KpiDuplicatePeriod);

        // Swimlane III.3.1: đặt KPI thì hệ thống kiểm BTP khả dụng (kho + tổ đang giữ − các chỉ tiêu khác).
        var capacity = await ComputeCapacityAsync(dto.OrderId, dto.ProductId, dto.SewingTeamId, exceptKpiTargetId, ct);
        return dto.TargetFinishedQty > capacity.MaxTarget
            ? Fail(ErrorCodes.KpiExceedsStock, string.Format(Messages.KpiExceedsStock, capacity.WarehouseBtp, capacity.TeamHeld,
                capacity.CommittedByOthers, capacity.MaxTarget))
            : null;
    }

    public async Task<Result<KpiCapacityDto>> GetCapacityAsync(int orderId, int productId, int sewingTeamId,
        int? exceptKpiTargetId, CancellationToken ct) =>
        Result<KpiCapacityDto>.Success(await ComputeCapacityAsync(orderId, productId, sewingTeamId, exceptKpiTargetId, ct));

    /// <summary>
    /// Tối đa = BTP bộ đủ còn trong kho (đã nhận từ tổ cắt − đã cấp cho các tổ may) + BTP tổ này đã nhận mà chưa thành
    /// phẩm / chưa hủy − phần còn phải làm (chỉ tiêu − đã hoàn thiện) của các chỉ tiêu KPI còn hiệu lực khác cùng đơn + mã hàng.
    /// </summary>
    private async Task<KpiCapacityDto> ComputeCapacityAsync(int orderId, int productId, int sewingTeamId,
        int? exceptKpiTargetId, CancellationToken ct)
    {
        var handedIn = await _uow.Repository<CutHandoverLine>().Query().AsNoTracking()
            .Where(l => l.CutHandover.Status == HandoverStatuses.Confirmed && l.ProductComponentMaterialId == null
                        && l.OrderLine.OrderId == orderId && l.OrderLine.ProductId == productId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        // Phiếu cấp BTP chờ nhận tính số cấp, đã nhận tính số xác nhận; phiếu bị từ chối trả lại kho.
        var issuedOut = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.WarehouseType == WarehouseTypes.SemiFinished && l.ProductComponentMaterialId == null
                        && l.MaterialIssue.Status != IssueStatuses.Rejected && l.OrderLine != null
                        && l.OrderLine.OrderId == orderId && l.OrderLine.ProductId == productId)
            .SumAsync(l => l.MaterialIssue.Status == IssueStatuses.Received ? l.ConfirmedQty ?? 0 : l.IssuedQty, ct);
        var warehouse = Math.Max(0, handedIn - (int)issuedOut);

        var received = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .ReceivedSets(sewingTeamId)
            .Where(l => l.OrderLine!.OrderId == orderId && l.OrderLine.ProductId == productId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        var finished = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(q => q.SewingTeamId == sewingTeamId && q.OrderLine.OrderId == orderId && q.OrderLine.ProductId == productId)
            .SumAsync(q => q.FinishedQty, ct);
        var scrap = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.SewingTeamId == sewingTeamId && d.DefectType == DefectTypes.Scrap && d.Status != DefectStatuses.Cancelled
                        && d.OrderLine.OrderId == orderId && d.OrderLine.ProductId == productId)
            .SumAsync(d => d.Quantity, ct);
        var held = Math.Max(0, (int)received - finished - scrap);

        var others = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .Where(k => k.OrderId == orderId && k.ProductId == productId && k.Status == KpiStatuses.Active
                        && (exceptKpiTargetId == null || k.KpiTargetId != exceptKpiTargetId))
            .Select(k => new { k.KpiTargetId, k.TargetFinishedQty }).ToListAsync(ct);
        var done = await _output.GetKpiFinishedTotalsAsync(others.Select(o => o.KpiTargetId).ToList(), ct);
        var committed = others.Sum(o => Math.Max(0, o.TargetFinishedQty - done.GetValueOrDefault(o.KpiTargetId)));

        return new KpiCapacityDto(warehouse, held, committed, Math.Max(0, warehouse + held - committed));
    }

    private static void Apply(KpiTarget target, SaveKpiTargetDto dto)
    {
        target.OrderId = dto.OrderId;
        target.ProductId = dto.ProductId;
        target.SewingTeamId = dto.SewingTeamId;
        target.StartDate = dto.StartDate;
        target.EndDate = dto.EndDate;
        target.TargetFinishedQty = dto.TargetFinishedQty;
        target.WorkingDays = dto.WorkingDays;
    }

    private static object Snapshot(KpiTarget t) => new
    {
        t.OrderId, t.ProductId, t.SewingTeamId, t.StartDate, t.EndDate, t.TargetFinishedQty, t.WorkingDays, t.Status,
    };

    private static Result<KpiTargetDto> NotFound() =>
        Result<KpiTargetDto>.Failure(ErrorCodes.KpiNotFound, Messages.KpiNotFound, ResultKind.NotFound);

    private static Result<KpiTargetDto> Fail(string code, string message) => Result<KpiTargetDto>.Failure(code, message);
}
