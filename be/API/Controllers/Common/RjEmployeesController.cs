using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>
/// Danh sách NV từ Ronald Jack Pro (snapshot) — Admin/Quản đốc dùng khi tạo NV; Kế toán cũng xem được.
/// </summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Manager + "," + Roles.Accountant)]
public sealed class RjEmployeesController : CommonBaseController
{
    private readonly IAttendanceService _attendance;

    public RjEmployeesController(IAttendanceService attendance) => _attendance = attendance;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool onlyUnmapped, CancellationToken ct) =>
        this.ToResponse(await _attendance.GetRjEmployeesAsync(onlyUnmapped, ct));
}
