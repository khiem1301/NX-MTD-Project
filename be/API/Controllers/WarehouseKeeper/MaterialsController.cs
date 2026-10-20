using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC38: danh mục NVL (vải / phụ liệu).</summary>
public sealed class MaterialsController : WarehouseKeeperBaseController
{
    private readonly IMaterialService _materials;

    public MaterialsController(IMaterialService materials) => _materials = materials;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialQuery query, CancellationToken ct) =>
        this.ToResponse(await _materials.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _materials.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _materials.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _materials.UpdateAsync(id, dto, ct));

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeMaterialStatusDto dto, CancellationToken ct) =>
        this.ToResponse(await _materials.ChangeStatusAsync(id, dto, ct));
}
