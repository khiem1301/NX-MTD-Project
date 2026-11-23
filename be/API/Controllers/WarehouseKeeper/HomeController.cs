using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>DEV-ALL-170: số liệu trang chủ của Thủ kho.</summary>
public sealed class HomeController : WarehouseKeeperBaseController
{
    private readonly IHomeDashboardService _home;

    public HomeController(IHomeDashboardService home) => _home = home;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _home.GetWarehouseAsync(ct));
}
