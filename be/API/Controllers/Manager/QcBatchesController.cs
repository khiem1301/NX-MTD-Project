using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC22 (KI-11) — Quản đốc xem và sửa / xóa đợt QC sau hết ca (bắt buộc lý do, ghi AuditLog).</summary>
public sealed class QcBatchesController : ManagerBaseController
{
    private readonly IQcBatchService _qc;

    public QcBatchesController(IQcBatchService qc) => _qc = qc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QcBatchQuery query, CancellationToken ct) =>
        this.ToResponse(await _qc.ListAsync(query, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateQcBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _qc.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteQcBatchDto dto, CancellationToken ct) =>
        this.ToResponse(await _qc.DeleteAsync(id, dto, ct));
}
