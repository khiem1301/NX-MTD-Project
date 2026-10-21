using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC12 — "Tổng hợp chi phí" theo tháng (vật tư + lương, tổng và theo mã hàng). CHỈ Admin (dữ liệu
/// "mật" theo GBR-02, cùng tầng với hòa vốn — <see cref="BreakEvenController"/>).</summary>
public sealed class CostSummaryController : AdminBaseController
{
    private readonly ICostSummaryService _costSummary;

    public CostSummaryController(ICostSummaryService costSummary) => _costSummary = costSummary;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _costSummary.GetAsync(periodMonth, ct));

    /// <summary>Xuất tổng hợp chi phí ra Excel (MSG27).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string periodMonth, CancellationToken ct)
    {
        var result = await _costSummary.ExportAsync(periodMonth, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }
}
