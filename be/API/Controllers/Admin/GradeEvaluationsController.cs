using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC15 / UC16: Admin xem đề xuất bậc thợ và quyết định áp dụng hoặc không áp dụng (có lý do).</summary>
public sealed class GradeEvaluationsController : AdminBaseController
{
    private readonly IGradeService _grades;

    public GradeEvaluationsController(IGradeService grades) => _grades = grades;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] GradeEvaluationQuery query, CancellationToken ct) =>
        this.ToResponse(await _grades.GetPagedAsync(query, null, ct));

    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, [FromBody] ApplyGradeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.ApplyAsync(id, dto, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.RejectAsync(id, dto, ct));
}
