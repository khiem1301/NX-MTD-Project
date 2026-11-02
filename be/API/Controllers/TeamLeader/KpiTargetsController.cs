using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>Chỉ tiêu KPI được giao cho tổ của mình (chỉ xem). Tổ lấy từ token, không theo tham số gửi lên.</summary>
public sealed class KpiTargetsController : TeamLeaderBaseController
{
    private readonly IKpiService _kpi;
    private readonly ICurrentUser _user;

    public KpiTargetsController(IKpiService kpi, ICurrentUser user)
    {
        _kpi = kpi; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] KpiTargetQuery query, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetPagedAsync(query with { SewingTeamId = _user.TeamId ?? 0 }, ct));
}
