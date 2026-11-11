using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-49: Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may; một dòng có thể chia cho nhiều tổ. Tổ chỉ xin, nhận BTP
/// của dòng được chia. Phân công công đoạn và KPI vẫn theo mã hàng, tổ được làm khi có ít nhất một dòng của mã đó.
/// Mỗi tổ chỉ nhận đơn mới khi đơn đang làm đạt % nhập kho TP / SL đặt (TEAM_ORDER_PROGRESS_GATE_PCT).
/// </summary>
public sealed class OrderLineTeamService : IOrderLineTeamService
{
    private const string AssignmentLink = "/operation-assignments";
    private const int MaxLinesInNotification = 5;
    private const int DefaultProgressGatePct = 90;

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly ISystemSettingReader _settings;

    public OrderLineTeamService(IUnitOfWork uow, IAuditLogService audit, INotificationService notifier,
        ISystemSettingReader settings)
    {
        _uow = uow; _audit = audit; _notifier = notifier; _settings = settings;
    }

    private IQueryable<OrderLineTeam> Allotments => _uow.Repository<OrderLineTeam>().Query();
    private IQueryable<OrderLine> Lines => _uow.Repository<OrderLine>().Query();
    private IQueryable<VwOrderLineProgress> Progress => _uow.Repository<VwOrderLineProgress>().Query();

    private sealed record LineRow(int OrderLineId, int OrderId, string OrderCode, string OrderStatus, DateOnly DueDate, int ProductId,
        string ProductCode, string ProductName, string Color, string SizeCode, int OrderedQty)
    {
        public string Label => string.Format(Messages.OrderLineLabel, ProductCode, Color, SizeCode);
    }

    private static IQueryable<LineRow> ToRows(IQueryable<OrderLine> lines) => lines.Select(l => new LineRow(l.OrderLineId, l.OrderId,
        l.Order.OrderCode, l.Order.Status, l.Order.DueDate, l.ProductId, l.ProductVariant.Product.ProductCode,
        l.ProductVariant.Product.ProductName, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode, l.OrderedQty));

    public async Task<Result<PagedResult<OrderLineTeamDto>>> GetPagedAsync(OrderLineTeamQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var allotments = Allotments;
        var filtered = Lines.AsNoTracking()
            .Where(l => !OrderStatuses.Closed.Contains(l.Order.Status))
            .Where(l => string.IsNullOrEmpty(keyword) || l.Order.OrderCode.Contains(keyword)
                        || l.ProductVariant.Product.ProductCode.Contains(keyword) || l.ProductVariant.Product.ProductName.Contains(keyword)
                        || l.ProductVariant.ProductColor.Color.Contains(keyword) || l.ProductVariant.SizeCode.Contains(keyword))
            .Where(l => !query.OnlyUnallotted || !allotments.Any(a => a.OrderLineId == l.OrderLineId))
            .OrderBy(l => l.Order.DueDate).ThenBy(l => l.OrderId).ThenBy(l => l.ProductVariant.Product.ProductCode)
            .ThenBy(l => l.ProductVariant.ProductColor.Color).ThenBy(l => l.ProductVariant.SizeCode);
        var page = await ToRows(filtered).ToPagedAsync(query, ct);

        var lineIds = page.Items.Select(r => r.OrderLineId).ToList();
        var teams = await Allotments.AsNoTracking().Where(a => lineIds.Contains(a.OrderLineId))
            .OrderBy(a => a.SewingTeam.TeamName)
            .Select(a => new { a.OrderLineId, a.SewingTeamId, a.SewingTeam.TeamName }).ToListAsync(ct);
        var items = page.Items.Select(r => new OrderLineTeamDto(r.OrderLineId, r.OrderId, r.OrderCode, r.DueDate, r.ProductId, r.ProductCode,
            r.ProductName, r.Color, r.SizeCode, r.OrderedQty,
            teams.Where(t => t.OrderLineId == r.OrderLineId).Select(t => new AllottedTeamDto(t.SewingTeamId, t.TeamName)).ToList())).ToList();
        return Result<PagedResult<OrderLineTeamDto>>.Success(new PagedResult<OrderLineTeamDto>(items, page.Page, page.PageSize, page.TotalItems));
    }

