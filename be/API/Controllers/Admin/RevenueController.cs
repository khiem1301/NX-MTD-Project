using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC27 — Admin cũng xem được doanh thu (GBR-02: "doanh thu (Admin + Kế toán)"). Dùng chung
/// IFinanceService/RevenueSummaryDto với Accountant/RevenueController — không có đơn giá CMT ở đây, đơn
/// giá CMT chỉ lộ ra ở màn hòa vốn (<see cref="BreakEvenController"/>).</summary>
public sealed class RevenueController : AdminBaseController
{
    private readonly IFinanceService _finance;

    public RevenueController(IFinanceService finance) => _finance = finance;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] RevenueQuery query, CancellationToken ct) =>
        this.ToResponse(await _finance.GetRevenueAsync(query, ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] RevenueQuery query, CancellationToken ct)
    {
        var result = await _finance.ExportRevenueAsync(query, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }
}
