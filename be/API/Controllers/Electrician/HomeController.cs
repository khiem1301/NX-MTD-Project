using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Electrician;

/// <summary>DEV-ALL-168: số liệu trang chủ của cơ điện.</summary>
public sealed class HomeController : ElectricianBaseController
{
    private readonly IHomeDashboardService _home;

    public HomeController(IHomeDashboardService home) => _home = home;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _home.GetElectricianAsync(ct));
}
