using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC12/UC33 — Kế toán: tải file mẫu, xem trước rồi nhập Excel chi phí tháng (10 khoản
/// FIXED/VARIABLE); xem chi phí đã nhập của một tháng.</summary>
public sealed class MonthlyCostsController : AccountantBaseController
{
    private readonly IMonthlyCostService _monthlyCosts;

    public MonthlyCostsController(IMonthlyCostService monthlyCosts) => _monthlyCosts = monthlyCosts;

    /// <summary>Chi phí đã nhập của một tháng, kèm đối soát MEALS.</summary>
    [HttpGet]
    public async Task<IActionResult> GetByPeriodMonth([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _monthlyCosts.GetByPeriodMonthAsync(periodMonth, ct));

    /// <summary>Tải file Excel mẫu để điền chi phí tháng (MSG27).</summary>
    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate(CancellationToken ct)
    {
        var result = await _monthlyCosts.ExportTemplateAsync(ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    /// <summary>Xem trước file Excel chi phí tháng (chưa ghi DB) — MSG24, kèm đối soát MEALS nếu có.</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport(IFormFile? file, [FromQuery] string periodMonth, CancellationToken ct)
    {
        if (ExcelUploadGuard.Check(file) is { } invalid) return this.ToResponse(invalid);
        await using var stream = file!.OpenReadStream();
        return this.ToResponse(await _monthlyCosts.PreviewImportAsync(stream, periodMonth, ct));
    }

    /// <summary>Nhập chi phí tháng từ Excel: thay toàn bộ dòng cũ của tháng (MSG24); chặn khi tháng đã
    /// khóa (MSG21).</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile? file, [FromQuery] string periodMonth, CancellationToken ct)
    {
        if (ExcelUploadGuard.Check(file) is { } invalid) return this.ToResponse(invalid);
        await using var stream = file!.OpenReadStream();
        return this.ToResponse(await _monthlyCosts.ImportAsync(stream, periodMonth, ct));
    }

    /// <summary>DEV-ALL-187: kiểm lại bảng chi phí đã sửa trên màn hình (chưa ghi DB).</summary>
    [HttpPost("import/rows/preview")]
    public async Task<IActionResult> PreviewRows([FromBody] MonthlyCostRowsDto dto, CancellationToken ct) =>
        this.ToResponse(await _monthlyCosts.PreviewRowsAsync(dto, ct));

    /// <summary>DEV-ALL-187: nhập chi phí tháng từ bảng đã sửa trên màn hình.</summary>
    [HttpPost("import/rows")]
    public async Task<IActionResult> ImportRows([FromBody] MonthlyCostRowsDto dto, CancellationToken ct) =>
        this.ToResponse(await _monthlyCosts.ImportRowsAsync(dto, ct));
}
