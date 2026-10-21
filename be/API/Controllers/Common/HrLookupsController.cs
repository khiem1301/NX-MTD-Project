using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Tra cứu tổ / thành viên tổ cho luồng khác.</summary>
[Authorize(Roles = Roles.StaffExceptWorker)]
public sealed class HrLookupsController : CommonBaseController
{
    private readonly IHrLookupService _lookups;
    public HrLookupsController(IHrLookupService lookups) => _lookups = lookups;

    [HttpGet("teams")]
    public async Task<IActionResult> Teams(CancellationToken ct) => this.ToResponse(await _lookups.TeamsAsync(ct));

    [HttpGet("team-members")]
    public async Task<IActionResult> TeamMembers([FromQuery] int teamId, CancellationToken ct) =>
        this.ToResponse(await _lookups.MembersAsync(teamId, ct));
}
