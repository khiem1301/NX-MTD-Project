using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>DEV-ALL-50: Admin quản lý công đoạn như Kế toán nhưng không tạo đơn giá, kể cả khi nhập Excel, vì Admin là người duyệt đơn giá.</summary>
public sealed class OperationsController : AdminBaseController
{
    private readonly IOperationService _operations;

    public OperationsController(IOperationService operations) => _operations = operations;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int productId, [FromQuery] bool includeDiscontinued, CancellationToken ct) =>
        this.ToResponse(await _operations.ListAsync(productId, includeDiscontinued, ct));

    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport(IFormFile file, [FromQuery] int productId, [FromQuery] string? sheetName, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _operations.PreviewImportAsync(productId, stream, sheetName, null, ct));
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(
        IFormFile file, [FromQuery] int productId, [FromQuery] string? sheetName, [FromQuery] int? finalStt, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _operations.ImportAsync(productId, stream, sheetName, finalStt, null, false, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveOperationDto dto, CancellationToken ct) =>
        this.ToResponse(await _operations.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveOperationDto dto, CancellationToken ct) =>
        this.ToResponse(await _operations.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/discontinue")]
    public async Task<IActionResult> Discontinue(int id, CancellationToken ct) =>
        this.ToResponse(await _operations.DiscontinueAsync(id, ct));

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        this.ToResponse(await _operations.ReactivateAsync(id, ct));
}
