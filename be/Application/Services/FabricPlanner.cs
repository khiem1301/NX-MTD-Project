using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Vải cần cho lệnh cắt (từ định mức UC09), vải tổ cắt đang giữ và vải đã cấp theo lệnh.</summary>
public sealed class FabricPlanner
{
    private readonly IUnitOfWork _uow;

    public FabricPlanner(IUnitOfWork uow) => _uow = uow;

    /// <summary>Chỉ tính NVL loại vải; phụ liệu cấp cho tổ may, không thuộc lệnh cắt.</summary>
    public async Task<IReadOnlyList<FabricNeed>> GetNeedsAsync(int orderId, IEnumerable<PieceDemand> demands,
        CancellationToken ct)
    {
        var orderMaterials = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(om => om.OrderId == orderId && om.Material.MaterialType == MaterialTypes.Fabric)
            .Include(om => om.Material).Include(om => om.OrderMaterialNorms)
            .ToListAsync(ct);
        var byId = orderMaterials.ToDictionary(om => om.OrderMaterialId);
        var norms = orderMaterials.SelectMany(OrderMaterialService.ToNormLines).ToList();

        return FabricRequirementCalculator.Calculate(norms, demands)
            .Select(n => (Om: byId[n.OrderMaterialId], n.Qty))
            .Select(x => new FabricNeed(x.Om.OrderMaterialId, x.Om.MaterialId, x.Om.Material.MaterialCode,
                x.Om.Material.MaterialName, x.Om.Material.Unit, x.Om.MaterialColor, x.Qty))
            .ToList();
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
