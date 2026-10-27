using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC53 / UC54: tổ trưởng / tổ phó may chia việc (giao / thu hồi số lần làm công đoạn kèm BTP) cho công nhân và xác nhận hộ khi cần.</summary>
public sealed class WorkAllocationsController : TeamLeaderBaseController
{
    private readonly IWorkAllocationService _allocations;
    private readonly ICurrentUser _user;

    public WorkAllocationsController(IWorkAllocationService allocations, ICurrentUser user)
    {
        _allocations = allocations; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkAllocationQuery query, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetPagedAsync(query, TeamId, null, ct));

    [HttpGet("board")]
    public async Task<IActionResult> GetBoard([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetBoardAsync(TeamId, orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkAllocationDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.CreateAsync(dto, TeamId, ct));

    [HttpPost("{id:int}/confirm-on-behalf")]
    public async Task<IActionResult> ConfirmOnBehalf(int id, [FromBody] ConfirmOnBehalfDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.ConfirmOnBehalfAsync(id, dto, TeamId, ct));
}
