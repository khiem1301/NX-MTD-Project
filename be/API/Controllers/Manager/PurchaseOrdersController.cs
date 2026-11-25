using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Quản đốc tạo đơn mua NVL; không bắt buộc gắn đơn hàng (mua kim, chỉ dùng chung nhiều đơn).</summary>
public sealed class PurchaseOrdersController : ManagerBaseController
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PurchaseOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.GetByIdAsync(id, ct));

    /// <summary>DEV-ALL-186: sửa đơn mua trong 24 giờ kể từ lúc tạo (chưa nhận hàng, chưa thanh toán), kèm lý do.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePurchaseOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.UpdateAsync(id, dto, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _purchaseOrders.CreateAsync(dto, ct));
}
