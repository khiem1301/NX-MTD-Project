using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC09 (phần NVL): NVL của đơn + định mức.</summary>
public sealed class OrderMaterialsController : AdminBaseController
{
    private readonly IOrderMaterialService _orderMaterials;

    public OrderMaterialsController(IOrderMaterialService orderMaterials) => _orderMaterials = orderMaterials;

    [HttpGet]
    public async Task<IActionResult> GetByOrder([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.GetByOrderAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.DeleteAsync(id, ct));
}
