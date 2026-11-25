using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC53 / UC54: tổ trưởng / tổ phó may chia việc (giao / thu hồi số lần làm công đoạn kèm BTP) cho công nhân và xác nhận hộ khi cần.</summary>
public sealed class WorkAllocationsController : TeamLeaderBaseController
{
    private readonly IWorkAllocationService _allocations;
    private readonly IDailyWorkAllocationService _daily;
    private readonly ICurrentUser _user;

    public WorkAllocationsController(IWorkAllocationService allocations, IDailyWorkAllocationService daily, ICurrentUser user)
    {
        _allocations = allocations; _daily = daily; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] WorkAllocationQuery query, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetPagedAsync(query, TeamId, null, ct));

    /// <summary>DEV-ALL-182: danh sách phiếu gộp theo công nhân (một dòng mỗi công nhân).</summary>
    [HttpGet("workers")]
    public async Task<IActionResult> GetWorkers([FromQuery] WorkAllocationQuery query, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetWorkerSummariesAsync(query, TeamId, ct));

    [HttpGet("board")]
    public async Task<IActionResult> GetBoard([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(await _allocations.GetBoardAsync(TeamId, orderId, ct));

    /// <summary>Giao việc theo ngày: các công đoạn đã phân công trước khi may cùng phần đã giao của ngày.</summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily([FromQuery] DateOnly workDate, [FromQuery] int orderId, [FromQuery] int? orderLineId, CancellationToken ct) =>
        this.ToResponse(await _daily.GetBoardAsync(TeamId, workDate, orderId, orderLineId, ct));

    [HttpPut("daily")]
    public async Task<IActionResult> SaveDaily([FromBody] SaveDailyAllocationDto dto, CancellationToken ct) =>
        this.ToResponse(await _daily.SaveAsync(dto, TeamId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkAllocationDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.CreateAsync(dto, TeamId, ct));

    [HttpPost("{id:int}/confirm-on-behalf")]
    public async Task<IActionResult> ConfirmOnBehalf(int id, [FromBody] ConfirmOnBehalfDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.ConfirmOnBehalfAsync(id, dto, TeamId, ct));

    /// <summary>DEV-ALL-182: xác nhận hộ một lần mọi phiếu đang chờ của công nhân.</summary>
    [HttpPost("workers/{workerId:int}/confirm-on-behalf")]
    public async Task<IActionResult> ConfirmWorkerOnBehalf(int workerId, [FromBody] ConfirmWorkerOnBehalfDto dto, CancellationToken ct) =>
        this.ToResponse(await _allocations.ConfirmWorkerOnBehalfAsync(workerId, dto, TeamId, ct));
}
