using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Tra cứu đơn / dòng đơn / mã hàng cho luồng khác. Không có đơn giá (D26).</summary>
[Authorize(Roles = Roles.StaffExceptWorker)]
public sealed class OrderLookupsController : CommonBaseController
{
    private readonly IOrderLookupService _lookups;
    public OrderLookupsController(IOrderLookupService lookups) => _lookups = lookups;

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] OrderLookupQuery query, CancellationToken ct) =>
        this.ToResponse(await _lookups.OrdersAsync(query, ct));

    [HttpGet("orders/{id:int}/lines")]
    public async Task<IActionResult> Lines(int id, CancellationToken ct) =>
        this.ToResponse(await _lookups.LinesAsync(id, ct));

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] PageQuery query, CancellationToken ct) =>
        this.ToResponse(await _lookups.ProductsAsync(query, ct));
}
