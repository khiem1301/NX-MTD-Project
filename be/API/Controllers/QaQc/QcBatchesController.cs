using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.QaQc;

/// <summary>UC57 (KI-06) — QA/QC nhập số hàng hoàn thiện đạt theo đợt; sửa / xóa trong ca.</summary>
public sealed class QcBatchesController : QaQcBaseController
{
    private readonly IQcBatchService _qc;

    public QcBatchesController(IQcBatchService qc) => _qc = qc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QcBatchQuery query, CancellationToken ct) =>
        this.ToResponse(await _qc.ListAsync(query, ct));

    /// <summary>Hàng đang có trên chuyền của một tổ + dòng đơn (trần số đạt, MSG19).</summary>
    [HttpGet("on-line")]
    public async Task<IActionResult> OnLine([FromQuery] int sewingTeamId, [FromQuery] int orderLineId, CancellationToken ct) =>
        this.ToResponse(await _qc.GetOnLineAsync(sewingTeamId, orderLineId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQcBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _qc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateQcBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _qc.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteQcBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _qc.DeleteAsync(id, dto, ct));
}
