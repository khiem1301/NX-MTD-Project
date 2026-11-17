using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC10: mã hàng. Không có xóa (GBR-14), chỉ ngừng dùng.</summary>
public sealed class ProductsController : AdminBaseController
{
    private readonly IProductService _products;
    private readonly IProductSpecService _specs;
    private readonly ISpecImportService _import;
    private readonly IProductImportService _productImport;

    public ProductsController(IProductService products, IProductSpecService specs, ISpecImportService import,
        IProductImportService productImport)
    {
        _products = products; _specs = specs; _import = import; _productImport = productImport;
    }

    /// <summary>DEV-ALL-120: xem trước file Excel mã hàng (multipart: file), không ghi gì.</summary>
    [HttpPost("import-products/preview")]
    public async Task<IActionResult> PreviewProductImport(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _productImport.PreviewAsync(stream, ct));
    }

    /// <summary>DEV-ALL-120: tạo các mã hàng mới trong file (mã đã có bỏ qua), một giao dịch.</summary>
    [HttpPost("import-products")]
    public async Task<IActionResult> ImportProducts(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _productImport.ImportAsync(stream, ct));
    }

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

    /// <summary>Thành phần, biến thể (màu × size) và định mức NVL của mã hàng.</summary>
    [HttpGet("{id:int}/spec")]
    public async Task<IActionResult> GetSpec(int id, CancellationToken ct) =>
        this.ToResponse(await _specs.GetAsync(id, ct));

    [HttpPut("{id:int}/spec")]
    public async Task<IActionResult> SaveSpec(int id, [FromBody] SaveProductSpecDto dto, CancellationToken ct) =>
        this.ToResponse(await _specs.SaveAsync(id, dto, ct));

    /// <summary>DEV-ALL-115: xem trước file Excel định mức (multipart: file). orderId = đơn nhận bìa của đơn, bỏ trống = chỉ định mức.</summary>
    [HttpPost("{id:int}/import/preview")]
    public async Task<IActionResult> PreviewImport(int id, IFormFile file, [FromQuery] int? orderId, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.PreviewAsync(id, orderId, stream, ct));
    }

    /// <summary>DEV-ALL-115: nhập file Excel định mức (và bìa của đơn nếu có orderId) trong một giao dịch.</summary>
    [HttpPost("{id:int}/import")]
    public async Task<IActionResult> Import(int id, IFormFile file, [FromQuery] int? orderId, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.ImportAsync(id, orderId, stream, ct));
    }

    private const long MaxRequestBytes = 6 * 1024 * 1024;

    /// <summary>multipart/form-data: file. Trả khóa ảnh để gửi lại trong sampleImageUrl khi lưu mã hàng.</summary>
    [HttpPost("sample-image"), RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> UploadSampleImage(IFormFile? file, CancellationToken ct)
    {
        byte[] content = Array.Empty<byte>();
        if (file is not null)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }
        return this.ToResponse(await _products.UploadSampleImageAsync(new FileUpload(content, file?.FileName ?? string.Empty), ct));
    }

    [HttpGet("sample-image")]
    public async Task<IActionResult> SampleImage([FromQuery] string key, CancellationToken ct)
    {
        var result = await _products.OpenSampleImageAsync(key, ct);
        if (!result.IsSuccess || result.Data is null) return this.ToResponse(result);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Data.Content, result.Data.ContentType);
    }

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) =>
        this.ToResponse(await _products.DiscontinueAsync(id, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        this.ToResponse(await _products.ReactivateAsync(id, ct));
}
