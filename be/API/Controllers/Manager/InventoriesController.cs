using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC24: tồn 3 kho, thẻ kho, BTP tổ may đang giữ.</summary>
public sealed class InventoriesController : ManagerBaseController
{
    private readonly IInventoryService _inventory;

    public InventoriesController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet]
    public async Task<IActionResult> GetBalances([FromQuery] StockBalanceQuery query, CancellationToken ct) =>
        this.ToResponse(await _inventory.GetBalancesAsync(query, ct));

    [HttpGet("stock-card")]
    public async Task<IActionResult> GetStockCard([FromQuery] StockCardQuery query, CancellationToken ct) =>
        this.ToResponse(await _inventory.GetStockCardAsync(query, ct));

    [HttpGet("team-holdings")]
    public async Task<IActionResult> GetTeamHoldings([FromQuery] int? teamId, [FromQuery] int? orderId,
        CancellationToken ct) =>
        this.ToResponse(await _inventory.GetTeamHoldingsAsync(teamId, orderId, ct));
}
