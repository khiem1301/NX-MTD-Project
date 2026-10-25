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
                Norm = new NormLine(p.ProductVariantId, p.ProductComponentId, p.ProductComponent.PiecesPerGarment, p.MaterialId,
                    p.MaterialColor, p.QtyPerGarment, p.WastagePercent),
                p.Material.MaterialCode, p.Material.MaterialName, p.Material.Unit,
            })
            .ToListAsync(ct);
        var info = rows.GroupBy(r => r.Norm.MaterialId).ToDictionary(g => g.Key, g => g.First());

        return FabricRequirementCalculator.Calculate(rows.Select(r => r.Norm).ToList(), list)
            .Select(n => new FabricNeed(n.MaterialId, info[n.MaterialId].MaterialCode, info[n.MaterialId].MaterialName,
                info[n.MaterialId].Unit, n.MaterialColor, n.Qty))
            .ToList();
    }

    /// <summary>Số NVL cần của cả đơn theo định mức (mọi chi tiết, mọi loại NVL) — đối chiếu với số khai ở "NVL của đơn".</summary>
    public async Task<Dictionary<(int MaterialId, string Color), decimal>> GetOrderNormAsync(int orderId, CancellationToken ct)
    {
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == orderId)
            .Select(l => new PieceDemand(l.ProductVariantId, null, l.OrderedQty)).ToListAsync(ct);
        var variantIds = lines.Select(d => d.ProductVariantId).Distinct().ToArray();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => variantIds.Contains(p.ProductVariantId))
            .Select(p => new NormLine(p.ProductVariantId, p.ProductComponentId, p.ProductComponent.PiecesPerGarment, p.MaterialId,
                p.MaterialColor, p.QtyPerGarment, p.WastagePercent))
            .ToListAsync(ct);
        return FabricRequirementCalculator.Calculate(norms, lines).ToDictionary(n => (n.MaterialId, n.MaterialColor), n => n.Qty);
    }

    /// <summary>Vải tổ cắt đang giữ của đơn = đã nhận (phiếu cấp RECEIVED) − đã trả về kho (phiếu trả CONFIRMED).</summary>
    public async Task<decimal> GetTeamHoldingAsync(int cuttingTeamId, int orderId, int materialId, string color,
        CancellationToken ct)
    {
        var received = await _uow.Repository<MaterialIssueLine>().Query()
            .Where(l => l.MaterialIssue.ReceivingTeamId == cuttingTeamId && l.MaterialIssue.OrderId == orderId
                        && l.MaterialIssue.WarehouseType == WarehouseTypes.Material
                        && l.MaterialIssue.Status == IssueStatuses.Received
                        && l.MaterialId == materialId && l.MaterialColor == color)
            .SumAsync(l => l.ConfirmedQty ?? 0, ct);
        var returned = await _uow.Repository<MaterialReturnLine>().Query()
            .Where(r => r.MaterialReturn.Status == HandoverStatuses.Confirmed
                        && r.MaterialIssueLine.MaterialIssue.ReceivingTeamId == cuttingTeamId
                        && r.MaterialIssueLine.MaterialIssue.OrderId == orderId
                        && r.MaterialIssueLine.MaterialId == materialId && r.MaterialIssueLine.MaterialColor == color)
            .SumAsync(r => r.ConfirmedQty ?? 0, ct);
        return received - returned;
    }

    /// <summary>Vải đã cấp theo lệnh cắt: phiếu chờ nhận tính số cấp, phiếu đã nhận tính số xác nhận.</summary>
    public async Task<Dictionary<(int MaterialId, string Color), decimal>> GetIssuedAsync(int workOrderId,
        CancellationToken ct)
    {
        var rows = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.WorkOrderId == workOrderId && l.MaterialId != null
                        && l.MaterialIssue.Status != IssueStatuses.Rejected)
            .Select(l => new
            {
                MaterialId = l.MaterialId!.Value,
                Color = l.MaterialColor!,
                Qty = l.MaterialIssue.Status == IssueStatuses.Received ? l.ConfirmedQty ?? 0 : l.IssuedQty,
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => (r.MaterialId, r.Color)).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
    }
}
