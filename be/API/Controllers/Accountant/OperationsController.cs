using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC25 (KI-01) — Kế toán lập công đoạn của mã hàng; đúng một công đoạn may cuối đang dùng cho mỗi mã hàng.</summary>
public sealed class OperationsController : AccountantBaseController
{
    private readonly IOperationService _operations;

    public OperationsController(IOperationService operations) => _operations = operations;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int productId, [FromQuery] bool includeDiscontinued, CancellationToken ct) =>
        this.ToResponse(await _operations.ListAsync(productId, includeDiscontinued, ct));

    /// <summary>Xem trước file Excel quy trình công nghệ (chưa ghi DB): dòng nào thêm mới, cập nhật, không đổi, lỗi.</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport(
        IFormFile file, [FromQuery] int productId, [FromQuery] string? sheetName, [FromQuery] DateOnly? effectiveDate,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _operations.PreviewImportAsync(productId, stream, sheetName, effectiveDate, ct));
    }

    /// <summary>Nhập công đoạn từ file Excel quy trình công nghệ. <c>finalStt</c> (không bắt buộc) đánh dấu công đoạn cuối; có <c>effectiveDate</c> thì đọc luôn cột TIỀN thành đơn giá Nháp (hoặc Chờ duyệt nếu <c>submitRates</c>).</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(
        IFormFile file, [FromQuery] int productId, [FromQuery] string? sheetName, [FromQuery] int? finalStt,
        [FromQuery] DateOnly? effectiveDate, [FromQuery] bool submitRates, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(
            await _operations.ImportAsync(productId, stream, sheetName, finalStt, effectiveDate, submitRates, ct));
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
