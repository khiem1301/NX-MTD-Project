using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC36 — "Báo cáo kế toán": công + tăng ca, suất ăn + đối soát bếp, km + xăng theo xe. CHỈ
/// Kế toán (permission matrix: Accountant Full, Admin/Manager/WarehouseKeeper No) — khác
/// <see cref="NX_MTD.API.Controllers.Admin.CostSummaryController"/> (UC12, CHỈ Admin).</summary>
public sealed class AccountingReportsController : AccountantBaseController
{
    private readonly IAccountingReportService _reports;

    public AccountingReportsController(IAccountingReportService reports) => _reports = reports;

    /// <summary>Công + tăng ca theo nhân viên trong tháng (DailyTimesheet).</summary>
    [HttpGet("workdays")]
    public async Task<IActionResult> GetWorkdays([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _reports.GetWorkdayOvertimeReportAsync(periodMonth, ct));

    /// <summary>Suất ăn trong tháng + đối soát với hóa đơn bếp đã nhập ở chi phí tháng (nếu có).</summary>
    [HttpGet("meals")]
    public async Task<IActionResult> GetMeals([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _reports.GetMealReportAsync(periodMonth, ct));

    /// <summary>Km + xăng theo xe trong tháng (Vehicle/VehicleTrip).</summary>
    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles([FromQuery] string periodMonth, CancellationToken ct) =>
        this.ToResponse(await _reports.GetVehicleReportAsync(periodMonth, ct));

    /// <summary>Xuất cả 3 báo cáo ra một file Excel 3 sheet (MSG27).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string periodMonth, CancellationToken ct)
    {
        var result = await _reports.ExportAsync(periodMonth, ct);
        if (!result.IsSuccess) return this.ToResponse(result);

        return File(result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName);
    }
}
