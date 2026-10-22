using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC31 — Kế toán: xuất dữ liệu tính lương, nhập bảng lương từ Excel (Nháp), gửi duyệt.</summary>
public sealed class PayrollsController : AccountantBaseController
{
    private readonly IPayrollService _payroll;

    public PayrollsController(IPayrollService payroll) => _payroll = payroll;

    /// <summary>Bảng lương hiện tại của một tháng (nếu đã có), để Kế toán theo dõi trạng thái.</summary>
    [HttpGet]
    public async Task<IActionResult> GetByPeriodMonth([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _payroll.GetByPeriodMonthAsync(periodMonth, ct));

    /// <summary>Xuất dữ liệu tính lương tháng ra Excel (công, tăng ca, sản lượng, hàng hỏng, cảnh báo thiếu
    /// giờ/chưa nhập sản lượng) để Kế toán tính lương ngoài hệ thống (MSG27).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string periodMonth, CancellationToken ct)
    {
        var result = await _payroll.ExportAsync(periodMonth, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    /// <summary>Xem trước file Excel bảng lương đã tính (chưa ghi DB) — MSG24.</summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _payroll.PreviewImportAsync(stream, ct));
    }

    /// <summary>Nhập bảng lương từ Excel vào trạng thái Nháp cho một tháng (MSG24).</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, [FromQuery] string periodMonth, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _payroll.ImportAsync(stream, periodMonth, ct));
    }

    /// <summary>Tải lại nguyên file Excel bảng thanh toán lương đã nhập (DEV-MI-09-5).</summary>
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> DownloadImportedFile(int id, CancellationToken ct)
    {
        var result = await _payroll.GetImportedFileAsync(id, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }

    /// <summary>Gửi duyệt bảng lương (Nháp → Chờ duyệt).</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct) =>
        this.ToResponse(await _payroll.SubmitAsync(id, ct));
}
