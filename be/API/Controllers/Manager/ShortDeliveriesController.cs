using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC08 / UC39: Quản đốc xử lý đơn thiếu NVL khi nhập — chọn giao bù hoặc chốt không bù.</summary>
public sealed class ShortDeliveriesController : ManagerBaseController
{
    private readonly IShortDeliveryService _shortDeliveries;
    private readonly ICurrentUser _user;

    public ShortDeliveriesController(IShortDeliveryService shortDeliveries, ICurrentUser user)
    {
        _shortDeliveries = shortDeliveries; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ShortDeliveryQuery query, CancellationToken ct) =>
        this.ToResponse(await _shortDeliveries.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _shortDeliveries.GetByIdAsync(id, ct));

    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveShortDeliveryDto dto, CancellationToken ct) =>
        this.ToResponse(await _shortDeliveries.ResolveAsync(id, dto, _user.EmployeeId, ct));
}