    public async Task<Result<IReadOnlyList<AllottedTeamDto>>> ListTeamsAsync(int orderId, int productId, CancellationToken ct)
    {
        var lineIds = Lines.Where(l => l.OrderId == orderId && l.ProductId == productId).Select(l => l.OrderLineId);
        var teams = await Allotments.AsNoTracking()
            .Where(a => lineIds.Contains(a.OrderLineId) && a.SewingTeam.Status == RecordStatus.Active)
            .Select(a => new { a.SewingTeamId, a.SewingTeam.TeamName })
            .Distinct().OrderBy(t => t.TeamName).ToListAsync(ct);
        return Result<IReadOnlyList<AllottedTeamDto>>.Success(teams.Select(t => new AllottedTeamDto(t.SewingTeamId, t.TeamName)).ToList());
    }

    public async Task<Result> SetTeamsAsync(SetOrderLineTeamsDto dto, CancellationToken ct)
    {
        var lines = await LoadLinesAsync([dto.OrderLineId], ct);
        if (CheckLines(lines, 1) is { } badLine) return badLine;
        var line = lines[0];

        var current = await Allotments.Where(a => a.OrderLineId == dto.OrderLineId).ToListAsync(ct);
        var added = dto.TeamIds.Except(current.Select(a => a.SewingTeamId)).ToList();
        var removed = current.Where(a => !dto.TeamIds.Contains(a.SewingTeamId)).ToList();
        if (added.Count == 0 && removed.Count == 0) return Result.Success();

        var invalid = await CheckNewTeamsAsync(added, ct)
                      ?? await CheckTeamOrderGateAsync(added, line.OrderId, ct)
                      ?? await CheckRemovableAsync(line, removed, ct);
        if (invalid is not null) return invalid;

        _uow.Repository<OrderLineTeam>().RemoveRange(removed);
        await _uow.Repository<OrderLineTeam>().AddRangeAsync(added.Select(teamId => NewRow(line.OrderLineId, teamId)).ToList());
        _audit.Add(new AuditEntry(nameof(OrderLineTeam), line.OrderLineId, AuditActions.Update,
            new { TeamIds = current.Select(a => a.SewingTeamId).ToList() }, new { dto.TeamIds }));
        await NotifyAsync(added.Select(teamId => (line, teamId)), ct);
        await _uow.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Chia các dòng đã chọn cho thêm tổ; tổ đang được chia giữ nguyên. Trả về số lượt chia mới.</summary>
    public async Task<Result<int>> AddTeamsAsync(AddOrderLineTeamsDto dto, CancellationToken ct)
    {
        var lines = await LoadLinesAsync(dto.OrderLineIds, ct);
        if (CheckLines(lines, dto.OrderLineIds.Count) is { } badLine) return Fail.As<int>(badLine);
        if (await CheckNewTeamsAsync(dto.TeamIds, ct) is { } badTeam) return Fail.As<int>(badTeam);

        var current = await Allotments.AsNoTracking().Where(a => dto.OrderLineIds.Contains(a.OrderLineId))
            .Select(a => new { a.OrderLineId, a.SewingTeamId }).ToListAsync(ct);
        var taken = current.Select(a => (a.OrderLineId, a.SewingTeamId)).ToHashSet();
        var added = lines.SelectMany(l => dto.TeamIds.Where(t => !taken.Contains((l.OrderLineId, t))).Select(teamId => (line: l, teamId))).ToList();
        if (added.Count == 0) return Result<int>.Success(0);

        foreach (var group in added.GroupBy(a => (a.teamId, a.line.OrderId)))
        {
            if (await CheckTeamOrderGateAsync([group.Key.teamId], group.Key.OrderId, ct) is { } busy)
                return Fail.As<int>(busy);
        }

        await _uow.Repository<OrderLineTeam>().AddRangeAsync(added.Select(a => NewRow(a.line.OrderLineId, a.teamId)).ToList());
        foreach (var group in added.GroupBy(a => a.line.OrderLineId))
        {
            var before = current.Where(a => a.OrderLineId == group.Key).Select(a => a.SewingTeamId).ToList();
            _audit.Add(new AuditEntry(nameof(OrderLineTeam), group.Key, AuditActions.Update,
                new { TeamIds = before }, new { TeamIds = before.Concat(group.Select(a => a.teamId)).ToList() }));
        }
        await NotifyAsync(added, ct);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(added.Count);
    }

    private static OrderLineTeam NewRow(int orderLineId, int teamId) => new() { OrderLineId = orderLineId, SewingTeamId = teamId };

    private async Task<List<LineRow>> LoadLinesAsync(IReadOnlyCollection<int> orderLineIds, CancellationToken ct) =>
        await ToRows(Lines.AsNoTracking().Where(l => orderLineIds.Contains(l.OrderLineId))).ToListAsync(ct);

    private static Result? CheckLines(IReadOnlyCollection<LineRow> lines, int expected)
    {
        if (lines.Count != expected) return Fail.NotFound();
        return lines.Any(l => OrderStatuses.Closed.Contains(l.OrderStatus))
            ? Result.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid)
            : null;
    }

