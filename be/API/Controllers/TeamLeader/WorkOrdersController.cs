using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>Tổ trưởng / tổ phó cắt xem lệnh cắt của tổ mình.</summary>
[CuttingTeamOnly]
public sealed class WorkOrdersController : TeamLeaderBaseController
{
    private readonly IWorkOrderService _workOrders;
    private readonly ICurrentUser _user;

    public WorkOrdersController(IWorkOrderService workOrders, ICurrentUser user)
    {
        _workOrders = workOrders; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkOrderQuery query, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetPagedAsync(query, TeamId, ct));

    /// <summary>Tổ cắt báo cắt xong (cắt ít hơn kế hoạch, BTP đã vào kho): lệnh chuyển Hoàn thành; thiếu thì phải nêu lý do.</summary>
    [HttpPost("{id:int}/finish-cutting")]
    public async Task<IActionResult> FinishCutting(int id, [FromBody] FinishCuttingDto dto, CancellationToken ct) =>
        this.ToResponse(await _workOrders.FinishCuttingAsync(id, TeamId, dto.Reason, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetByIdAsync(id, TeamId, ct));
}
