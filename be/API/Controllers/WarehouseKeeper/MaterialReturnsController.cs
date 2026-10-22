using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC42: Thủ kho đếm, xác nhận nhận vải trả vào kho NVL hoặc từ chối phiếu.</summary>
public sealed class MaterialReturnsController : WarehouseKeeperBaseController
{
    private readonly IMaterialReturnService _returns;
    private readonly ICurrentUser _user;

    public MaterialReturnsController(IMaterialReturnService returns, ICurrentUser user)
    {
        _returns = returns; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialReturnQuery query, CancellationToken ct) =>
        this.ToResponse(await _returns.GetPagedAsync(query, null, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _returns.GetByIdAsync(id, null, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmMaterialReturnDto dto, CancellationToken ct) =>
        this.ToResponse(await _returns.ConfirmAsync(id, dto, _user.EmployeeId, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonDto dto, CancellationToken ct) =>
        this.ToResponse(await _returns.RejectAsync(id, dto, _user.EmployeeId, ct));
}
