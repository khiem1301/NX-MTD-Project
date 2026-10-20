using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC51: tổ trưởng / tổ phó cắt nhập sản lượng cắt theo bàn.</summary>
public sealed class CuttingLaysController : TeamLeaderBaseController
{
    private readonly ICuttingLayService _lays;
    private readonly ICurrentUser _user;

    public CuttingLaysController(ICuttingLayService lays, ICurrentUser user)
    {
        _lays = lays; _user = user;
    }

    /// <summary>Tài khoản không gắn tổ → 0 để không khớp dữ liệu tổ nào.</summary>
    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] CuttingLayQuery query, CancellationToken ct) =>
        this.ToResponse(await _lays.GetPagedAsync(query, TeamId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _lays.GetByIdAsync(id, TeamId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCuttingLayDto dto, CancellationToken ct) =>
        this.ToResponse(await _lays.CreateAsync(dto, TeamId, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveCuttingLayDto dto, CancellationToken ct) =>
        this.ToResponse(await _lays.UpdateAsync(id, dto, TeamId, ct));
}
