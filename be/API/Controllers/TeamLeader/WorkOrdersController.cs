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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _workOrders.GetByIdAsync(id, TeamId, ct));
}
