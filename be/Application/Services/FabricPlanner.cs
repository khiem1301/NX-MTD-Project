using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Vải cần cho lệnh cắt (từ định mức theo mã hàng UC10), vải tổ cắt đang giữ và vải đã cấp theo lệnh.</summary>
public sealed class FabricPlanner
{
    private readonly IUnitOfWork _uow;

    public FabricPlanner(IUnitOfWork uow) => _uow = uow;

    /// <summary>Chỉ tính chi tiết PANEL và NVL loại vải; phụ liệu (TRIM, chỉ, khóa…) cấp cho tổ may, không thuộc lệnh cắt.</summary>
    public async Task<IReadOnlyList<FabricNeed>> GetNeedsAsync(IEnumerable<PieceDemand> demands, CancellationToken ct)
    {
        var list = demands.ToList();
        var variantIds = list.Select(d => d.ProductVariantId).Distinct().ToArray();
        var rows = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => variantIds.Contains(p.ProductVariantId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new
            {
                Norm = new NormLine(p.ProductComponentMaterialId, p.ProductVariantId, p.ProductComponentId, p.ProductComponent.PiecesPerGarment, p.MaterialId,
                    p.QtyPerGarment, p.WastagePercent),
                p.Material.MaterialCode, p.Material.MaterialName, p.Material.Unit, p.Material.Color,
            })
            .ToListAsync(ct);
        var info = rows.GroupBy(r => r.Norm.MaterialId).ToDictionary(g => g.Key, g => g.First());

        return FabricRequirementCalculator.Calculate(rows.Select(r => r.Norm).ToList(), list)
            .Select(n => new FabricNeed(n.MaterialId, info[n.MaterialId].MaterialCode, info[n.MaterialId].MaterialName,
                info[n.MaterialId].Unit, info[n.MaterialId].Color, n.Qty))
            .ToList();
    }

