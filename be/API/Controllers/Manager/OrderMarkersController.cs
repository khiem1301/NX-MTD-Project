using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Bìa cắt của đơn (DEV-ALL-85): xem bìa theo đơn, lưu bìa của một mã hàng trong đơn.</summary>
public sealed class OrderMarkersController : ManagerBaseController
{
    private readonly IOrderMarkerService _markers;
    public OrderMarkersController(IOrderMarkerService markers) => _markers = markers;

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> Get(int orderId, CancellationToken ct) =>
        this.ToResponse(await _markers.GetAsync(orderId, ct));

    [HttpPut("{orderId:int}/products/{productId:int}")]
    public async Task<IActionResult> Save(int orderId, int productId, [FromBody] SaveOrderMarkersDto dto, CancellationToken ct) =>
        this.ToResponse(await _markers.SaveAsync(orderId, productId, dto, ct));
}
