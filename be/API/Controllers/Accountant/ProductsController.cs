using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>
/// UC10 (định mức): Kế toán cùng Admin nhập thành phần và định mức NVL của mã hàng. Chỉ xem danh sách mã hàng và sửa định mức
/// (không thêm / sửa / ngừng sản xuất mã hàng, không đơn giá CMT).
/// </summary>
public sealed class ProductsController : AccountantBaseController
{
    private readonly IProductService _products;
    private readonly IProductSpecService _specs;
    public ProductsController(IProductService products, IProductSpecService specs) { _products = products; _specs = specs; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductQuery query, CancellationToken ct) =>
        this.ToResponse(await _products.ListAsync(query, ct));

    /// <summary>Thành phần, biến thể (màu × size, lấy từ đơn hàng) và định mức NVL của mã hàng.</summary>
    [HttpGet("{id:int}/spec")]
    public async Task<IActionResult> GetSpec(int id, CancellationToken ct) =>
        this.ToResponse(await _specs.GetAsync(id, ct));

    [HttpPut("{id:int}/spec")]
    public async Task<IActionResult> SaveSpec(int id, [FromBody] SaveProductSpecDto dto, CancellationToken ct) =>
        this.ToResponse(await _specs.SaveAsync(id, dto, ct));

    [HttpGet("sample-image")]
    public async Task<IActionResult> SampleImage([FromQuery] string key, CancellationToken ct)
    {
        var result = await _products.OpenSampleImageAsync(key, ct);
        if (!result.IsSuccess || result.Data is null) return this.ToResponse(result);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Data.Content, result.Data.ContentType);
    }
}
