using NX_MTD.Application.Interfaces;

namespace NX_MTD.Application.Services;

/// <summary>
/// Ước mét vải theo bìa của đơn (DEV-ALL-85), ghép nhiều bìa cho ít mét nhất (DEV-ALL-88). Mỗi bài toán là một nhóm bìa × một vải:
/// tìm số lớp nguyên xⱼ ≥ 0 của từng bìa j sao cho với mọi điều kiện (size s, hệ số f, số cần N): Σⱼ rⱼₛ · f · xⱼ ≥ N,
/// và tổng mét Σⱼ Dⱼ · xⱼ nhỏ nhất. Cắt cả bộ: f = 1, N = số sản phẩm size s. Cắt lẻ / cắt bù: f = số chi tiết trên một sản phẩm,
/// N = số miếng (tối thiểu một lớp). Bằng mét thì ít dư → ít loại bìa → ít lớp → theo mã bìa. Không giới hạn số loại bìa.
/// Dựng bài toán ở đây, giải bằng <see cref="IMarkerPlanSolver"/>.
/// </summary>
public static class MarkerFabricEstimator
{
    /// <summary>Cắt cả bộ: need = số sản phẩm mỗi size. Null khi không có nhu cầu hoặc có size không bìa nào có.</summary>
    public static MarkerPlan? ChooseWhole(IReadOnlyDictionary<string, int> need, IEnumerable<MarkerOption> markers, IMarkerPlanSolver solver) =>
        Solve(need.Where(n => n.Value > 0).Select(n => new MarkerRequirement(MarkerProblem.SizeKey(n.Key), n.Value, 1)).ToList(),
            markers, solver);

    /// <summary>Cắt lẻ / cắt bù: nhu cầu theo miếng; một lớp cắt được (số sản phẩm của size × số chi tiết trên một sản phẩm) miếng.</summary>
    public static MarkerPlan? ChooseExtra(IReadOnlyList<PieceNeed> need, IEnumerable<MarkerOption> markers, IMarkerPlanSolver solver) =>
        Solve(need.Where(n => n.Pieces > 0)
            .Select(n => new MarkerRequirement(MarkerProblem.SizeKey(n.SizeCode), n.Pieces, Math.Max(1, (int)n.PiecesPerGarment))).ToList(),
            markers, solver);

    private static MarkerPlan? Solve(List<MarkerRequirement> requirements, IEnumerable<MarkerOption> markers, IMarkerPlanSolver solver)
    {
        var problem = MarkerProblem.Create(requirements, markers);
        return problem is null ? null : solver.Solve(problem);
    }
}
