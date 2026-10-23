using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.QaQc;

/// <summary>UC44: QA/QC lập phiếu bàn giao thành phẩm (số QC đạt) vào kho; chỉ thấy phiếu của mình.</summary>
public sealed class FgHandoversController : QaQcBaseController
{
    private readonly IFgHandoverService _handovers;
    private readonly ICurrentUser _user;

    public FgHandoversController(IFgHandoverService handovers, ICurrentUser user)
    {
        _handovers = handovers; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] FgHandoverQuery query, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetPagedAsync(query, _user.EmployeeId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetByIdAsync(id, _user.EmployeeId, ct));

    [HttpGet("handoverable")]
    public async Task<IActionResult> GetHandoverable([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetHandoverableAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFgHandoverDto dto, CancellationToken ct) =>
        this.ToResponse(await _handovers.CreateAsync(dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.CancelAsync(id, _user.EmployeeId, ct));
}
