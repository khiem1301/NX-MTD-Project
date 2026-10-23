using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC41: tổ trưởng / tổ phó đếm và xác nhận NVL / BTP nhận từ kho, hoặc từ chối.</summary>
public sealed class MaterialIssuesController : TeamLeaderBaseController
{
    private readonly IMaterialIssueService _issues;
    private readonly ICurrentUser _user;

    public MaterialIssuesController(IMaterialIssueService issues, ICurrentUser user)
    {
        _issues = issues; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialIssueQuery query, CancellationToken ct) =>
        this.ToResponse(await _issues.GetPagedAsync(query, TeamId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _issues.GetByIdAsync(id, TeamId, ct));

    /// <summary>Xuất Excel phiếu cấp của tổ mình để in (TH-16).</summary>
    [HttpGet("{id:int}/excel")]
    public async Task<IActionResult> ExportExcel(int id,
        [FromServices] IWarehouseDocumentExportService exports, CancellationToken ct) =>
        this.ToExcelFile(await exports.ExportIssueAsync(id, TeamId, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmIssueDto dto, CancellationToken ct) =>
        this.ToResponse(await _issues.ConfirmAsync(id, dto, TeamId, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _issues.RejectAsync(id, dto, TeamId, ct));
}
