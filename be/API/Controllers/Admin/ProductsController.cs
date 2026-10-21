using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC10: mã hàng. Không có xóa (GBR-14), chỉ ngừng dùng.</summary>
public sealed class ProductsController : AdminBaseController
{
    private readonly IProductService _products;
    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductQuery query, CancellationToken ct) =>
        this.ToResponse(await _products.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        this.ToResponse(await _products.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveProductDto dto, CancellationToken ct) =>
        this.ToResponse(await _products.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveProductDto dto, CancellationToken ct) =>
        this.ToResponse(await _products.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) =>
        this.ToResponse(await _products.DiscontinueAsync(id, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        this.ToResponse(await _products.ReactivateAsync(id, ct));
}
