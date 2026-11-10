using NX_MTD.Application.Services;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Giải bài toán ghép bìa (DEV-ALL-88): số lớp của từng bìa cho ít mét nhất, bằng mét thì ít dư → ít loại bìa → ít lớp → theo mã.
/// Bản chính dùng Google.OrTools (Infrastructure); <see cref="BranchAndBoundMarkerSolver"/> là bản dự phòng không cần thư viện ngoài.
/// </summary>
public interface IMarkerPlanSolver
{
    /// <summary>Luôn trả một phương án đủ hàng cho bài toán hợp lệ (<see cref="MarkerProblem.Create"/> khác null).</summary>
    MarkerPlan Solve(MarkerProblem problem);
}
