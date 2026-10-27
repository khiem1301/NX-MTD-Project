namespace NX_MTD.Application.Services;

/// <summary>Một dòng định mức: 1 sản phẩm (theo chi tiết, màu, size) cần QtyPerPiece NVL, cộng % hao hụt.</summary>
public sealed record NormLine(int OrderMaterialId, int MaterialId, string MaterialColor, int ProductId,
    string ComponentName, string? GarmentColor, string? SizeCode, decimal QtyPerPiece, decimal WastagePercent);

/// <summary>Số sản phẩm cần làm của một dòng đơn; PanelName rỗng = cả sản phẩm, khác rỗng = chỉ chi tiết đó (cắt bù).</summary>
public sealed record PieceDemand(int ProductId, string Color, string SizeCode, string PanelName, int Qty);

public sealed record MaterialNeed(int OrderMaterialId, int MaterialId, string MaterialColor, decimal Qty);

/// <summary>
/// Tính NVL cần từ định mức (UC09 / UC19). Với mỗi NVL và mỗi chi tiết, chọn dòng định mức khớp cụ thể nhất:
/// đúng cả màu + size &gt; đúng màu &gt; đúng size &gt; dòng chung (màu, size để trống).
/// </summary>
public static class FabricRequirementCalculator
{
    private const int QtyDecimals = 3;
    private static readonly StringComparer Text = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<MaterialNeed> Calculate(IReadOnlyCollection<NormLine> norms,
        IEnumerable<PieceDemand> demands)
    {
        var totals = new Dictionary<int, MaterialNeed>();
        foreach (var demand in demands.Where(d => d.Qty > 0))
        {
            var chosen = norms
                .Where(n => n.ProductId == demand.ProductId && Matches(n, demand))
                .GroupBy(n => (n.OrderMaterialId, Component: n.ComponentName.Trim().ToUpperInvariant()))
                .Select(g => g.OrderByDescending(Specificity).First());

            foreach (var norm in chosen)
            {
                var qty = demand.Qty * norm.QtyPerPiece * (1 + norm.WastagePercent / 100m);
                totals[norm.OrderMaterialId] = totals.TryGetValue(norm.OrderMaterialId, out var acc)
                    ? acc with { Qty = acc.Qty + qty }
                    : new MaterialNeed(norm.OrderMaterialId, norm.MaterialId, norm.MaterialColor, qty);
            }
        }
        return totals.Values
            .Select(n => n with { Qty = Math.Round(n.Qty, QtyDecimals, MidpointRounding.AwayFromZero) })
            .OrderBy(n => n.OrderMaterialId).ToList();
    }

    /// <summary>Các dòng cần làm mà không có dòng định mức nào khớp; <see cref="Calculate"/> bỏ qua chúng (tính 0 vải).</summary>
    public static IReadOnlyList<PieceDemand> FindUncovered(IReadOnlyCollection<NormLine> norms,
        IEnumerable<PieceDemand> demands) =>
        demands.Where(d => d.Qty > 0 && !norms.Any(n => n.ProductId == d.ProductId && Matches(n, d))).ToList();

    private static bool Matches(NormLine norm, PieceDemand demand) =>
        (string.IsNullOrWhiteSpace(norm.GarmentColor) || Text.Equals(norm.GarmentColor.Trim(), demand.Color.Trim()))
        && (string.IsNullOrWhiteSpace(norm.SizeCode) || Text.Equals(norm.SizeCode.Trim(), demand.SizeCode.Trim()))
        && (string.IsNullOrWhiteSpace(demand.PanelName) || Text.Equals(norm.ComponentName.Trim(), demand.PanelName.Trim()));

    private static int Specificity(NormLine norm) =>
        (string.IsNullOrWhiteSpace(norm.GarmentColor) ? 0 : 2) + (string.IsNullOrWhiteSpace(norm.SizeCode) ? 0 : 1);
}