    private async Task<Result?> CheckNewTeamsAsync(IReadOnlyCollection<int> added, CancellationToken ct)
    {
        if (added.Count == 0) return null;
        var valid = await _uow.Repository<Team>().Query().AsNoTracking()
            .CountAsync(t => added.Contains(t.TeamId) && t.TeamType == TeamTypes.Sewing && t.Status == RecordStatus.Active, ct);
        return valid == added.Count ? null : Result.Failure(ErrorCodes.OrderLineTeamInvalid, Messages.OrderLineTeamTeamInvalid);
    }

    /// <summary>
    /// Tổ chỉ nhận đơn mới khi mọi đơn mở khác đang chia cho tổ đã đạt % nhập kho TP / SL đặt (hoặc đã đóng).
    /// </summary>
    private async Task<Result?> CheckTeamOrderGateAsync(IReadOnlyCollection<int> teamIds, int newOrderId, CancellationToken ct)
    {
        if (teamIds.Count == 0) return null;
        var gate = await _settings.GetIntAsync(SettingKeys.TeamOrderProgressGatePct, DefaultProgressGatePct, ct);

        foreach (var teamId in teamIds.Distinct())
        {
            var otherOrderIds = await Allotments.AsNoTracking()
                .Where(a => a.SewingTeamId == teamId && a.OrderLine.OrderId != newOrderId
                            && !OrderStatuses.Closed.Contains(a.OrderLine.Order.Status))
                .Select(a => a.OrderLine.OrderId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var orderId in otherOrderIds)
            {
                var (orderCode, percent) = await GetOrderWarehouseProgressAsync(orderId, ct);
                if (percent >= gate) continue;
                return Result.Failure(ErrorCodes.TeamOrderBusy,
                    string.Format(Messages.TeamOrderBusy, orderCode, percent.ToString("0.#"), gate));
            }
        }

        return null;
    }

    /// <summary>% = Σ FinishedReceivedQty / Σ OrderedQty toàn đơn (vw_OrderLineProgress).</summary>
    private async Task<(string OrderCode, decimal Percent)> GetOrderWarehouseProgressAsync(int orderId, CancellationToken ct)
    {
        var rows = await Progress.AsNoTracking().Where(v => v.OrderId == orderId)
            .Select(v => new { v.OrderCode, v.OrderedQty, v.FinishedReceivedQty }).ToListAsync(ct);
        if (rows.Count == 0)
        {
            var code = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
                .Where(o => o.OrderId == orderId).Select(o => o.OrderCode).FirstOrDefaultAsync(ct) ?? orderId.ToString();
            return (code, 0m);
        }

        var ordered = rows.Sum(r => r.OrderedQty);
        var received = rows.Sum(r => r.FinishedReceivedQty);
        var percent = ordered <= 0 ? 0m : Math.Round(100m * received / ordered, 1, MidpointRounding.AwayFromZero);
        return (rows[0].OrderCode, percent);
    }

