using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC22 (KI-11) — Quản đốc xem phiếu lỗi mọi tổ và hủy phiếu sau hạn (bắt buộc lý do, ghi AuditLog).</summary>
public sealed class DefectRecordsController : ManagerBaseController
{
    private readonly IDefectService _defects;

    public DefectRecordsController(IDefectService defects) => _defects = defects;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DefectQuery query, CancellationToken ct) =>
        this.ToResponse(await _defects.ListForTeamAsync(query, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelDefectDto dto, CancellationToken ct) =>
        this.ToResponse(await _defects.CancelAsync(id, dto, ct));
}
