using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC19 (tổ cắt): Quản đốc lập / sửa / hủy lệnh cắt.</summary>
public sealed class WorkOrdersController : ManagerBaseController
{
    private readonly IWorkOrderService _workOrders;

    public WorkOrdersController(IWorkOrderService workOrders) => _workOrders = workOrders;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetByIdAsync(id, null, ct));

    /// <summary>Xem trước vải cần / khả dụng / thiếu cho kế hoạch đang nhập (không lưu).</summary>
    [HttpPost("fabric-preview")]
    public async Task<IActionResult> PreviewFabric([FromBody] SaveWorkOrderDto dto, [FromQuery] int? exceptWorkOrderId, CancellationToken ct) =>
        this.ToResponse(await _workOrders.PreviewFabricAsync(dto, exceptWorkOrderId, ct));

    /// <summary>Vải theo đơn của các lệnh cắt đang chạy.</summary>
    [HttpGet("fabric-by-order")]
    public async Task<IActionResult> GetFabricByOrder([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetFabricByOrderAsync(orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveWorkOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.CreateAsync(dto, ct));

    /// <summary>Đơn cắt bù đã duyệt, chưa có lệnh cắt bù.</summary>
    [HttpGet("replacement-requests")]
    public async Task<IActionResult> GetReplacementRequests(CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetOpenReplacementRequestsAsync(ct));

    /// <summary>Lập lệnh cắt bù từ đơn cắt bù đã duyệt (TH-10, UC19).</summary>
    [HttpPost("replacement")]
    public async Task<IActionResult> CreateReplacement([FromBody] CreateReplacementWorkOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.CreateReplacementAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveWorkOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.CancelAsync(id, ct));
}
