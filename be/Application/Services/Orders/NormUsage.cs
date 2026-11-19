using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Dòng định mức NVL đã được chứng từ nào dùng chưa: dùng chung cho lưu định mức và nhập Excel chế độ thay thế.</summary>
internal static class NormUsage
{
    /// <summary>Dòng định mức đã được dùng ở lệnh cắt, đơn từ, bàn cắt, bàn giao, phiếu cấp hoặc dòng tồn kho BTP.</summary>
    public static async Task<HashSet<int>> UsedIdsAsync(IUnitOfWork uow, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var used = new HashSet<int>();
        if (ids.Count == 0) return used;
        var idArray = ids.ToArray();
        used.UnionWith(await uow.Repository<WorkOrderLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await uow.Repository<RequestLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await uow.Repository<CuttingLayLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await uow.Repository<CutHandoverLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await uow.Repository<MaterialIssueLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await uow.Repository<StockBalance>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        return used;
    }
}
