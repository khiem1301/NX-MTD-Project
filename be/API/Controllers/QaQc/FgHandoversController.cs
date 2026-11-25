using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.QaQc;

/// <summary>UC44: QA/QC lập phiếu bàn giao thành phẩm (số QC đạt) vào kho; chỉ thấy phiếu của mình.</summary>
public sealed class FgHandoversController : QaQcBaseController
{
    private readonly IFgHandoverService _handovers;
    private readonly ICurrentUser _user;

    public FgHandoversController(IFgHandoverService handovers, ICurrentUser user)
    {
        _handovers = handovers; _user = user;
    }

    /// <summary>Tổ may của QA/QC đăng nhập; null nếu chưa thuộc tổ may.</summary>
    private int? SewingTeamId => _user.TeamTypeCode == TeamTypes.Sewing ? _user.TeamId : null;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] FgHandoverQuery query, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetPagedAsync(query, _user.EmployeeId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetByIdAsync(id, _user.EmployeeId, ct));

    [HttpGet("handoverable")]
    public async Task<IActionResult> GetHandoverable([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _handovers.GetHandoverableAsync(orderId, ct));

    /// <summary>DEV-ALL-183: thành phẩm của tổ may mà QA/QC thuộc về, còn chờ bàn giao (mọi đơn).</summary>
    [HttpGet("team-handoverable")]
    public async Task<IActionResult> GetTeamHandoverable(CancellationToken ct) =>
        this.ToResponse(await _handovers.GetTeamHandoverableAsync(SewingTeamId, ct));

    /// <summary>DEV-ALL-183: bàn giao toàn bộ thành phẩm còn lại của tổ vào kho (mỗi đơn + mã hàng một phiếu).</summary>
    [HttpPost("handover-all")]
    public async Task<IActionResult> CreateAll(CancellationToken ct) =>
        this.ToResponse(await _handovers.CreateAllForTeamAsync(SewingTeamId, _user.EmployeeId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFgHandoverDto dto, CancellationToken ct) =>
        this.ToResponse(await _handovers.CreateAsync(dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _handovers.CancelAsync(id, _user.EmployeeId, ct));
}
