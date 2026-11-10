using NX_MTD.Application.Interfaces;

namespace NX_MTD.Application.Services;

/// <summary>
/// Giải ghép bìa bằng duyệt nhánh có cận dưới (DEV-ALL-88), không cần thư viện ngoài: phương án ban đầu bằng cách tham lam, rồi lần lượt
/// chọn số lớp của từng bìa, bỏ nhánh khi cận dưới của tổng mét đã không thể tốt hơn. Chắc chắn tối ưu khi duyệt hết trong
/// giới hạn số nút (mặc định <see cref="MaxSearchNodes"/>); nhóm nhiều bìa mà chạm giới hạn thì trả phương án tốt nhất đã tìm được (IsProvenOptimal = false).
/// </summary>
public sealed class BranchAndBoundMarkerSolver : IMarkerPlanSolver
{
    /// <summary>Giới hạn số nút duyệt cho một bài toán, để một nhóm rất nhiều bìa không làm treo yêu cầu.</summary>
    public const int MaxSearchNodes = 200_000;

    private readonly int _maxNodes;

    public BranchAndBoundMarkerSolver(int maxNodes = MaxSearchNodes) => _maxNodes = maxNodes;

    public MarkerPlan Solve(MarkerProblem problem)
    {
        var search = new Search(problem, _maxNodes);
        return search.Run();
    }

    private sealed class Search
    {
        private readonly MarkerProblem _problem;
        private readonly IReadOnlyList<MarkerRequirement> _requirements;
        private readonly IReadOnlyList<MarkerCandidate> _candidates;
        private readonly int[] _plies;
        private readonly int _maxNodes;
        private MarkerPlan _best = null!;
        private int _nodes;

        public Search(MarkerProblem problem, int maxNodes)
        {
            _maxNodes = maxNodes;
            _problem = problem;
            _requirements = problem.Requirements;
            _candidates = problem.Candidates;
            _plies = new int[_candidates.Count];
        }

        public MarkerPlan Run()
        {
            _best = Greedy();
            Visit(0, _requirements.Select(r => r.Need).ToArray(), 0m);
            return _nodes > _maxNodes ? _best with { IsProvenOptimal = false } : _best;
        }

        private void Visit(int index, int[] remaining, decimal meters)
        {
            if (++_nodes > _maxNodes) return;
            if (remaining.All(r => r <= 0))
            {
                var plan = _problem.Plan(_plies, isProvenOptimal: true);
                if (MarkerProblem.IsBetter(plan, _best)) _best = plan;
                return;
            }
            if (index == _candidates.Count || LowerBound(index, remaining, meters) > _best.Meters) return;

            var marker = _candidates[index];
            var max = MaxUseful(marker, remaining);
            // Bìa cuối không cần thử: trải đúng số lớp đủ bù phần còn thiếu (thiếu size bìa này không có thì nhánh không có lời giải).
            var min = index == _candidates.Count - 1 ? max : 0;
            for (var plies = max; plies >= min; plies--)
            {
                _plies[index] = plies;
                Visit(index + 1, After(marker, plies, remaining), meters + plies * marker.LengthM);
            }
            _plies[index] = 0;
        }

        /// <summary>Số lớp nhiều nhất còn có ích: đủ bù điều kiện thiếu nhiều nhất mà bìa này cắt được.</summary>
        private int MaxUseful(MarkerCandidate marker, int[] remaining) =>
            _requirements.Select((r, i) => remaining[i] > 0 && marker.Per(r) > 0 ? MarkerProblem.Ceil(remaining[i], marker.Per(r)) : 0).Max();

        /// <summary>Cận dưới: với mỗi điều kiện còn thiếu, số thiếu × mét rẻ nhất cho một đơn vị trong các bìa còn lại; lấy lớn nhất.</summary>
        private decimal LowerBound(int index, int[] remaining, decimal meters)
        {
            var bound = 0m;
            for (var i = 0; i < _requirements.Count; i++)
            {
                if (remaining[i] <= 0) continue;
                var requirement = _requirements[i];
                var perUnit = _candidates.Skip(index).Where(c => c.Per(requirement) > 0)
                    .Select(c => c.LengthM / c.Per(requirement)).ToList();
                if (perUnit.Count == 0) return decimal.MaxValue;
                bound = Math.Max(bound, remaining[i] * perUnit.Min());
            }
            return meters + bound;
        }

        private int[] After(MarkerCandidate marker, int plies, int[] remaining) =>
            _requirements.Select((r, i) => remaining[i] - plies * marker.Per(r)).ToArray();

        /// <summary>Phương án ban đầu: lặp thêm một lớp của bìa có mét trên mỗi đơn vị còn thiếu thấp nhất, đến khi đủ.</summary>
        private MarkerPlan Greedy()
        {
            var plies = new int[_candidates.Count];
            var remaining = _requirements.Select(r => r.Need).ToArray();
            while (remaining.Any(r => r > 0))
            {
                var pick = Enumerable.Range(0, _candidates.Count)
                    .Select(j => (Index: j, Useful: UsefulUnits(_candidates[j], remaining)))
                    .Where(x => x.Useful > 0)
                    .MinBy(x => _candidates[x.Index].LengthM / x.Useful).Index;
                plies[pick]++;
                remaining = After(_candidates[pick], 1, remaining);
            }
            return _problem.Plan(plies, isProvenOptimal: true);
        }

        /// <summary>Số đơn vị còn thiếu mà một lớp bìa này bù được.</summary>
        private int UsefulUnits(MarkerCandidate marker, int[] remaining) =>
            _requirements.Select((r, i) => Math.Min(Math.Max(remaining[i], 0), marker.Per(r))).Sum();
    }
}
