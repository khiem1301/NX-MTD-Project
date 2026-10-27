using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC15 / UC16: Quản đốc xem đề xuất bậc thợ và quyết định áp dụng hoặc không áp dụng (có lý do).</summary>
public sealed class GradeEvaluationsController : ManagerBaseController
{
    private readonly IGradeService _grades;

    public GradeEvaluationsController(IGradeService grades) => _grades = grades;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] GradeEvaluationQuery query, CancellationToken ct) =>
        this.ToResponse(await _grades.GetPagedAsync(query, null, ct));

    /// <summary>Công nhân đang làm việc của mọi tổ để chọn khi đặt bậc trực tiếp.</summary>
    [HttpGet("workers")]
    public async Task<IActionResult> GetWorkers(CancellationToken ct) =>
        this.ToResponse(await _grades.GetAllWorkersAsync(ct));

    /// <summary>Đặt bậc trực tiếp cho công nhân, không cần tổ trưởng đề xuất trước.</summary>
    [HttpPost("set")]
    public async Task<IActionResult> SetGrade([FromBody] SetGradeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.SetGradeAsync(dto, ct));

    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, [FromBody] ApplyGradeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.ApplyAsync(id, dto, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.RejectAsync(id, dto, ct));
}
