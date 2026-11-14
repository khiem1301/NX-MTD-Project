using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Vải của lệnh cắt: NVL vải theo định mức mã hàng (UC10), vải tổ cắt đang giữ, vải đã cấp / đã nhận / đã trả theo lệnh.</summary>
public sealed class FabricPlanner
{
    private readonly IUnitOfWork _uow;

    public FabricPlanner(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Các NVL vải (chi tiết PANEL × NVL loại vải, theo định mức của mã hàng) mà các dòng lệnh cắt dùng. Dòng cả bộ lấy mọi
    /// vải thân của màu áo (định mức theo màu, DEV-ALL-87); dòng cắt bù chỉ lấy đúng dòng định mức của nó. Không còn số lượng theo định mức (DB 29/10).
    /// </summary>
    public async Task<IReadOnlyList<FabricInfo>> GetFabricsAsync(IEnumerable<FabricLine> lines, CancellationToken ct)
    {
        var list = lines.ToList();
        var colorIds = list.Select(d => d.ProductColorId).Distinct().ToArray();
        var rows = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => colorIds.Contains(p.ProductColorId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new
            {
                p.ProductComponentMaterialId,
                p.ProductColorId,
                p.MaterialId,
                p.Material.MaterialCode,
                p.Material.MaterialName,
                p.Material.Unit,
                p.Material.Color,
            })
            .ToListAsync(ct);
        return rows
            .Where(r => list.Any(d => d.ProductColorId == r.ProductColorId
                                      && (d.ProductComponentMaterialId == null || d.ProductComponentMaterialId == r.ProductComponentMaterialId)))
            .GroupBy(r => r.MaterialId)
            .Select(g => g.First())
            .Select(r => new FabricInfo(r.MaterialId, r.MaterialCode, r.MaterialName, r.Unit, r.Color))
            .OrderBy(f => f.MaterialCode).ThenBy(f => f.MaterialColor)
            .ToList();
    }

    /// <summary>Vải mỗi dòng cần (DEV-ALL-93): cả bộ = mọi vải chi tiết PANEL của màu áo; cắt bù / cắt lẻ = vải của dòng định mức.</summary>
    public async Task<IReadOnlyDictionary<FabricLine, IReadOnlyList<int>>> FabricIdsByLineAsync(IEnumerable<FabricLine> lines,
        CancellationToken ct)
    {
        var list = lines.Distinct().ToList();
        var colorIds = list.Select(d => d.ProductColorId).Distinct().ToArray();
        var rows = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => colorIds.Contains(p.ProductColorId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new { p.ProductComponentMaterialId, p.ProductColorId, p.MaterialId })
            .ToListAsync(ct);
        return list.ToDictionary(d => d, d => (IReadOnlyList<int>)rows
            .Where(r => r.ProductColorId == d.ProductColorId
                        && (d.ProductComponentMaterialId == null || d.ProductComponentMaterialId == r.ProductComponentMaterialId))
            .Select(r => r.MaterialId).Distinct().OrderBy(id => id).ToList());
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
                        && r.MaterialReturn.ReturningTeamId == cuttingTeamId
                        && r.MaterialReturn.OrderId == orderId
                        && r.MaterialId == materialId)
            .SumAsync(r => r.ConfirmedQty ?? 0, ct);
        return received - returned;
    }

    /// <summary>
    /// Vải tổ cắt đã cắt trên đơn, theo NVL: Σ số mét vải đã dùng của các bàn (DEV-ALL-90). Bàn cũ chưa có số mét (nhập trước khi có
    /// chiều dài bàn cắt) không cộng được nên đếm riêng để màn hình báo số đã cắt chưa đủ.
    /// </summary>
    public async Task<Dictionary<int, (decimal CutQty, int UnmeasuredLays)>> GetCutAsync(int cuttingTeamId, int orderId, CancellationToken ct)
    {
        var fabrics = await _uow.Repository<CuttingLayFabric>().Query().AsNoTracking()
            .Where(f => f.CuttingLay.CuttingTeamId == cuttingTeamId && f.CuttingLay.WorkOrder.OrderId == orderId)
            .Select(f => new { f.MaterialId, f.UsedM })
            .ToListAsync(ct);
        return fabrics.GroupBy(f => f.MaterialId).ToDictionary(g => g.Key, g =>
            (g.Sum(f => f.UsedM ?? 0m), g.Count(f => f.UsedM is null)));
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

    /// <summary>Vải tổ cắt đã nhận (phiếu cấp RECEIVED, số xác nhận) và đã trả lại kho (phiếu trả CONFIRMED) theo NVL của một lệnh.</summary>
    public async Task<(Dictionary<int, decimal> Received, Dictionary<int, decimal> Returned)> GetUsageAsync(int workOrderId,
        CancellationToken ct)
    {
        var received = (await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.WorkOrderId == workOrderId && l.MaterialId != null
                        && l.MaterialIssue.Status == IssueStatuses.Received)
            .Select(l => new { MaterialId = l.MaterialId!.Value, Qty = l.ConfirmedQty ?? 0 }).ToListAsync(ct))
            .GroupBy(r => r.MaterialId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        var returned = (await _uow.Repository<MaterialReturnLine>().Query().AsNoTracking()
            .Where(r => r.MaterialReturn.Status == HandoverStatuses.Confirmed
                        && r.MaterialReturn.WorkOrderId == workOrderId && r.MaterialId != null
                        && r.Material!.MaterialType != MaterialTypes.Scrap)
            .Select(r => new { MaterialId = r.MaterialId!.Value, Qty = r.ConfirmedQty ?? 0 }).ToListAsync(ct))
            .GroupBy(r => r.MaterialId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
        return (received, returned);
    }
}

/// <summary>Một dòng của lệnh cắt: màu áo của dòng đơn; ProductComponentMaterialId null = cả bộ, có giá trị = chỉ dòng định mức đó (cắt bù).</summary>
public sealed record FabricLine(int ProductColorId, int? ProductComponentMaterialId);

public sealed record FabricInfo(int MaterialId, string MaterialCode, string MaterialName, string Unit, string MaterialColor);
