using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Thủ kho: NVL chờ nhận, lệnh cắt chưa cấp vải, đơn xin chờ cấp và các phiếu bàn giao chờ nhận.</summary>
    public async Task<Result<WarehouseHomeDto>> GetWarehouseAsync(CancellationToken ct)
    {
        var (count, incoming) = await IncomingItemsAsync(ct);
        var requests = await _requests.ListInboxAsync(new RequestQuery { Page = 1, PageSize = 1 }, ct);
        var cut = _uow.Repository<CutHandover>().Query().AsNoTracking().Where(h => h.Status == HandoverStatuses.Pending);
        var fg = _uow.Repository<FgHandover>().Query().AsNoTracking().Where(h => h.Status == HandoverStatuses.Pending);
        var returns = _uow.Repository<MaterialReturn>().Query().AsNoTracking().Where(r => r.Status == HandoverStatuses.Pending);
        var pending = await PendingItemsAsync(cut, fg, returns, ct);
        return Result<WarehouseHomeDto>.Success(new WarehouseHomeDto(
            count, await WorkOrdersNoIssueAsync(ct), requests.IsSuccess ? requests.Data!.TotalItems : 0,
            await cut.CountAsync(ct), await fg.CountAsync(ct), await returns.CountAsync(ct), incoming, pending));
    }

    /// <summary>Dòng NVL chờ nhận (đơn mua + khách cấp): tổng số dòng và 3 dòng đầu.</summary>
    private async Task<(int Count, IReadOnlyList<HomeItemDto> Items)> IncomingItemsAsync(CancellationToken ct)
    {
        Result<IncomingMaterialsDto> result;
        try
        {
            result = await _incoming.GetAsync(ct);
        }
        catch (Exception)
        {
            return (0, []);
        }
        if (!result.IsSuccess || result.Data is null) return (0, []);
        var d = result.Data!;
        var items = d.Supplier.Select(s => new HomeItemDto(
                $"{s.PoCode} · {s.MaterialCode} {s.OutstandingQty:0.##} {s.Unit}", s.SupplierName, "NCC", "blue"))
            .Concat(d.Customer.Select(c => new HomeItemDto(
                $"{c.OrderCode} · {c.MaterialCode} {c.OutstandingQty:0.##} {c.Unit}", null, "Khách cấp", "green")))
            .Take(3).ToList();
        return (d.Supplier.Count + d.Customer.Count, items);
    }

    /// <summary>Lệnh cắt đang mở chưa có phiếu cấp vải nào (không tính phiếu bị từ chối).</summary>
    private Task<int> WorkOrdersNoIssueAsync(CancellationToken ct)
    {
        var issues = _uow.Repository<MaterialIssue>().Query().AsNoTracking().Where(i => i.Status != IssueStatuses.Rejected);
        return _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Where(w => (w.Status == WorkOrderStatuses.Open || w.Status == WorkOrderStatuses.Cutting)
                        && !issues.Any(i => i.WorkOrderId == w.WorkOrderId))
            .CountAsync(ct);
    }

    /// <summary>Các phiếu bàn giao chờ kho nhận: mỗi loại tối đa 2 phiếu mới nhất.</summary>
    private static async Task<IReadOnlyList<HomeItemDto>> PendingItemsAsync(
        IQueryable<CutHandover> cut, IQueryable<FgHandover> fg, IQueryable<MaterialReturn> returns, CancellationToken ct)
    {
        const int PerKind = 2;
        var cuts = await cut.OrderByDescending(h => h.CutHandoverId).Take(PerKind)
            .Select(h => new { h.HandoverCode, Order = h.WorkOrder.Order.OrderCode }).ToListAsync(ct);
        var fgs = await fg.OrderByDescending(h => h.FgHandoverId).Take(PerKind)
            .Select(h => new { h.HandoverCode, Order = h.Order.OrderCode }).ToListAsync(ct);
        var rets = await returns.OrderByDescending(r => r.MaterialReturnId).Take(PerKind)
            .Select(r => new { r.ReturnCode, Team = r.ReturningTeam.TeamCode }).ToListAsync(ct);
        return cuts.Select(h => new HomeItemDto(h.HandoverCode, h.Order, "BTP chờ nhận", "orange"))
            .Concat(fgs.Select(h => new HomeItemDto(h.HandoverCode, h.Order, "TP chờ nhận", "orange")))
            .Concat(rets.Select(r => new HomeItemDto(r.ReturnCode, r.Team, "Trả NVL", "gray"))).ToList();
    }
}
