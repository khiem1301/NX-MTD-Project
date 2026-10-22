using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Worker;

/// <summary>UC60: công nhân xem BTP được giao / thu hồi của mình và tự xác nhận hoặc từ chối.</summary>
public sealed class PieceMovementsController : WorkerBaseController
{
    private readonly IWorkerPieceMovementService _movements;
    private readonly ICurrentUser _user;

    public PieceMovementsController(IWorkerPieceMovementService movements, ICurrentUser user)
    {
        _movements = movements; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] WorkerMovementQuery query, CancellationToken ct) =>
        this.ToResponse(await _movements.GetPagedAsync(query, null, _user.EmployeeId, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmWorkerMovementDto dto, CancellationToken ct) =>
        this.ToResponse(await _movements.ConfirmByWorkerAsync(id, dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _movements.RejectByWorkerAsync(id, dto, _user.EmployeeId, ct));
}
