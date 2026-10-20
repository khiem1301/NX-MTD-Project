using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC19 (tổ cắt): Quản đốc lập / sửa / hủy lệnh cắt.</summary>
public sealed class WorkOrdersController : ManagerBaseController
{
    private readonly IWorkOrderService _workOrders;

    public WorkOrdersController(IWorkOrderService workOrders) => _workOrders = workOrders;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetByIdAsync(id, null, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveWorkOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveWorkOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.CancelAsync(id, ct));
}
