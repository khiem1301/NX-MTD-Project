namespace NX_MTD.Application.Services;

/// <summary>Một dòng định mức của biến thể (ProductComponentMaterial): QtyPerGarment là tổng cho 1 sản phẩm, đã tính số chi tiết.</summary>
public sealed record NormLine(int ProductVariantId, int ProductComponentId, short PiecesPerGarment, int MaterialId,
    string MaterialColor, decimal QtyPerGarment, decimal WastagePercent);

/// <summary>Số sản phẩm cần làm của một biến thể (màu × size); ProductComponentId null = cả bộ, có giá trị = chỉ chi tiết đó (cắt bù).</summary>
public sealed record PieceDemand(int ProductVariantId, int? ProductComponentId, int Qty);

public sealed record MaterialNeed(int MaterialId, string MaterialColor, decimal Qty);

/// <summary>
/// Tính NVL cần từ định mức theo mã hàng (UC10 / UC19), cùng công thức với vw_OrderMaterialNorm và vw_WorkOrderFabricNeed:
/// số sản phẩm × định mức × (1 + hao hụt). Dòng cắt bù một chi tiết chỉ cần phần của chi tiết đó: chia cho số chi tiết / bộ.
/// </summary>
public static class FabricRequirementCalculator
{
    private const int QtyDecimals = 3;

    public static IReadOnlyList<MaterialNeed> Calculate(IReadOnlyCollection<NormLine> norms,
        IEnumerable<PieceDemand> demands)
    {
        var totals = new Dictionary<(int MaterialId, string Color), decimal>();
        foreach (var demand in demands.Where(d => d.Qty > 0))
        {
            foreach (var norm in norms.Where(n => n.ProductVariantId == demand.ProductVariantId
                         && (demand.ProductComponentId is null || n.ProductComponentId == demand.ProductComponentId)))
            {
                var pieces = demand.ProductComponentId is null ? 1m : Math.Max((short)1, norm.PiecesPerGarment);
                var qty = demand.Qty * norm.QtyPerGarment * (1 + norm.WastagePercent / 100m) / pieces;
                var key = (norm.MaterialId, norm.MaterialColor);
                totals[key] = totals.GetValueOrDefault(key) + qty;
            }
        }
        return totals
            .Select(t => new MaterialNeed(t.Key.MaterialId, t.Key.Color,
                Math.Round(t.Value, QtyDecimals, MidpointRounding.AwayFromZero)))
            .OrderBy(n => n.MaterialId).ThenBy(n => n.MaterialColor).ToList();
    }
}
