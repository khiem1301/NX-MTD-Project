using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC18 cho Admin. Quản đốc dùng Manager/TeamsController (cùng ITeamService).</summary>
public sealed class TeamsController : AdminBaseController
{
    private readonly ITeamService _teams;
    public TeamsController(ITeamService teams) => _teams = teams;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeDiscontinued, CancellationToken ct) =>
        this.ToResponse(await _teams.ListAsync(includeDiscontinued, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _teams.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTeamDto dto, CancellationToken ct) =>
        this.ToResponse(await _teams.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTeamDto dto, CancellationToken ct) =>
        this.ToResponse(await _teams.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) => this.ToResponse(await _teams.DiscontinueAsync(id, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) => this.ToResponse(await _teams.ReactivateAsync(id, ct));

    [HttpPost("{id:int}/transfer-in")]
    public async Task<IActionResult> TransferIn(int id, [FromBody] TransferMembersDto dto, CancellationToken ct) =>
        this.ToResponse(await _teams.TransferInAsync(id, dto, ct));

    [HttpPost("{id:int}/appoint")]
    public async Task<IActionResult> Appoint(int id, [FromBody] AppointDto dto, CancellationToken ct) =>
        this.ToResponse(await _teams.AppointAsync(id, dto, ct));
}
