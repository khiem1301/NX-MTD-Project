using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>Thủ kho xem lệnh cắt để cấp vải (UC40); vải còn phải cấp lấy ở /fabrics.</summary>
public sealed class WorkOrdersController : WarehouseKeeperBaseController
{
    private readonly IWorkOrderService _workOrders;

    public WorkOrdersController(IWorkOrderService workOrders) => _workOrders = workOrders;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetByIdAsync(id, null, ct));

    [HttpGet("{id:int}/fabrics")]
    public async Task<IActionResult> GetFabrics(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetFabricRequirementsAsync(id, ct));
}
