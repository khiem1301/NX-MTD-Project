using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC43: tổ trưởng / tổ phó cắt lập phiếu bàn giao BTP vào kho.</summary>
[CuttingTeamOnly]
public sealed class CutHandoversController : TeamLeaderBaseController
{
    private readonly ICutHandoverService _handovers;
    private readonly ICurrentUser _user;

    public CutHandoversController(ICutHandoverService handovers, ICurrentUser user)
    {
        _handovers = handovers; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] CutHandoverQuery query, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetPagedAsync(query, TeamId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetByIdAsync(id, TeamId, ct));

    [HttpGet("handoverable")]
    public async Task<IActionResult> GetHandoverable([FromQuery] int workOrderId, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetHandoverableAsync(workOrderId, TeamId, ct));

    /// <summary>Rút phiếu bàn giao BTP khi Thủ kho chưa nhận.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.CancelAsync(id, TeamId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCutHandoverDto dto, CancellationToken ct) =>
        this.ToResponse(await _handovers.CreateAsync(dto, TeamId, ct));
}
