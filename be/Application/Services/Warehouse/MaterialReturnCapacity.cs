using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Tính hàng tổ còn trả được và trần số trả (DEV-ALL-112). NVL tổ cắt: theo lệnh cắt (đã nhận của lệnh − đã cắt − đã trả).
/// NVL tổ may: tổng đã nhận ở mọi phiếu cấp − đã trả. BTP tổ may: theo dòng đơn (đã nhận − đã QC đạt − đã trả).
/// "Đã trả" gồm phiếu chờ (số tổ khai) và phiếu đã nhận (số kho đếm). NVL phế phẩm không có trần nên không nằm ở đây.
/// </summary>
public sealed class MaterialReturnCapacity
{
    /// <summary>BTP đếm theo bộ.</summary>
    public const string PieceUnit = "bộ";

    private readonly IUnitOfWork _uow;

    public MaterialReturnCapacity(IUnitOfWork uow) => _uow = uow;

    private IQueryable<MaterialIssueLine> ReceivedLines(int teamId, string warehouseType) =>
        _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.ReceivingTeamId == teamId && l.MaterialIssue.WarehouseType == warehouseType
                        && l.MaterialIssue.Status == IssueStatuses.Received);

    private IQueryable<MaterialReturnLine> ReturnedLines(int teamId, string warehouseType) =>
        _uow.Repository<MaterialReturnLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReturn.ReturningTeamId == teamId && l.MaterialReturn.WarehouseType == warehouseType
                        && (l.MaterialReturn.Status == HandoverStatuses.Pending || l.MaterialReturn.Status == HandoverStatuses.Confirmed));

    /// <summary>NVL tổ cắt có thể trả theo lệnh cắt: vải đã nhận theo lệnh, trừ số mét đã cắt (khi tính được) và số đã trả.</summary>
    public async Task<IReadOnlyList<ReturnableLineDto>> CuttingMaterialsAsync(int teamId, int workOrderId, CancellationToken ct)
    {
        var received = await ReceivedLines(teamId, WarehouseTypes.Material)
            .Where(l => l.MaterialIssue.WorkOrderId == workOrderId && l.MaterialId != null && l.Material!.MaterialType != MaterialTypes.Scrap)
            .GroupBy(l => l.MaterialId!.Value).Select(g => new { MaterialId = g.Key, Qty = g.Sum(l => l.ConfirmedQty ?? 0) })
            .ToDictionaryAsync(x => x.MaterialId, x => x.Qty, ct);
        var returned = await ReturnedMaterialsAsync(teamId, workOrderId, ct);
        var cut = (await _uow.Repository<CuttingLayFabric>().Query().AsNoTracking()
                .Where(f => f.CuttingLay.WorkOrderId == workOrderId).Select(f => new { f.MaterialId, f.UsedM }).ToListAsync(ct))
            .GroupBy(f => f.MaterialId)
            .ToDictionary(g => g.Key, g => g.Any(f => f.UsedM is null) ? (decimal?)null : g.Sum(f => f.UsedM ?? 0m));
        return await ToMaterialLinesAsync(received, returned, cut, ct);
    }

    /// <summary>NVL tổ may có thể trả: tổng đã nhận ở mọi phiếu cấp (không theo đơn) trừ số đã trả.</summary>
    public async Task<IReadOnlyList<ReturnableLineDto>> SewingMaterialsAsync(int teamId, CancellationToken ct)
    {
        var received = await ReceivedLines(teamId, WarehouseTypes.Material)
            .Where(l => l.MaterialId != null && l.Material!.MaterialType != MaterialTypes.Scrap)
            .GroupBy(l => l.MaterialId!.Value).Select(g => new { MaterialId = g.Key, Qty = g.Sum(l => l.ConfirmedQty ?? 0) })
            .ToDictionaryAsync(x => x.MaterialId, x => x.Qty, ct);
        var returned = await ReturnedMaterialsAsync(teamId, null, ct);
        return await ToMaterialLinesAsync(received, returned, new Dictionary<int, decimal?>(), ct);
    }

    private async Task<Dictionary<int, decimal>> ReturnedMaterialsAsync(int teamId, int? workOrderId, CancellationToken ct) =>
        (await ReturnedLines(teamId, WarehouseTypes.Material)
            .Where(l => l.MaterialId != null && l.Material!.MaterialType != MaterialTypes.Scrap
                        && (workOrderId == null || l.MaterialReturn.WorkOrderId == workOrderId))
            .Select(l => new
            {
                MaterialId = l.MaterialId!.Value,
                Qty = l.MaterialReturn.Status == HandoverStatuses.Confirmed ? l.ConfirmedQty ?? 0 : l.ReturnedQty,
            }).ToListAsync(ct))
        .GroupBy(r => r.MaterialId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));

    private async Task<IReadOnlyList<ReturnableLineDto>> ToMaterialLinesAsync(Dictionary<int, decimal> received,
        Dictionary<int, decimal> returned, Dictionary<int, decimal?> used, CancellationToken ct)
    {
        var ids = received.Keys.ToList();
        var materials = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => ids.Contains(m.MaterialId))
            .ToDictionaryAsync(m => m.MaterialId, ct);
        return ids.Select(id =>
        {
            var m = materials[id];
            var usedQty = used.GetValueOrDefault(id);
            var done = returned.GetValueOrDefault(id);
            var remaining = Math.Max(0, received[id] - (usedQty ?? 0) - done);
            return new ReturnableLineDto(id, null, m.MaterialCode, m.MaterialName, m.Unit, m.Color, received[id], usedQty, done, remaining);
        }).Where(l => l.RemainingQty > 0).OrderBy(l => l.ItemCode).ToList();
    }

    /// <summary>BTP tổ may có thể trả theo dòng đơn của một đơn: đã nhận (cả bộ) − đã QC đạt − đã trả.</summary>
    public async Task<IReadOnlyList<ReturnableLineDto>> SemiFinishedAsync(int teamId, int orderId, CancellationToken ct)
    {
        var received = await ReceivedLines(teamId, WarehouseTypes.SemiFinished)
            .Where(l => l.OrderLineId != null && l.ProductComponentMaterialId == null && l.OrderLine!.OrderId == orderId)
            .GroupBy(l => l.OrderLineId!.Value).Select(g => new { LineId = g.Key, Qty = g.Sum(l => l.ConfirmedQty ?? 0) })
            .ToDictionaryAsync(x => x.LineId, x => x.Qty, ct);
        var ids = received.Keys.ToList();
        var passed = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(b => b.SewingTeamId == teamId && ids.Contains(b.OrderLineId))
            .GroupBy(b => b.OrderLineId).Select(g => new { LineId = g.Key, Qty = g.Sum(b => b.FinishedQty) })
            .ToDictionaryAsync(x => x.LineId, x => (decimal)x.Qty, ct);
        var returned = (await ReturnedLines(teamId, WarehouseTypes.SemiFinished)
                .Where(l => l.OrderLineId != null && ids.Contains(l.OrderLineId.Value))
                .Select(l => new
                {
                    LineId = l.OrderLineId!.Value,
                    Qty = l.MaterialReturn.Status == HandoverStatuses.Confirmed ? l.ConfirmedQty ?? 0 : l.ReturnedQty,
                }).ToListAsync(ct))
            .GroupBy(r => r.LineId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        var labels = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => ids.Contains(l.OrderLineId))
            .Select(l => new { l.OrderLineId, l.ProductVariant.Product.ProductCode, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode })
            .ToDictionaryAsync(l => l.OrderLineId, ct);

        return ids.Select(id =>
        {
            var l = labels[id];
            var used = passed.GetValueOrDefault(id);
            var done = returned.GetValueOrDefault(id);
            var remaining = Math.Max(0, received[id] - used - done);
            return new ReturnableLineDto(null, id, l.ProductCode, $"{l.Color}/{l.SizeCode}", PieceUnit, l.Color, received[id], used, done, remaining);
        }).Where(l => l.RemainingQty > 0).OrderBy(l => l.ItemCode).ThenBy(l => l.ItemName).ToList();
    }
}
