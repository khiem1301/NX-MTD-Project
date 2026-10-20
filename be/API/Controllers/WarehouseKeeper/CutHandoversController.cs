using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC43: Thủ kho đếm, xác nhận nhận BTP vào kho hoặc từ chối phiếu bàn giao.</summary>
public sealed class CutHandoversController : WarehouseKeeperBaseController
{
    private readonly ICutHandoverService _handovers;
    private readonly ICurrentUser _user;

    public CutHandoversController(ICutHandoverService handovers, ICurrentUser user)
    {
        _handovers = handovers; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] CutHandoverQuery query, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetByIdAsync(id, null, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmCutHandoverDto dto, CancellationToken ct) =>
        this.ToResponse(await _handovers.ConfirmAsync(id, dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _handovers.RejectAsync(id, dto, _user.EmployeeId, ct));
}
