using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC39: Thủ kho xem các đơn thiếu NVL đã chọn "giao bù" để lập phiếu bổ sung khi hàng về.</summary>
public sealed class ShortDeliveriesController : WarehouseKeeperBaseController
{
    private readonly IShortDeliveryService _shortDeliveries;

    public ShortDeliveriesController(IShortDeliveryService shortDeliveries) => _shortDeliveries = shortDeliveries;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ShortDeliveryQuery query, CancellationToken ct) =>
        this.ToResponse(await _shortDeliveries.GetPagedAsync(query with { AwaitingReplenish = true }, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _shortDeliveries.GetByIdAsync(id, ct));
}
