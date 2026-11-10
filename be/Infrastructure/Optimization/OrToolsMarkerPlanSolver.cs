using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;

namespace NX_MTD.Infrastructure.Optimization;

/// <summary>
/// Giải ghép bìa bằng Google.OrTools CP-SAT (DEV-ALL-88). Biến xⱼ = số lớp của bìa j (nguyên, 0..số lớp nhiều nhất còn có ích),
/// uⱼ = có dùng bìa j; ràng buộc mỗi điều kiện Σⱼ rⱼₛ · f · xⱼ ≥ N. Tối ưu lần lượt (mỗi bước giữ nguyên kết quả bước trước):
/// 1. tổng mét Σ Dⱼ · xⱼ (D tính bằng cm cho nguyên), 2. số dư, 3. số loại bìa Σ uⱼ, 4. tổng số lớp.
/// Nhóm nhỏ: duyệt nhánh với giới hạn nhỏ (<see cref="QuickSearchNodes"/>) đã chắc chắn tối ưu thì dùng luôn, khỏi gọi CP-SAT.
/// Bước 1 không chứng minh được tối ưu trong thời gian cho phép thì IsProvenOptimal = false; CP-SAT không ra phương án nào
/// (hoặc lỗi) thì dùng <see cref="BranchAndBoundMarkerSolver"/> đầy đủ. Phương án nào tốt hơn (<see cref="MarkerProblem.IsBetter"/>) thì lấy.
/// </summary>
public sealed class OrToolsMarkerPlanSolver : IMarkerPlanSolver
{
    /// <summary>Thời gian tối đa cho mỗi bước tối ưu (4 bước), để một yêu cầu kiểm vải không chờ quá lâu.</summary>
    public const double MaxSecondsPerStep = 1.0;
    private const int SearchWorkers = 8;
    private const long CentimetersPerMeter = 100;
    /// <summary>Giới hạn nút của lần duyệt nhánh thử nhanh trước khi gọi CP-SAT (nhóm 2–4 bìa duyệt hết trong giới hạn này).</summary>
    public const int QuickSearchNodes = 20_000;

    private readonly BranchAndBoundMarkerSolver _quick = new(QuickSearchNodes);
    private readonly BranchAndBoundMarkerSolver _fallback = new();
    private readonly ILogger<OrToolsMarkerPlanSolver> _logger;

    public OrToolsMarkerPlanSolver(ILogger<OrToolsMarkerPlanSolver> logger) => _logger = logger;

    public MarkerPlan Solve(MarkerProblem problem)
    {
        var quick = _quick.Solve(problem);
        if (quick.IsProvenOptimal) return quick;   // nhóm nhỏ: duyệt nhánh đã chắc chắn tối ưu, khỏi gọi CP-SAT
        try
        {
            var plan = SolveWithCpSat(problem);
            if (plan is not null) return MarkerProblem.IsBetter(quick, plan) ? quick : plan;
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            _logger.LogError(ex, "Không nạp được Google.OrTools, dùng bộ giải dự phòng.");
        }
        return _fallback.Solve(problem);
    }

    private static MarkerPlan? SolveWithCpSat(MarkerProblem problem)
    {
        var model = new CpModel();
        var candidates = problem.Candidates;
        var plies = candidates.Select((c, j) => model.NewIntVar(0, problem.MaxUsefulPlies(c), $"x{j}")).ToArray();
        var used = candidates.Select((_, j) => model.NewBoolVar($"u{j}")).ToArray();
        for (var j = 0; j < candidates.Count; j++)
        {
            model.Add(plies[j] <= used[j] * problem.MaxUsefulPlies(candidates[j]));
            model.Add(plies[j] >= used[j]);
        }
        foreach (var requirement in problem.Requirements)
            model.Add(LinearExpr.WeightedSum(plies, candidates.Select(c => (long)c.Per(requirement))) >= requirement.Need);

        var steps = new[]
        {
            LinearExpr.WeightedSum(plies, candidates.Select(c => (long)decimal.Round(c.LengthM * CentimetersPerMeter))),
            LinearExpr.WeightedSum(plies, candidates.Select(c => (long)problem.UnitsPerPly(c))),
            LinearExpr.Sum(used),
            LinearExpr.Sum(plies),
        };
        return Optimize(model, steps, plies, problem);
    }

    /// <summary>Tối ưu lần lượt từng mục tiêu; bước nào hết giờ chưa chứng minh được tối ưu thì dừng và giữ phương án đang có.</summary>
    private static MarkerPlan? Optimize(CpModel model, LinearExpr[] steps, IntVar[] plies, MarkerProblem problem)
    {
        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{MaxSecondsPerStep.ToString(System.Globalization.CultureInfo.InvariantCulture)} num_workers:{SearchWorkers}",
        };
        long[]? values = null;
        var provenOptimal = true;
        for (var step = 0; step < steps.Length; step++)
        {
            model.Minimize(steps[step]);
            var status = solver.Solve(model);
            if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible)) break;
            values = plies.Select(p => solver.Value(p)).ToArray();
            if (status != CpSolverStatus.Optimal)
            {
                provenOptimal &= step > 0;   // chỉ bước 1 (tổng mét) quyết định "ít mét nhất"
                break;
            }
            model.Add(steps[step] == (long)solver.ObjectiveValue);
            model.ClearHints();
            for (var j = 0; j < plies.Length; j++) model.AddHint(plies[j], values[j]);
        }
        return values is null ? null : problem.Plan(values.Select(v => (int)v).ToList(), provenOptimal);
    }
}
