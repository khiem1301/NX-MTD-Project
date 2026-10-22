using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC25 (KI-01) — Kế toán lập đơn giá công đoạn (Nháp), sửa khi chưa duyệt, gửi Admin duyệt.</summary>
public sealed class PieceRatesController : AccountantBaseController
{
    private readonly IPieceRateService _rates;

    public PieceRatesController(IPieceRateService rates) => _rates = rates;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PieceRateQuery query, CancellationToken ct) =>
        this.ToResponse(await _rates.ListAsync(query, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePieceRateDto dto, CancellationToken ct) =>
        this.ToResponse(await _rates.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SavePieceRateDto dto, CancellationToken ct) =>
        this.ToResponse(await _rates.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct) =>
        this.ToResponse(await _rates.SubmitAsync(id, ct));
}
