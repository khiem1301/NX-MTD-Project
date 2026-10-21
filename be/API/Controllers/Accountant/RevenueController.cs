using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC27 — Kế toán: xem doanh thu theo tháng/đơn/mã hàng (suy ra từ vw_MonthlyRevenue), xuất
/// Excel. KHÔNG có đơn giá CMT — đó là dữ liệu "mật" chỉ Admin xem được (GBR-02).</summary>
public sealed class RevenueController : AccountantBaseController
{
    private readonly IFinanceService _finance;

    public RevenueController(IFinanceService finance) => _finance = finance;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] RevenueQuery query, CancellationToken ct) =>
        this.ToResponse(await _finance.GetRevenueAsync(query, ct));

    /// <summary>Xuất doanh thu ra Excel theo đúng bộ lọc đang xem (MSG27).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] RevenueQuery query, CancellationToken ct)
    {
        var result = await _finance.ExportRevenueAsync(query, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }
}
