using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>DEV-ALL-50: Kế toán quản lý nhóm công đoạn của mã hàng.</summary>
public sealed class OperationGroupsController : AccountantBaseController
{
    private readonly IOperationGroupService _groups;

    public OperationGroupsController(IOperationGroupService groups) => _groups = groups;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int productId, CancellationToken ct) =>
        this.ToResponse(await _groups.ListAsync(productId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOperationGroupDto dto, CancellationToken ct) =>
        this.ToResponse(await _groups.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameOperationGroupDto dto, CancellationToken ct) =>
        this.ToResponse(await _groups.RenameAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToResponse(await _groups.DeleteAsync(id, ct));

    [HttpPut("operations")]
    public async Task<IActionResult> SetOperationsGroup([FromBody] SetOperationsGroupDto dto, CancellationToken ct) =>
        this.ToResponse(await _groups.SetOperationsGroupAsync(dto, ct));
}
