using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC22: Admin xem nhật ký thay đổi dữ liệu (ai, lúc nào, giá trị cũ / mới, lý do).</summary>
public sealed class AuditLogsController : AdminBaseController
{
    private readonly IAuditLogQueryService _logs;

    public AuditLogsController(IAuditLogQueryService logs) => _logs = logs;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        this.ToResponse(await _logs.ListAsync(query, ct));

    [HttpGet("tables")]
    public async Task<IActionResult> Tables(CancellationToken ct) => this.ToResponse(await _logs.GetTableNamesAsync(ct));
}
