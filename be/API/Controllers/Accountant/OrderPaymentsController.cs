using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>
/// DEV-ALL-118 — Kế toán chốt giá trị đơn hàng khách (có thể âm khi mình phải đền hợp đồng) và ghi nhận thanh toán nhiều lần
/// (dương = khách trả, âm = mình trả). Không có thao tác xóa; sửa một lần thanh toán trong 24 giờ kèm lý do.
/// </summary>
public sealed class OrderPaymentsController : AccountantBaseController
{
    private readonly IOrderPaymentService _payments;

    public OrderPaymentsController(IOrderPaymentService payments) => _payments = payments;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderPaymentQuery query, CancellationToken ct) =>
        this.ToResponse(await _payments.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _payments.GetAsync(id, ct));

    [HttpPut("{id:int}/final-amount")]
    public async Task<IActionResult> SetFinalAmount(int id, [FromBody] SetFinalAmountDto dto, CancellationToken ct) =>
        this.ToResponse(await _payments.SetFinalAmountAsync(id, dto, ct));

    [HttpPost("{id:int}/payments")]
    public async Task<IActionResult> Add(int id, [FromBody] AddOrderPaymentDto dto, CancellationToken ct) =>
        this.ToResponse(await _payments.AddPaymentAsync(id, dto, ct));

    [HttpPut("{id:int}/payments/{paymentId:int}")]
    public async Task<IActionResult> Update(int id, int paymentId, [FromBody] UpdateOrderPaymentDto dto, CancellationToken ct) =>
        this.ToResponse(await _payments.UpdatePaymentAsync(id, paymentId, dto, ct));
}
