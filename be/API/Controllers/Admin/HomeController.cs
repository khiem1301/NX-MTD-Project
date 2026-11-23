using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>DEV-ALL-170: số liệu trang chủ của Giám đốc.</summary>
public sealed class HomeController : AdminBaseController
{
    private readonly IHomeDashboardService _home;

    public HomeController(IHomeDashboardService home) => _home = home;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _home.GetAdminAsync(ct));
}
