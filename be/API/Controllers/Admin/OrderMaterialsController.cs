using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC09 (phần NVL): NVL của đơn + định mức.</summary>
public sealed class OrderMaterialsController : AdminBaseController
{
    private readonly IOrderMaterialService _orderMaterials;
    private readonly ICustomerDeliveryPlanService _customerDelivery;

    public OrderMaterialsController(IOrderMaterialService orderMaterials, ICustomerDeliveryPlanService customerDelivery)
    {
        _orderMaterials = orderMaterials; _customerDelivery = customerDelivery;
    }

    /// <summary>Bảng NVL của đơn: tìm kiếm, lọc, phân trang.</summary>
    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] OrderMaterialPageQuery query, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.GetPagedAsync(query, ct));

    [HttpGet]
    public async Task<IActionResult> GetByOrder([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.GetByOrderAsync(orderId, ct));

    /// <summary>Mã hàng trong đơn và NVL định mức của từng mã.</summary>
    [HttpGet("product-materials")]
    public async Task<IActionResult> GetProductMaterials([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.GetProductMaterialsAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.CreateAsync(dto, ct));

    /// <summary>Thêm các NVL của một mã hàng cùng lúc.</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateOrderMaterialBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.CreateBatchAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.UpdateAsync(id, dto, ct));

    /// <summary>Lịch khách giao NVL của cả đơn: số lần, ngày / khoảng ngày dự kiến (DEV-ALL-95).</summary>
    [HttpGet("customer-delivery/{orderId:int}")]
    public async Task<IActionResult> GetCustomerDelivery(int orderId, CancellationToken ct) =>
        this.ToResponse(await _customerDelivery.GetAsync(orderId, ct));

    [HttpPut("customer-delivery/{orderId:int}")]
    public async Task<IActionResult> SaveCustomerDelivery(int orderId, [FromBody] SaveOrderCustomerDeliveryDto dto,
        CancellationToken ct) =>
        this.ToResponse(await _customerDelivery.SaveAsync(orderId, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.DeleteAsync(id, ct));
}
