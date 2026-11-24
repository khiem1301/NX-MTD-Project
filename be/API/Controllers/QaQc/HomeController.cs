using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.QaQc;

/// <summary>DEV-ALL-170: số liệu trang chủ của QA/QC.</summary>
public sealed class HomeController : QaQcBaseController
{
    private readonly IHomeDashboardService _home;

    public HomeController(IHomeDashboardService home) => _home = home;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _home.GetQaqcAsync(ct));
}
