using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC34 (MI-07) — Kế toán xem đơn mua đã đặt hàng, xác nhận thanh toán (kèm ảnh chứng từ) hoặc trả lại Quản đốc.</summary>
public sealed class PurchasePaymentsController : AccountantBaseController
{
    private readonly IPurchaseOrderFlowService _flow;
    private readonly IPurchasePaymentService _payments;

    public PurchasePaymentsController(IPurchaseOrderFlowService flow, IPurchasePaymentService payments)
    {
        _flow = flow;
        _payments = payments;
    }

    /// <summary>Chỉ đơn đã đặt hàng với nhà cung cấp mới đến lượt thanh toán.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PurchaseOrderFlowQuery query, CancellationToken ct) =>
        this.ToResponse(await _flow.ListAsync(query with { PayableOnly = true }, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => this.ToResponse(await _flow.GetAsync(id, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmPaymentDto dto, CancellationToken ct) =>
        this.ToResponse(await _flow.ConfirmPaymentAsync(id, dto, ct));

    /// <summary>Sửa một lần thanh toán trong 24 giờ kể từ lúc nhập, bắt buộc lý do. Không có thao tác xóa.</summary>
    [HttpPut("{id:int}/payments/{paymentId:int}")]
    public async Task<IActionResult> UpdatePayment(int id, int paymentId, [FromBody] UpdatePurchasePaymentDto dto, CancellationToken ct) =>
        this.ToResponse(await _payments.UpdatePaymentAsync(id, paymentId, dto, ct));

    /// <summary>Nhập giá trị thực tế của đơn đang nhận hàng / đã chốt giao thiếu (Amount null = bỏ, dùng giá trị tự tính).</summary>
    [HttpPut("{id:int}/actual-amount")]
    public async Task<IActionResult> SetActualAmount(int id, [FromBody] SetActualAmountDto dto, CancellationToken ct) =>
        this.ToResponse(await _payments.SetActualAmountAsync(id, dto, ct));

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReturnPaymentDto dto, CancellationToken ct) =>
        this.ToResponse(await _flow.ReturnPaymentAsync(id, dto, ct));
}
