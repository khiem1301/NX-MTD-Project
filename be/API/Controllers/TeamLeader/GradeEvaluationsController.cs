using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC50: tổ trưởng / tổ phó đề xuất bậc thợ cho công nhân của tổ mình và xem kết quả.</summary>
public sealed class GradeEvaluationsController : TeamLeaderBaseController
{
    private readonly IGradeService _grades;
    private readonly ICurrentUser _user;

    public GradeEvaluationsController(IGradeService grades, ICurrentUser user)
    {
        _grades = grades; _user = user;
    }

    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] GradeEvaluationQuery query, CancellationToken ct) =>
        this.ToResponse(await _grades.GetPagedAsync(query, TeamId, ct));

    [HttpGet("workers")]
    public async Task<IActionResult> GetWorkers(CancellationToken ct) =>
        this.ToResponse(await _grades.GetTeamWorkersAsync(TeamId, ct));

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] ProposeGradeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.ProposeAsync(dto, TeamId, ct));
}
