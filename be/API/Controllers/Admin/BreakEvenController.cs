using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC11 — Hòa vốn + giá trị thặng dư theo mã hàng. CHỈ Admin (dữ liệu "mật" theo GBR-02: đơn giá
/// CMT, hòa vốn, giá trị thặng dư). Mỗi lần "Tính toán" ghi thêm một bộ dòng mới (lịch sử không bị ghi
/// đè); chặn khi bảng lương tháng chưa duyệt hoặc chi phí tháng chưa nhập (MSG25, GBR-17).</summary>
public sealed class BreakEvenController : AdminBaseController
{
    private readonly IFinanceService _finance;

    public BreakEvenController(IFinanceService finance) => _finance = finance;

    /// <summary>Kết quả đã tính của một tháng; mặc định chỉ lấy lần tính gần nhất (history=true để xem
    /// toàn bộ lịch sử các lần tính).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string periodMonth, [FromQuery] int? productId,
        [FromQuery] bool history, CancellationToken ct) =>
        this.ToResponse(await _finance.GetBreakEvenAsync(periodMonth, productId, history, ct));

    /// <summary>Chạy một lần tính hòa vốn mới cho một tháng theo cách phân bổ Admin chọn (OUTPUT hoặc
    /// LABOUR_HOURS) — trả kèm danh sách cảnh báo mã hàng nguy cơ lỗ (MSG26).</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] BreakEvenCalculateDto dto, CancellationToken ct) =>
        this.ToResponse(await _finance.CalculateAsync(dto, ct));

    /// <summary>Xuất kết quả hòa vốn ra Excel (MSG27).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string periodMonth, [FromQuery] int? productId,
        [FromQuery] bool history, CancellationToken ct)
    {
        var result = await _finance.ExportBreakEvenAsync(periodMonth, productId, history, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }
}
