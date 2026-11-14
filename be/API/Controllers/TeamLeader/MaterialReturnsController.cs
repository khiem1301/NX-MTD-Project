using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC42: tổ trưởng / tổ phó tổ cắt (NVL theo lệnh cắt) và tổ may (NVL, BTP theo đơn) trả hàng về kho (DEV-ALL-112).</summary>
public sealed class MaterialReturnsController : TeamLeaderBaseController
{
    private readonly IMaterialReturnService _returns;
    private readonly ICurrentUser _user;

    public MaterialReturnsController(IMaterialReturnService returns, ICurrentUser user)
    {
        _returns = returns; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialReturnQuery query, CancellationToken ct) =>
        this.ToResponse(await _returns.GetPagedAsync(query, TeamId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _returns.GetByIdAsync(id, TeamId, ct));

    [HttpGet("returnable")]
    public async Task<IActionResult> GetReturnable([FromQuery] ReturnableQuery query, CancellationToken ct) =>
        this.ToResponse(await _returns.GetReturnableAsync(TeamId, query, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialReturnDto dto, CancellationToken ct) =>
        this.ToResponse(await _returns.CreateAsync(dto, TeamId, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _returns.CancelAsync(id, TeamId, ct));
}
