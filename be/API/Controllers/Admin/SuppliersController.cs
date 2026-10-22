using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC14 (MI-07) — Admin quản lý nhà cung cấp: thêm, sửa, ngừng hợp tác / hợp tác lại (không xóa).</summary>
public sealed class SuppliersController : AdminBaseController
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SupplierQuery query, CancellationToken ct) =>
        this.ToResponse(await _suppliers.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        this.ToResponse(await _suppliers.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSupplierDto dto, CancellationToken ct) =>
        this.ToResponse(await _suppliers.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveSupplierDto dto, CancellationToken ct) =>
        this.ToResponse(await _suppliers.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) =>
        this.ToResponse(await _suppliers.DiscontinueAsync(id, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        this.ToResponse(await _suppliers.ReactivateAsync(id, ct));
}