    /// <summary>Số NVL cần của cả đơn theo định mức (mọi chi tiết, mọi loại NVL) — đối chiếu với số khai ở "NVL của đơn".</summary>
    public async Task<Dictionary<int, decimal>> GetOrderNormAsync(int orderId, CancellationToken ct)
    {
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == orderId)
            .Select(l => new PieceDemand(l.ProductVariantId, null, l.OrderedQty)).ToListAsync(ct);
        var variantIds = lines.Select(d => d.ProductVariantId).Distinct().ToArray();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => variantIds.Contains(p.ProductVariantId))
            .Select(p => new NormLine(p.ProductComponentMaterialId, p.ProductVariantId, p.ProductComponentId, p.ProductComponent.PiecesPerGarment, p.MaterialId,
                p.QtyPerGarment, p.WastagePercent))
            .ToListAsync(ct);
        return FabricRequirementCalculator.Calculate(norms, lines).ToDictionary(n => n.MaterialId, n => n.Qty);
    }

    /// <summary>Vải tổ cắt đang giữ của đơn = đã nhận (phiếu cấp RECEIVED) − đã trả về kho (phiếu trả CONFIRMED).</summary>
    public async Task<decimal> GetTeamHoldingAsync(int cuttingTeamId, int orderId, int materialId,
        CancellationToken ct)
    {
        var received = await _uow.Repository<MaterialIssueLine>().Query()
            .Where(l => l.MaterialIssue.ReceivingTeamId == cuttingTeamId && l.MaterialIssue.OrderId == orderId
                        && l.MaterialIssue.WarehouseType == WarehouseTypes.Material
                        && l.MaterialIssue.Status == IssueStatuses.Received
                        && l.MaterialId == materialId)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        var returned = await _uow.Repository<MaterialReturnLine>().Query()
            .Where(r => r.MaterialReturn.Status == HandoverStatuses.Confirmed
                        && r.MaterialIssueLine.MaterialIssue.ReceivingTeamId == cuttingTeamId
                        && r.MaterialIssueLine.MaterialIssue.OrderId == orderId
                        && r.MaterialIssueLine.MaterialId == materialId)
            .SumAsync(r => r.ConfirmedQty ?? 0, ct);
        return received - returned;
    }

    /// <summary>Vải đã cấp theo lệnh cắt: phiếu chờ nhận tính số cấp, phiếu đã nhận tính số xác nhận.
    /// <paramref name="receivedOnly"/> = true chỉ tính phiếu tổ cắt đã nhận.</summary>
    public async Task<Dictionary<int, decimal>> GetIssuedAsync(int workOrderId,
        CancellationToken ct, bool receivedOnly = false)
    {
        var rows = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.WorkOrderId == workOrderId && l.MaterialId != null
                        && l.MaterialIssue.Status != IssueStatuses.Rejected
                        && (!receivedOnly || l.MaterialIssue.Status == IssueStatuses.Received))
            .Select(l => new
            {
                MaterialId = l.MaterialId!.Value,
                Qty = l.MaterialIssue.Status == IssueStatuses.Received ? l.ConfirmedQty ?? 0 : l.IssuedQty,
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.MaterialId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
    }

    /// <summary>
    /// Vải các lệnh cắt khác (OPEN / CUTTING) đã "giữ chỗ": kho chỉ trừ phiếu cấp đang chờ nhận nên lệnh cắt chưa có phiếu cấp
    /// vẫn chưa giữ gì. <c>Outstanding</c> = Σ (cần − đã cấp) theo NVL; <c>TeamReceived</c> = vải tổ này đã nhận cho các lệnh khác
    /// cùng đơn (đã nằm trong số tổ đang giữ nên không được dùng lại cho lệnh mới).
    /// </summary>
    public async Task<FabricCommitments> GetCommitmentsAsync(int cuttingTeamId, int orderId, int? exceptWorkOrderId,
        CancellationToken ct)
    {
        var open = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
            .Where(w => (w.Status == WorkOrderStatuses.Open || w.Status == WorkOrderStatuses.Cutting)
                        && (exceptWorkOrderId == null || w.WorkOrderId != exceptWorkOrderId))
            .ToListAsync(ct);

        var outstanding = new Dictionary<int, decimal>();
        var teamReceived = new Dictionary<int, decimal>();
        foreach (var wo in open)
        {
            var demands = wo.WorkOrderLines
                .Select(l => new PieceDemand(l.OrderLine.ProductVariantId, l.ProductComponentMaterialId, l.PlannedQty)).ToList();
            var needs = await GetNeedsAsync(demands, ct);
            var issued = await GetIssuedAsync(wo.WorkOrderId, ct);
            foreach (var n in needs)
                outstanding[n.MaterialId] = outstanding.GetValueOrDefault(n.MaterialId)
                                            + Math.Max(0, n.RequiredQty - issued.GetValueOrDefault(n.MaterialId));

            if (wo.CuttingTeamId != cuttingTeamId || wo.OrderId != orderId) continue;
            foreach (var (materialId, qty) in await GetIssuedAsync(wo.WorkOrderId, ct, receivedOnly: true))
                teamReceived[materialId] = teamReceived.GetValueOrDefault(materialId) + qty;
        }
        return new FabricCommitments(outstanding, teamReceived);
    }

    /// <summary>Biến thể (màu - size) chưa có chi tiết PANEL dùng NVL vải trong định mức: không tính được vải cần.</summary>
    public async Task<HashSet<int>> GetVariantsWithFabricNormAsync(IEnumerable<int> variantIds, CancellationToken ct)
    {
        var ids = variantIds.Distinct().ToArray();
        var withNorm = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => ids.Contains(p.ProductVariantId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => p.ProductVariantId).Distinct().ToListAsync(ct);
        return withNorm.ToHashSet();
    }
}

/// <summary>Xem <see cref="FabricPlanner.GetCommitmentsAsync"/>.</summary>
public sealed record FabricCommitments(IReadOnlyDictionary<int, decimal> Outstanding, IReadOnlyDictionary<int, decimal> TeamReceived);
