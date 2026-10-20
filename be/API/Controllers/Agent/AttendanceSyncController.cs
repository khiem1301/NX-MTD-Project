using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.API.Filters;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Agent;

/// <summary>
/// UC61 — endpoint cho agent console đọc RJData.mdb trên máy cài Ronald Jack Pro.
/// Xác thực bằng khóa tĩnh, không phải JWT của nhân viên.
/// </summary>
[ApiController]
[Route("api/v1/agent/attendance-sync")]
[AllowAnonymous]
[AgentApiKey]
public sealed class AttendanceSyncController : ControllerBase
{
    private readonly IAttendanceService _attendance;

    public AttendanceSyncController(IAttendanceService attendance) => _attendance = attendance;

    /// <summary>Mốc đọc tiếp: agent lấy CheckInOut có ID lớn hơn giá trị này.</summary>
    [HttpGet("cursor")]
    public async Task<IActionResult> GetCursor(CancellationToken ct) =>
        this.ToResponse(await _attendance.GetCursorAsync(ct));

    /// <summary>Nhận một lô lượt quẹt. Gọi lại cùng lô không sinh bản ghi trùng (GBR-16).</summary>
    [HttpPost("punches")]
    public async Task<IActionResult> Ingest([FromBody] IngestPunchesDto dto, CancellationToken ct) =>
        this.ToResponse(await _attendance.IngestAsync(dto, SyncSource.RjSync, ct));
}
