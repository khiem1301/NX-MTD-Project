using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC37: Thủ kho chỉ xem đơn mua để chọn khi lập phiếu nhập từ nhà cung cấp (không tạo / sửa đơn mua).</summary>
public sealed class PurchaseOrdersController : WarehouseKeeperBaseController
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PurchaseOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.GetByIdAsync(id, ct));
}
