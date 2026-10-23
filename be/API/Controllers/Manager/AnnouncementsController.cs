using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC20: Quản đốc gửi thông báo nhanh tới toàn xưởng / tổ / vai trò và xem các thông báo đã gửi.</summary>
public sealed class AnnouncementsController : ManagerBaseController
{
    private readonly IAnnouncementService _announcements;
    private readonly ICurrentUser _user;

    public AnnouncementsController(IAnnouncementService announcements, ICurrentUser user)
    {
        _announcements = announcements; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> ListSent([FromQuery] AnnouncementQuery query, CancellationToken ct) =>
        this.ToResponse(await _announcements.ListSentAsync(query, ct));

    [HttpGet("targets")]
    public async Task<IActionResult> GetTargets(CancellationToken ct) =>
        this.ToResponse(await _announcements.GetTargetsAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendAnnouncementDto dto, CancellationToken ct) =>
        this.ToResponse(await _announcements.SendAsync(dto, _user.EmployeeId, ct));
}
