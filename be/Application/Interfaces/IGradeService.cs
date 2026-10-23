using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC15 / UC16 / UC50: mức bậc thợ, đề xuất bậc của tổ trưởng, quyết định của Admin / Quản đốc.</summary>
public interface IGradeService
{
    Task<Result<IReadOnlyList<GradeLevelDto>>> GetLevelsAsync(CancellationToken ct);
    Task<Result<GradeLevelDto>> AddLevelAsync(SaveGradeLevelDto dto, CancellationToken ct);

    Task<Result<IReadOnlyList<GradeWorkerDto>>> GetTeamWorkersAsync(int teamId, CancellationToken ct);

    /// <param name="teamId">Có giá trị = chỉ đề xuất của tổ này; null = mọi tổ (Admin / Quản đốc).</param>
    Task<Result<PagedResult<GradeEvaluationDto>>> GetPagedAsync(GradeEvaluationQuery query, int? teamId, CancellationToken ct);

    Task<Result<GradeEvaluationDto>> ProposeAsync(ProposeGradeDto dto, int teamId, CancellationToken ct);
    Task<Result> ApplyAsync(int evaluationId, ApplyGradeDto dto, CancellationToken ct);
    Task<Result> RejectAsync(int evaluationId, ReasonDto dto, CancellationToken ct);
}
