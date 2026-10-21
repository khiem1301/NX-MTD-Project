using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>
/// UC23 (MI-07) — Quản đốc theo dõi đơn mua, chọn nhà cung cấp + chốt đặt hàng, hủy, sửa đơn giá khi Kế toán trả lại.
/// Tạo đơn mua nằm ở <see cref="PurchaseOrdersController"/> (Thái).
/// </summary>
public sealed class PurchaseOrderTrackingController : ManagerBaseController
{
    private readonly IPurchaseOrderFlowService _flow;

    public PurchaseOrderTrackingController(IPurchaseOrderFlowService flow) => _flow = flow;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PurchaseOrderFlowQuery query, CancellationToken ct) =>
        this.ToResponse(await _flow.ListAsync(query with { PayableOnly = false }, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _flow.GetAsync(id, ct));

    [HttpPost("{id:int}/place")]
    public async Task<IActionResult> Place(int id, [FromBody] PlacePurchaseOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _flow.PlaceAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelPurchaseOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _flow.CancelAsync(id, dto, ct));

    [HttpPost("{id:int}/reprice")]
    public async Task<IActionResult> Reprice(int id, [FromBody] RepricePurchaseOrderDto dto, CancellationToken ct) =>
        this.ToResponse(await _flow.RepriceAsync(id, dto, ct));
}
