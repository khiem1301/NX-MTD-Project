using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Bìa cắt của đơn (DEV-ALL-85): xem bìa theo đơn, lưu bìa của một mã hàng trong đơn.</summary>
public sealed class OrderMarkersController : ManagerBaseController
{
    private readonly IOrderMarkerService _markers;
    private readonly ISpecImportService _import;

    public OrderMarkersController(IOrderMarkerService markers, ISpecImportService import)
    {
        _markers = markers; _import = import;
    }

    /// <summary>DEV-ALL-114: xem trước file Excel định mức + bìa của đơn cho một mã hàng của đơn (multipart: file).</summary>
    [HttpPost("{orderId:int}/products/{productId:int}/import/preview")]
    public async Task<IActionResult> PreviewImport(int orderId, int productId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.PreviewAsync(productId, orderId, stream, ct));
    }

    /// <summary>DEV-ALL-114 / DEV-ALL-116: Quản đốc và Admin nhập định mức mã hàng + bìa của đơn trong một giao dịch.</summary>
    [HttpPost("{orderId:int}/products/{productId:int}/import")]
    public async Task<IActionResult> Import(int orderId, int productId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.ImportAsync(productId, orderId, stream, ct));
    }

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> Get(int orderId, CancellationToken ct) =>
        this.ToResponse(await _markers.GetAsync(orderId, ct));

    [HttpPut("{orderId:int}/products/{productId:int}")]
    public async Task<IActionResult> Save(int orderId, int productId, [FromBody] SaveOrderMarkersDto dto, CancellationToken ct) =>
        this.ToResponse(await _markers.SaveAsync(orderId, productId, dto, ct));
}
