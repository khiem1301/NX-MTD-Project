namespace NX_MTD.Application.Services;

/// <summary>Một bìa của đơn để chọn khi ước vải: mã, chiều dài (m), số sản phẩm mỗi size trên một lớp.</summary>
public sealed record MarkerOption(string Code, decimal LengthM, IReadOnlyDictionary<string, short> Ratio);

/// <summary>Bìa được chọn: số lớp, số mét = số lớp × chiều dài, số sản phẩm dư so với nhu cầu (tính cả size lệnh không cần).</summary>
public sealed record MarkerChoice(string Code, int Plies, decimal Meters, int Surplus);

/// <summary>Số miếng chi tiết cần cắt lẻ / cắt bù ở một size; PiecesPerGarment = số chi tiết trên một sản phẩm.</summary>
public sealed record PieceNeed(string SizeCode, int Pieces, short PiecesPerGarment);

/// <summary>
/// Ước mét vải theo bìa của đơn (DEV-ALL-85). Cắt cả bộ: số lớp = max theo size của ⌈cần ÷ số sản phẩm size đó trên 1 lớp⌉,
/// mét = số lớp × chiều dài; chọn bìa có đủ size cần, ít mét nhất, rồi dư ít nhất, rồi theo mã. Cắt lẻ / cắt bù: số miếng trên 1 lớp
/// = số sản phẩm size đó × số chi tiết trên 1 sản phẩm, tối thiểu một lớp (một chiều dài bìa). Không kết hợp nhiều bìa.
/// </summary>
public static class MarkerFabricEstimator
{
    public static MarkerChoice? ChooseWhole(IReadOnlyDictionary<string, int> need, IEnumerable<MarkerOption> markers)
    {
        var sizes = need.Where(n => n.Value > 0).ToList();
        if (sizes.Count == 0) return null;
        return markers
            .Where(m => sizes.All(s => m.Ratio.ContainsKey(s.Key)))
            .Select(m =>
            {
                var plies = sizes.Max(s => Ceil(s.Value, m.Ratio[s.Key]));
                var surplus = m.Ratio.Sum(r => plies * r.Value - need.GetValueOrDefault(r.Key));
                return new MarkerChoice(m.Code, plies, plies * m.LengthM, surplus);
            })
            .OrderBy(c => c.Meters).ThenBy(c => c.Surplus).ThenBy(c => c.Code, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static MarkerChoice? ChooseExtra(IReadOnlyList<PieceNeed> need, IEnumerable<MarkerOption> markers)
    {
        var pieces = need.Where(n => n.Pieces > 0).ToList();
        if (pieces.Count == 0) return null;
        return markers
            .Where(m => pieces.All(p => m.Ratio.ContainsKey(p.SizeCode)))
            .Select(m =>
            {
                var plies = Math.Max(1, pieces.Max(p => Ceil(p.Pieces, m.Ratio[p.SizeCode] * Math.Max((short)1, p.PiecesPerGarment))));
                return new MarkerChoice(m.Code, plies, plies * m.LengthM, 0);
            })
            .OrderBy(c => c.Meters).ThenBy(c => c.Code, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int Ceil(int need, int perPly) => (need + perPly - 1) / perPly;
}
