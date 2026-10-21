using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC58 — ngày công của tôi. Mọi vai trò đều xem được, nhưng chỉ dữ liệu của chính mình.</summary>
[Authorize]
public sealed class MyAttendanceController : CommonBaseController
{
    private readonly IAttendanceService _attendance;
    private readonly ICurrentUser _user;

    public MyAttendanceController(IAttendanceService attendance, ICurrentUser user)
    {
        _attendance = attendance;
        _user = user;
    }

    [HttpGet("timesheets")]
    public async Task<IActionResult> GetMyTimesheets([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetTimesheetsAsync(
            query, TimesheetScope.ForEmployee(_user.EmployeeId), ct));

    [HttpGet("timesheets/summary")]
    public async Task<IActionResult> GetMySummary([FromQuery] TimesheetQuery query, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetSummaryAsync(
            query, TimesheetScope.ForEmployee(_user.EmployeeId), ct));
}
