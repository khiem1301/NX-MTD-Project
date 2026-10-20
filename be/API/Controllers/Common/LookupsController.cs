using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Dropdown dùng chung cho mọi vai trò đã đăng nhập (chỉ đọc).</summary>
[Authorize]
public sealed class LookupsController : CommonBaseController
{
    private readonly ILookupService _lookups;

    public LookupsController(ILookupService lookups) => _lookups = lookups;

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? keyword, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<OrderOptionDto>>.Success(await _lookups.GetOpenOrdersAsync(keyword, ct)));

    [HttpGet("orders/{orderId:int}/lines")]
    public async Task<IActionResult> OrderLines(int orderId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<OrderLineOptionDto>>.Success(await _lookups.GetOrderLinesAsync(orderId, ct)));

    [HttpGet("teams")]
    public async Task<IActionResult> Teams([FromQuery] string? teamType, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<TeamOptionDto>>.Success(await _lookups.GetTeamsAsync(teamType, ct)));

    [HttpGet("materials")]
    public async Task<IActionResult> Materials([FromQuery] string? keyword, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<MaterialOptionDto>>.Success(await _lookups.GetActiveMaterialsAsync(keyword, ct)));
}
