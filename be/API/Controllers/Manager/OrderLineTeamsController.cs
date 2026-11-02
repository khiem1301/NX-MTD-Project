using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>DEV-ALL-49: Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may.</summary>
public sealed class OrderLineTeamsController : ManagerBaseController
{
    private readonly IOrderLineTeamService _allotments;

    public OrderLineTeamsController(IOrderLineTeamService allotments) => _allotments = allotments;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] OrderLineTeamQuery query, CancellationToken ct) =>
        this.ToResponse(await _allotments.GetPagedAsync(query, ct));

    [HttpGet("teams")]
    public async Task<IActionResult> ListTeams([FromQuery] int orderId, [FromQuery] int productId, CancellationToken ct) =>
        this.ToResponse(await _allotments.ListTeamsAsync(orderId, productId, ct));

    [HttpPut]
    public async Task<IActionResult> SetTeams([FromBody] SetOrderLineTeamsDto dto, CancellationToken ct) =>
        this.ToResponse(await _allotments.SetTeamsAsync(dto, ct));

    [HttpPost("bulk")]
    public async Task<IActionResult> AddTeams([FromBody] AddOrderLineTeamsDto dto, CancellationToken ct) =>
        this.ToResponse(await _allotments.AddTeamsAsync(dto, ct));
}