    /// <summary>Không bỏ được tổ đã có phiếu cấp BTP của dòng này, hoặc đây là dòng cuối của mã hàng mà tổ đã phân công, có chỉ tiêu KPI.</summary>
    private async Task<Result?> CheckRemovableAsync(LineRow line, IReadOnlyCollection<OrderLineTeam> removed, CancellationToken ct)
    {
        if (removed.Count == 0) return null;
        var teamIds = removed.Select(a => a.SewingTeamId).ToList();
        var issued = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.OrderLineId == line.OrderLineId && l.MaterialIssue.WarehouseType == WarehouseTypes.SemiFinished
                        && l.MaterialIssue.Status != IssueStatuses.Rejected && teamIds.Contains(l.MaterialIssue.ReceivingTeamId))
            .Select(l => l.MaterialIssue.ReceivingTeamId).ToListAsync(ct);

        var otherLineIds = Lines.Where(l => l.OrderId == line.OrderId && l.ProductId == line.ProductId && l.OrderLineId != line.OrderLineId)
            .Select(l => l.OrderLineId);
        var stillOnProduct = await Allotments.AsNoTracking()
            .Where(a => otherLineIds.Contains(a.OrderLineId) && teamIds.Contains(a.SewingTeamId))
            .Select(a => a.SewingTeamId).ToListAsync(ct);
        var withWork = await TeamsWithWorkOnProductAsync(line, teamIds.Except(stillOnProduct).ToList(), ct);

        var busy = issued.Concat(withWork).Distinct().ToList();
        if (busy.Count == 0) return null;
        var names = await _uow.Repository<Team>().Query().AsNoTracking().Where(t => busy.Contains(t.TeamId))
            .OrderBy(t => t.TeamName).Select(t => t.TeamName).ToListAsync(ct);
        return Result.Failure(ErrorCodes.OrderLineTeamInUse, string.Format(Messages.OrderLineTeamInUse, string.Join(", ", names)));
    }

    private async Task<List<int>> TeamsWithWorkOnProductAsync(LineRow line, IReadOnlyCollection<int> teamIds, CancellationToken ct)
    {
        if (teamIds.Count == 0) return [];
        var assigned = await _uow.Repository<OperationAssignment>().Query().AsNoTracking()
            .Where(a => a.OrderId == line.OrderId && a.ValidTo == null && a.Operation.ProductId == line.ProductId && teamIds.Contains(a.SewingTeamId))
            .Select(a => a.SewingTeamId).ToListAsync(ct);
        var withKpi = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .Where(k => k.OrderId == line.OrderId && k.ProductId == line.ProductId && k.Status != KpiStatuses.Cancelled && teamIds.Contains(k.SewingTeamId))
            .Select(k => k.SewingTeamId).ToListAsync(ct);
        return assigned.Concat(withKpi).ToList();
    }

    /// <summary>Mỗi tổ nhận một thông báo cho mỗi đơn, nêu tối đa vài dòng để nội dung không quá dài.</summary>
    private async Task NotifyAsync(IEnumerable<(LineRow line, int teamId)> added, CancellationToken ct)
    {
        foreach (var group in added.GroupBy(a => (a.teamId, a.line.OrderCode)))
        {
            var labels = group.Select(a => a.line.Label).ToList();
            var shown = string.Join(", ", labels.Take(MaxLinesInNotification)) + (labels.Count > MaxLinesInNotification ? "..." : "");
            await _notifier.NotifyTeamLeadersAsync(group.Key.teamId, new NotificationMessage(NotificationSources.Production,
                Messages.OrderLineTeamNotifyTitle, string.Format(Messages.OrderLineTeamNotifyBody, shown, group.Key.OrderCode),
                AssignmentLink), ct);
        }
    }
}
