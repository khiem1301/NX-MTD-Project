using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC28 — tổ trưởng / tổ phó chỉ xem bảng công của tổ mình (GBR-01).</summary>
public sealed class AttendanceController : TeamLeaderBaseController
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _user;

    public AttendanceController(IAttendanceService attendance, ICurrentUser user)
    {
        _attendance = attendance;
        _user = user;
    }

    [HttpGet("timesheets")]
    public async Task<IActionResult> GetTimesheets([FromQuery] TimesheetQuery query, CancellationToken ct)
    {
        if (_user.TeamId is not { } teamId) return NoTeam();
        return this.ToResponse(await _attendance.GetTimesheetsAsync(query, TimesheetScope.ForTeam(teamId), ct));
    }

    [HttpGet("timesheets/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] TimesheetQuery query, CancellationToken ct)
    {
        if (_user.TeamId is not { } teamId) return NoTeam();
        return this.ToResponse(await _attendance.GetSummaryAsync(query, TimesheetScope.ForTeam(teamId), ct));
    }

    private IActionResult NoTeam() =>
        this.ToResponse(Result.Failure(ErrorCodes.AuthForbidden, "Tài khoản chưa được gán tổ.", ResultKind.Forbidden));
}
