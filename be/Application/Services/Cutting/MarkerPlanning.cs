namespace NX_MTD.Application.Services;

/// <summary>Một bìa của đơn để chọn khi ước vải: mã, chiều dài (m), số sản phẩm mỗi size trên một lớp.</summary>
public sealed record MarkerOption(string Code, decimal LengthM, IReadOnlyDictionary<string, short> Ratio);

/// <summary>Trải bìa <c>Code</c> <c>Plies</c> lớp; LengthM, Ratio (số sản phẩm mỗi size trên một lớp) của bìa để hiển thị.</summary>
public sealed record MarkerUse(string Code, int Plies, decimal LengthM = 0, IReadOnlyDictionary<string, int>? Ratio = null)
{
    public decimal Meters => Plies * LengthM;
}

/// <summary>
/// Phương án trải bìa: các bìa dùng (theo mã), tổng mét, số dư (sản phẩm cắt cả bộ / miếng cắt lẻ) so với nhu cầu.
/// IsProvenOptimal = false khi bộ giải hết thời gian / số bước trước khi chứng minh được là ít mét nhất.
/// </summary>
public sealed record MarkerPlan(IReadOnlyList<MarkerUse> Uses, decimal Meters, int Surplus, bool IsProvenOptimal = true)
{
    public int Plies => Uses.Sum(u => u.Plies);
}

/// <summary>Số miếng chi tiết cần cắt lẻ / cắt bù ở một size; PiecesPerGarment = số chi tiết trên một sản phẩm.</summary>
public sealed record PieceNeed(string SizeCode, int Pieces, short PiecesPerGarment);

/// <summary>Một điều kiện của bài toán: Σⱼ rⱼₛ · Factor · xⱼ ≥ Need (Factor = số chi tiết trên một sản phẩm; cắt cả bộ = 1).</summary>
public sealed record MarkerRequirement(string SizeCode, int Need, int Factor);

/// <summary>Một bìa trong bài toán: mã, chiều dài (m), số sản phẩm mỗi size (khóa size đã chuẩn hóa) trên một lớp.</summary>
public sealed record MarkerCandidate(string Code, decimal LengthM, IReadOnlyDictionary<string, int> Ratio)
{
    /// <summary>Số đơn vị của điều kiện mà một lớp bìa này cắt được.</summary>
    public int Per(MarkerRequirement r) => Ratio.GetValueOrDefault(r.SizeCode) * r.Factor;
}

/// <summary>
/// Bài toán ghép bìa của một nhóm bìa × một vải (DEV-ALL-88): tìm số lớp nguyên xⱼ ≥ 0 của từng bìa sao cho mọi điều kiện đủ và
/// Σⱼ Dⱼ · xⱼ nhỏ nhất; bằng mét thì ít dư → ít loại bìa → ít lớp → theo mã bìa (<see cref="IsBetter"/>).
/// </summary>
public sealed record MarkerProblem(IReadOnlyList<MarkerRequirement> Requirements, IReadOnlyList<MarkerCandidate> Candidates)
{
    /// <summary>Dựng bài toán; null khi không có nhu cầu hoặc có size cần cắt mà không bìa nào có.</summary>
    public static MarkerProblem? Create(IReadOnlyList<MarkerRequirement> requirements, IEnumerable<MarkerOption> markers)
    {
        if (requirements.Count == 0) return null;
        var candidates = markers.Where(m => m.LengthM > 0)
            .Select(m => new MarkerCandidate(m.Code, m.LengthM, m.Ratio.ToDictionary(r => SizeKey(r.Key), r => (int)r.Value)))
            .Where(c => requirements.Any(r => c.Per(r) > 0))
            .OrderBy(c => c.Code, StringComparer.Ordinal).ToList();
        return requirements.Any(r => candidates.All(c => c.Per(r) == 0)) ? null : new MarkerProblem(requirements, candidates);
    }

    public static string SizeKey(string size) => size.Trim().ToUpperInvariant();

    public static int Ceil(int need, int perPly) => (need + perPly - 1) / perPly;

    /// <summary>Số lớp nhiều nhất còn có ích của một bìa: đủ bù điều kiện cần nhiều lớp nhất mà bìa đó cắt được.</summary>
    public int MaxUsefulPlies(MarkerCandidate marker) =>
        Requirements.Select(r => marker.Per(r) > 0 ? Ceil(r.Need, marker.Per(r)) : 0).Max();

    /// <summary>Số đơn vị một lớp bìa cắt ra (mọi size, nhân hệ số của size), dùng để tính số dư.</summary>
    public int UnitsPerPly(MarkerCandidate marker) => marker.Ratio.Sum(r => r.Value * FactorOf(r.Key));

    /// <summary>Tổng nhu cầu theo đơn vị, dùng để tính số dư.</summary>
    public int TotalNeed => Requirements.GroupBy(r => r.SizeCode).Sum(g => g.Max(r => r.Need));

    /// <summary>Số dư = mọi đơn vị cắt ra trừ nhu cầu, tính cả size không cần (cắt cả bộ: sản phẩm; cắt lẻ: miếng chi tiết).</summary>
    public int Surplus(IReadOnlyList<int> plies) =>
        Candidates.Select((c, i) => plies[i] * UnitsPerPly(c)).Sum() - TotalNeed;

    public MarkerPlan Plan(IReadOnlyList<int> plies, bool isProvenOptimal)
    {
        var uses = Candidates.Select((c, i) => new MarkerUse(c.Code, plies[i], c.LengthM, c.Ratio)).Where(u => u.Plies > 0).ToList();
        var meters = Candidates.Select((c, i) => plies[i] * c.LengthM).Sum();
        return new MarkerPlan(uses, meters, Surplus(plies), isProvenOptimal);
    }

    /// <summary>Ít mét → ít dư → ít loại bìa → ít lớp → theo mã bìa (so lần lượt từng bìa dùng, mã rồi số lớp).</summary>
    public static bool IsBetter(MarkerPlan a, MarkerPlan b)
    {
        if (a.Meters != b.Meters) return a.Meters < b.Meters;
        if (a.Surplus != b.Surplus) return a.Surplus < b.Surplus;
        if (a.Uses.Count != b.Uses.Count) return a.Uses.Count < b.Uses.Count;
        if (a.Plies != b.Plies) return a.Plies < b.Plies;
        foreach (var (x, y) in a.Uses.Zip(b.Uses))
        {
            var byCode = string.CompareOrdinal(x.Code, y.Code);
            if (byCode != 0) return byCode < 0;
            if (x.Plies != y.Plies) return x.Plies < y.Plies;
        }
        return false;
    }

    private int FactorOf(string size) =>
        Requirements.Where(r => r.SizeCode == size).Select(r => r.Factor).DefaultIfEmpty(1).Max();
}
