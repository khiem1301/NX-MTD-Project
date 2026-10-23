using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC53 / UC54: tổ trưởng / tổ phó may giao hoặc thu hồi BTP với công nhân và xác nhận hộ khi cần.</summary>
public sealed class WorkerPieceMovementsController : TeamLeaderBaseController
{
    private readonly IWorkerPieceMovementService _movements;
    private readonly ICurrentUser _user;

    public WorkerPieceMovementsController(IWorkerPieceMovementService movements, ICurrentUser user)
    {
        _movements = movements; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkerMovementQuery query, CancellationToken ct) =>
        this.ToResponse(await _movements.GetPagedAsync(query, TeamId, null, ct));

    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldings([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(await _movements.GetHoldingsAsync(TeamId, orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkerMovementDto dto, CancellationToken ct) =>
        this.ToResponse(await _movements.CreateAsync(dto, TeamId, ct));

    [HttpPost("{id:int}/confirm-on-behalf")]
    public async Task<IActionResult> ConfirmOnBehalf(int id, [FromBody] ConfirmOnBehalfDto dto, CancellationToken ct) =>
        this.ToResponse(await _movements.ConfirmOnBehalfAsync(id, dto, TeamId, ct));
}
