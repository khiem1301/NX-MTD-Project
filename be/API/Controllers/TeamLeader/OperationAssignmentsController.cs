using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC52 (KI-03) — tổ trưởng / tổ phó may phân công công nhân × công đoạn; đổi phân công giữ lịch sử.</summary>
public sealed class OperationAssignmentsController : TeamLeaderBaseController
{
    private readonly IOperationAssignmentService _assignments;

    public OperationAssignmentsController(IOperationAssignmentService assignments) => _assignments = assignments;

    [HttpGet]
    public async Task<IActionResult> ListCurrent([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _assignments.ListCurrentAsync(orderId, ct));

    [HttpGet("operations")]
    public async Task<IActionResult> ListOperations([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _assignments.ListOperationsAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Assign([FromBody] AssignOperationDto dto, CancellationToken ct) =>
        this.ToResponse(await _assignments.AssignAsync(dto, ct));

    /// <summary>Một công nhân làm nhiều công đoạn: phân công một lần.</summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> AssignMany([FromBody] AssignOperationsDto dto, CancellationToken ct) =>
        this.ToResponse(await _assignments.AssignManyAsync(dto, ct));

    [HttpPost("{id:int}/replace")]
    public async Task<IActionResult> Replace(int id, [FromBody] ReplaceAssignmentDto dto, CancellationToken ct) =>
        this.ToResponse(await _assignments.ReplaceAsync(id, dto, ct));

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct) =>
        this.ToResponse(await _assignments.CloseAsync(id, ct));

    [HttpPost("copy")]
    public async Task<IActionResult> Copy([FromBody] CopyAssignmentsDto dto, CancellationToken ct) =>
        this.ToResponse(await _assignments.CopyAsync(dto, ct));
}
