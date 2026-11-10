using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Optimization;

namespace Application.Tests;

/// <summary>
/// Ước mét vải theo bìa của đơn (DEV-ALL-85, ghép bìa DEV-ALL-88): tìm số lớp xⱼ của từng bìa để mọi size đủ số cần và Σ dài × lớp nhỏ nhất;
/// bằng mét thì ít dư → ít loại bìa → ít lớp → theo mã; cắt lẻ tính theo miếng, tối thiểu một lớp.
/// Cùng bộ test chạy với cả hai bộ giải: duyệt nhánh (dự phòng) và Google.OrTools (bản chính).
/// </summary>
public abstract class MarkerFabricEstimatorTests
{
    protected abstract IMarkerPlanSolver Solver { get; }

    private static MarkerOption Marker(string code, decimal length, params (string Size, short Pieces)[] ratio) =>
        new(code, length, ratio.ToDictionary(r => r.Size, r => r.Pieces, StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, int> Need(params (string Size, int Qty)[] need) =>
        need.ToDictionary(n => n.Size, n => n.Qty, StringComparer.OrdinalIgnoreCase);

    private static string Uses(MarkerPlan plan) => string.Join(", ", plan.Uses.Select(u => $"{u.Code}×{u.Plies}"));

    [Fact]
    public void ChooseWhole_MatchingRatio_LayersEqualTheSetsNeeded()
    {
        var plan = MarkerFabricEstimator.ChooseWhole(Need(("S", 40), ("M", 80), ("L", 40)), [Marker("1A", 2.2m, ("S", 1), ("M", 2), ("L", 1))], Solver)!;

        (Uses(plan), plan.Meters, plan.Surplus).Should().Be(("1A×40", 88m, 0));
    }

    [Fact]
    public void ChooseWhole_OffRatio_TakesTheLargestNumberOfLayersAndCountsTheSurplus()
    {
        var plan = MarkerFabricEstimator.ChooseWhole(Need(("M", 30)), [Marker("2A", 5m, ("S", 1), ("M", 2), ("L", 1))], Solver)!;

        (Uses(plan), plan.Meters, plan.Surplus).Should().Be(("2A×15", 75m, 30));   // dư S15, L15
    }

    [Fact]
    public void ChooseWhole_UserExampleSml_CombinesMarkersForFewestMeters()
    {
        // Ví dụ của người dùng: S33, M100, L130; A = 1S 2L, B = 3M 1L, C = 1S 1M 1L; giả sử 0,7 m mỗi sản phẩm trên bìa.
        var plan = MarkerFabricEstimator.ChooseWhole(Need(("S", 33), ("M", 100), ("L", 130)),
        [
            Marker("A", 2.1m, ("S", 1), ("L", 2)),
            Marker("B", 2.8m, ("M", 3), ("L", 1)),
            Marker("C", 2.1m, ("S", 1), ("M", 1), ("L", 1)),
        ], Solver)!;

        (Uses(plan), plan.Meters, plan.Surplus).Should().Be(("A×48, B×33, C×1", 195.3m, 16));   // chỉ C: 130 lớp, 273 m
    }

    [Fact]
    public void ChooseWhole_MarkersCoveringSomeSizes_AreCombined()
    {
        var plan = MarkerFabricEstimator.ChooseWhole(Need(("S", 10), ("XL", 6)), [Marker("1A", 1m, ("S", 2)), Marker("1B", 1.5m, ("XL", 3))], Solver)!;

        (Uses(plan), plan.Meters).Should().Be(("1A×5, 1B×2", 8m));
    }

    [Fact]
    public void ChooseWhole_ManySizes_CoversEverySize()
    {
        var need = Need(("XS", 12), ("S", 40), ("M", 75), ("L", 64), ("XL", 30), ("2XL", 9));
        var markers = new[]
        {
            Marker("1A", 3.0m, ("XS", 1), ("S", 2), ("M", 2), ("L", 1)),
            Marker("1B", 3.4m, ("M", 2), ("L", 2), ("XL", 1)),
            Marker("1C", 2.6m, ("L", 1), ("XL", 2), ("2XL", 1)),
            Marker("1D", 1.9m, ("S", 1), ("M", 1), ("L", 1)),
        };

        var plan = MarkerFabricEstimator.ChooseWhole(need, markers, Solver)!;

        var cut = need.Keys.ToDictionary(s => s, s => plan.Uses.Sum(u => u.Plies * markers.Single(m => m.Code == u.Code).Ratio.GetValueOrDefault(s)));
        cut.Should().OnlyContain(c => c.Value >= need[c.Key]);
        plan.Meters.Should().Be(plan.Uses.Sum(u => u.Plies * markers.Single(m => m.Code == u.Code).LengthM));
        plan.Meters.Should().BeLessThan(CheapestSingleMarkerMeters(need, markers));
    }

    /// <summary>Số mét ít nhất khi chỉ dùng một bìa; không bìa nào có đủ mọi size thì là decimal.MaxValue.</summary>
    private decimal CheapestSingleMarkerMeters(Dictionary<string, int> need, IEnumerable<MarkerOption> markers) =>
        markers.Select(m => MarkerFabricEstimator.ChooseWhole(need, [m], Solver)?.Meters).Where(m => m is not null).Select(m => m!.Value)
            .DefaultIfEmpty(decimal.MaxValue).Min();

    [Fact]
    public void ChooseWhole_SameMeters_PrefersLessSurplusThenFewerTypesThenFewerLayersThenCode()
    {
        // ít dư: 1B (đúng M4) thắng 1A (M4 L1), cùng 2 m
        Uses(MarkerFabricEstimator.ChooseWhole(Need(("M", 4)), [Marker("1A", 2m, ("M", 4), ("L", 1)), Marker("1B", 2m, ("M", 4))], Solver)!)
            .Should().Be("1B×1");
        // ít loại bìa: 1C × 1 (M2 L2, 2 m) thắng 1A × 1 + 1B × 1 (cùng 2 m, cùng dư 0)
        Uses(MarkerFabricEstimator.ChooseWhole(Need(("M", 2), ("L", 2)),
                [Marker("1A", 1m, ("M", 2)), Marker("1B", 1m, ("L", 2)), Marker("1C", 2m, ("M", 2), ("L", 2))], Solver)!)
            .Should().Be("1C×1");
        // ít lớp: 1B × 1 (M4, 2 m) thắng 1A × 2 (M2, 1 m / lớp)
        Uses(MarkerFabricEstimator.ChooseWhole(Need(("M", 4)), [Marker("1A", 1m, ("M", 2)), Marker("1B", 2m, ("M", 4))], Solver)!)
            .Should().Be("1B×1");
        // theo mã
        Uses(MarkerFabricEstimator.ChooseWhole(Need(("M", 4)), [Marker("1B", 2m, ("M", 4)), Marker("1A", 2m, ("M", 4))], Solver)!)
            .Should().Be("1A×1");
    }

    [Fact]
    public void ChooseWhole_SizeNoMarkerHas_ReturnsNull()
    {
        MarkerFabricEstimator.ChooseWhole(Need(("S", 10), ("M", 10)), [Marker("1B", 2m, ("M", 4))], Solver).Should().BeNull();
        MarkerFabricEstimator.ChooseWhole(Need(("M", 0)), [Marker("1B", 2m, ("M", 4))], Solver).Should().BeNull();
    }

    [Fact]
    public void ChooseWhole_SizeCase_DoesNotMatter() =>
        MarkerFabricEstimator.ChooseWhole(Need(("m", 4)), [Marker("1A", 1m, ("M", 2))], Solver)!.Plies.Should().Be(2);

    [Theory]
    [InlineData(3, 1, 5.0)]    // 3 tay ÷ 4 tay / lớp → 1 lớp
    [InlineData(4, 1, 5.0)]
    [InlineData(5, 2, 10.0)]
    [InlineData(1, 1, 5.0)]    // tối thiểu một chiều dài bìa
    public void ChooseExtra_PiecesNeeded_PliesCountGarmentsTimesPiecesPerGarment(int pieces, int plies, double meters)
    {
        var plan = MarkerFabricEstimator.ChooseExtra([new PieceNeed("M", pieces, 2)], [Marker("2A", 5m, ("S", 1), ("M", 2), ("L", 1))], Solver)!;

        (plan.Plies, plan.Meters).Should().Be((plies, (decimal)meters));
    }

    [Fact]
    public void ChooseExtra_TwoMarkers_PicksFewestMeters()
    {
        // 6 tay M + 4 tay L (2 tay / áo): 2B × 1 (M3 L2 → 6 + 4 miếng, 3 m) rẻ hơn 2A × 3 (M1 L1, 1,2 m / lớp = 3,6 m)
        var plan = MarkerFabricEstimator.ChooseExtra([new PieceNeed("M", 6, 2), new PieceNeed("L", 4, 2)],
            [Marker("2A", 1.2m, ("M", 1), ("L", 1)), Marker("2B", 3m, ("M", 3), ("L", 2))], Solver)!;

        (Uses(plan), plan.Meters).Should().Be(("2B×1", 3m));
    }

    [Fact]
    public void ChooseExtra_SizeNotOnTheMarker_ReturnsNull() =>
        MarkerFabricEstimator.ChooseExtra([new PieceNeed("XL", 3, 1)], [Marker("2A", 5m, ("M", 2))], Solver).Should().BeNull();
}

public sealed class BranchAndBoundMarkerSolverTests : MarkerFabricEstimatorTests
{
    protected override IMarkerPlanSolver Solver { get; } = new BranchAndBoundMarkerSolver();
}

public sealed class OrToolsMarkerPlanSolverTests : MarkerFabricEstimatorTests
{
    protected override IMarkerPlanSolver Solver { get; } = new OrToolsMarkerPlanSolver(NullLogger<OrToolsMarkerPlanSolver>.Instance);

    /// <summary>Nhóm nhiều bìa, nhiều size (sinh ngẫu nhiên cố định): duyệt nhánh chạm giới hạn, OR-Tools vẫn chứng minh được tối ưu.</summary>
    [Theory]
    [InlineData(8, 8, 500)]
    [InlineData(12, 10, 800)]
    public void Solve_ManyMarkers_IsProvenOptimalAndNotWorseThanBranchAndBound(int markers, int sizes, int maxNeed)
    {
        var (need, options) = RandomGroup(markers, sizes, maxNeed);

        var plan = MarkerFabricEstimator.ChooseWhole(need, options, Solver)!;
        var fallback = MarkerFabricEstimator.ChooseWhole(need, options, new BranchAndBoundMarkerSolver())!;

        plan.IsProvenOptimal.Should().BeTrue();
        fallback.IsProvenOptimal.Should().BeFalse();
        plan.Meters.Should().BeLessThanOrEqualTo(fallback.Meters);
        need.Should().OnlyContain(n => plan.Uses.Sum(u => u.Plies * options.Single(o => o.Code == u.Code).Ratio.GetValueOrDefault(n.Key)) >= n.Value);
    }

    private static (Dictionary<string, int> Need, List<MarkerOption> Options) RandomGroup(int markers, int sizes, int maxNeed)
    {
        var random = new Random(markers * 100 + sizes);
        var codes = Enumerable.Range(0, sizes).Select(i => $"S{i}").ToArray();
        var need = codes.ToDictionary(s => s, _ => random.Next(maxNeed / 3, maxNeed));
        var options = Enumerable.Range(0, markers).Select(j =>
        {
            var ratio = codes.Where((_, i) => i == j % sizes || random.NextDouble() < 0.6).ToDictionary(s => s, _ => (short)random.Next(1, 4));
            return new MarkerOption($"M{j:00}", ratio.Values.Sum(v => v) * 0.7m, ratio);
        }).ToList();
        return (need, options);
    }
}
