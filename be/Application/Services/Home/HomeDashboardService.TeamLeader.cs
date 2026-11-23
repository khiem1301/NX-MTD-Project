using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Tổ trưởng / tổ phó: trang chủ của tổ cắt hoặc tổ may tùy loại tổ đang đăng nhập.</summary>
    public async Task<Result<TeamLeaderHomeDto>> GetTeamLeaderAsync(CancellationToken ct)
    {
        var teamId = _user.TeamId;
        var type = _user.TeamTypeCode;
        if (teamId is not { } id) return Result<TeamLeaderHomeDto>.Success(new TeamLeaderHomeDto(type, null, null));
        return type switch
        {
            TeamTypes.Cutting => Result<TeamLeaderHomeDto>.Success(new TeamLeaderHomeDto(type, await CuttingAsync(id, ct), null)),
            TeamTypes.Sewing => Result<TeamLeaderHomeDto>.Success(new TeamLeaderHomeDto(type, null, await SewingAsync(id, ct))),
            _ => Result<TeamLeaderHomeDto>.Success(new TeamLeaderHomeDto(type, null, null)),
        };
    }

    // ---------- Tổ cắt ----------

    private async Task<CuttingHomeDto> CuttingAsync(int teamId, CancellationToken ct)
    {
        var issues = await IssuesPendingAsync(teamId, null, ct);
        var progress = await _uow.Repository<VwWorkOrderLineProgress>().Query().AsNoTracking()
            .Where(v => v.CuttingTeamId == teamId && v.WorkOrderStatus != WorkOrderStatuses.Cancelled)
            .Select(v => new { v.WorkOrderId, v.OrderLineId, v.PlannedQty, v.CutQty, v.HandedToWarehouseQty }).ToListAsync(ct);
        // Một dòng đơn có thể lặp theo từng vải / chi tiết: lấy số lớn nhất của dòng đó để không cộng trùng.
        var perLine = progress.GroupBy(v => (v.WorkOrderId, v.OrderLineId))
            .Select(g => new
            {
                g.Key.WorkOrderId,
                Planned = g.Max(x => x.PlannedQty),
                Cut = g.Max(x => x.CutQty),
                Handed = g.Max(x => x.HandedToWarehouseQty)
            }).ToList();
        var notHanded = perLine.Sum(l => Math.Max(0, l.Cut - l.Handed));
        return new CuttingHomeDto(issues.Count, notHanded, await LeftoverNotReturnedAsync(teamId, ct),
            await OpenWorkOrdersAsync(teamId, perLine.Select(l => (l.WorkOrderId, l.Planned, l.Cut)).ToList(), ct));
    }

    /// <summary>Phiếu cấp đang chờ tổ nhận (lọc theo loại kho nếu có): số phiếu và mã phiếu.</summary>
    private async Task<(int Count, IReadOnlyList<MaterialIssue> Issues)> IssuesPendingAsync(int teamId, string? warehouseType, CancellationToken ct)
    {
        var issues = await _uow.Repository<MaterialIssue>().Query().AsNoTracking()
            .Where(i => i.ReceivingTeamId == teamId && i.Status == IssueStatuses.PendingReceipt
                        && (warehouseType == null || i.WarehouseType == warehouseType))
            .OrderByDescending(i => i.MaterialIssueId).ToListAsync(ct);
        return (issues.Count, issues);
    }

    /// <summary>Lệnh cắt đã hoàn thành của tổ có phiếu cấp vải đã nhận nhưng chưa có phiếu trả vải thừa nào.</summary>
    private Task<int> LeftoverNotReturnedAsync(int teamId, CancellationToken ct)
    {
        var issues = _uow.Repository<MaterialIssue>().Query().AsNoTracking();
        var returns = _uow.Repository<MaterialReturn>().Query().AsNoTracking().Where(r => r.Status != HandoverStatuses.Rejected);
        return _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Where(w => w.CuttingTeamId == teamId && w.Status == WorkOrderStatuses.Completed
                        && issues.Any(i => i.WorkOrderId == w.WorkOrderId && i.Status == IssueStatuses.Received)
                        && !returns.Any(r => r.WorkOrderId == w.WorkOrderId))
            .CountAsync(ct);
    }

    private async Task<IReadOnlyList<HomeWorkOrderDto>> OpenWorkOrdersAsync(
        int teamId, IReadOnlyList<(int WorkOrderId, int Planned, int Cut)> lines, CancellationToken ct)
    {
        var open = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Where(w => w.CuttingTeamId == teamId && (w.Status == WorkOrderStatuses.Open || w.Status == WorkOrderStatuses.Cutting))
            .OrderByDescending(w => w.WorkOrderId)
            .Select(w => new { w.WorkOrderId, w.WorkOrderCode, Order = w.Order.OrderCode, w.Status }).ToListAsync(ct);
        return open.Select(w => new HomeWorkOrderDto(w.WorkOrderId, w.WorkOrderCode, w.Order, CodeText.ToPascal(w.Status),
            lines.Where(l => l.WorkOrderId == w.WorkOrderId).Sum(l => l.Planned),
            lines.Where(l => l.WorkOrderId == w.WorkOrderId).Sum(l => l.Cut))).ToList();
    }

    // ---------- Tổ may ----------

    private async Task<SewingHomeDto> SewingAsync(int teamId, CancellationToken ct)
    {
        var today = _clock.Today;
        var kpi = await _uow.Repository<VwKpiDailyResult>().Query().AsNoTracking()
            .Where(k => k.SewingTeamId == teamId && k.WorkDate == today)
            .Select(k => new { k.DailyTarget, k.FinishedQty }).ToListAsync(ct);
        var btp = await IssuesPendingAsync(teamId, WarehouseTypes.SemiFinished, ct);
        var (items, unassigned) = await BtpIssueItemsAsync(teamId, btp.Issues, ct);
        var allocations = await _uow.Repository<WorkAllocation>().Query().AsNoTracking()
            .CountAsync(a => a.SewingTeamId == teamId && a.Status == HandoverStatuses.Pending, ct);
        var meal = await _meals.GetTeamSuggestionAsync(teamId, today, ct);
        return new SewingHomeDto(kpi.Sum(k => k.DailyTarget ?? 0), kpi.Sum(k => k.FinishedQty ?? 0), btp.Count, unassigned,
            allocations, meal.IsSuccess ? meal.Data!.SuggestedCount : 0, meal.IsSuccess ? meal.Data!.MealCount : null,
            meal.IsSuccess && meal.Data!.IsLocked, items);
    }

    /// <summary>
    /// Phiếu BTP chờ nhận: phiếu nào còn công đoạn chưa phân công thì bị chặn nhận (cùng điều kiện với bước xác nhận phiếu cấp).
    /// Trả các dòng hiển thị và số cặp (đơn, công đoạn) chưa phân công.
    /// </summary>
    private async Task<(IReadOnlyList<HomeItemDto> Items, int Unassigned)> BtpIssueItemsAsync(
        int teamId, IReadOnlyList<MaterialIssue> issues, CancellationToken ct)
    {
        var items = new List<HomeItemDto>();
        var blocked = new HashSet<(int OrderId, int OperationId)>();
        foreach (var issue in issues)
        {
            var missing = await MissingOperationsAsync(issue.MaterialIssueId, issue.OrderId, teamId, ct);
            foreach (var op in missing) blocked.Add((issue.OrderId, op));
            items.Add(missing.Count > 0
                ? new HomeItemDto(issue.IssueCode, $"Chặn nhận: còn {missing.Count} công đoạn chưa phân công", "Chặn", "red")
                : new HomeItemDto(issue.IssueCode, null, "Chờ nhận", "orange"));
        }
        return (items, blocked.Count);
    }

    private async Task<IReadOnlyList<int>> MissingOperationsAsync(int issueId, int orderId, int teamId, CancellationToken ct)
    {
        var lineIds = _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssueId == issueId && l.OrderLineId != null).Select(l => l.OrderLineId!.Value);
        var productIds = _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => lineIds.Contains(l.OrderLineId)).Select(l => l.ProductId).Distinct();
        var assignments = _uow.Repository<OperationAssignment>().Query().AsNoTracking();
        return await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => productIds.Contains(o.ProductId) && o.Status == OperationStatuses.Active && o.TeamType == TeamTypes.Sewing
                        && !assignments.Any(a => a.OperationId == o.OperationId && a.OrderId == orderId && a.SewingTeamId == teamId
                                                 && a.ValidTo == null && a.Worker.Status == RecordStatus.Active
                                                 && a.Worker.TeamId == teamId))
            .Select(o => o.OperationId).ToListAsync(ct);
    }
}
