using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>
/// UC22 (KI-11) — Quản đốc sửa sản lượng của tổ may (kể cả sau hạn của tổ trưởng): phải chọn tổ và nhập lý do khi quá
/// hạn; mọi thay đổi ghi AuditLog.
/// </summary>
public sealed class OutputRecordsController : ManagerBaseController
{
    private readonly IOutputService _output;

    public OutputRecordsController(IOutputService output) => _output = output;

    [HttpGet("grid")]
    public async Task<IActionResult> GetGrid([FromQuery] DateOnly workDate, [FromQuery] string shiftCode,
        [FromQuery] int orderLineId, [FromQuery] int sewingTeamId, CancellationToken ct) =>
        this.ToResponse(await _output.GetGridAsync(workDate, shiftCode, orderLineId, sewingTeamId, ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveOutputDto dto, CancellationToken ct) =>
        this.ToResponse(await _output.SaveAsync(dto, ct));
}
