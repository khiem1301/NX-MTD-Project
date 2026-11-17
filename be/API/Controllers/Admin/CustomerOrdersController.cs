using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC09: đơn hàng + ma trận màu × size + đơn giá CMT (chỉ Admin, D26). Không có xóa.</summary>
public sealed class CustomerOrdersController : AdminBaseController
{
    private readonly ICustomerOrderService _orders;
    private readonly IOrderImportService _import;

    public CustomerOrdersController(ICustomerOrderService orders, IOrderImportService import)
    {
        _orders = orders; _import = import;
    }

    /// <summary>DEV-ALL-117: xem trước dòng hàng của đơn từ Excel (multipart: file). Không ghi gì, form đơn lưu như tạo tay.</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.PreviewAsync(stream, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _orders.ListAsync(query, ct));

    [HttpGet("next-code")]
    public async Task<IActionResult> NextCode(CancellationToken ct) =>
        this.ToResponse(await _orders.SuggestCodeAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        this.ToResponse(await _orders.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _orders.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _orders.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _orders.CancelAsync(id, dto, ct));

    /// <summary>Chuyển đơn NEW sang sản xuất khi đã có NVL (MaterialStatus khác AWAITING).</summary>
    [HttpPost("{id:int}/start-production")]
    public async Task<IActionResult> StartProduction(int id, [FromBody] StartOrderProductionDto dto, CancellationToken ct) =>
        this.ToResponse(await _orders.StartProductionAsync(id, dto, ct));

    [HttpPost("{id:int}/stop")]
    public async Task<IActionResult> Stop(int id, [FromBody] StopOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _orders.StopAsync(id, dto, ct));
}
