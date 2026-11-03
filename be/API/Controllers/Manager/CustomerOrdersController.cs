using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC09: Quản đốc chỉ xem đơn hàng, không có đơn giá CMT, thành tiền (GBR-02). Tạo, sửa, hủy, dừng chỉ ở API của Admin.</summary>
public sealed class CustomerOrdersController : ManagerBaseController
{
    private readonly ICustomerOrderService _orders;
    public CustomerOrdersController(ICustomerOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _orders.ListOverviewAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        this.ToResponse(await _orders.GetOverviewAsync(id, ct));
}
