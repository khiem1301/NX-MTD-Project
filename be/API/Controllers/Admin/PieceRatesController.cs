using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC26 (KI-01) — Admin xem đơn giá chờ duyệt, duyệt hoặc từ chối (bắt buộc lý do).</summary>
public sealed class PieceRatesController : AdminBaseController
{
    private readonly IPieceRateService _rates;

    public PieceRatesController(IPieceRateService rates) => _rates = rates;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PieceRateQuery query, CancellationToken ct) =>
        this.ToResponse(await _rates.ListAsync(query, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApprovePieceRateDto dto, CancellationToken ct) =>
        this.ToResponse(await _rates.ApproveAsync(id, dto, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectPieceRateDto dto, CancellationToken ct) =>
        this.ToResponse(await _rates.RejectAsync(id, dto, ct));
}
