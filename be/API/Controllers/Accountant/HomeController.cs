using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>DEV-ALL-170: số liệu trang chủ của Kế toán.</summary>
public sealed class HomeController : AccountantBaseController
{
    private readonly IHomeDashboardService _home;

    public HomeController(IHomeDashboardService home) => _home = home;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _home.GetAccountantAsync(ct));
}
