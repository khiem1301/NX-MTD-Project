using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Worker;

/// <summary>UC60: công nhân xem việc được giao / thu hồi của mình và tự xác nhận hoặc từ chối.</summary>
public sealed class WorkAllocationsController : WorkerBaseController
{
    private readonly IWorkAllocationService _allocations;
    private readonly ICurrentUser _user;

    public WorkAllocationsController(IWorkAllocationService allocations, ICurrentUser user)
    {
        _allocations = allocations; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] WorkAllocationQuery query, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetPagedAsync(query, null, _user.EmployeeId, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmWorkAllocationDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.ConfirmByWorkerAsync(id, dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.RejectByWorkerAsync(id, dto, _user.EmployeeId, ct));
}
