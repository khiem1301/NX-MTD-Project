using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>
/// MI-17 (UC21) — Admin chỉ XEM chỉ tiêu KPI tổ may và kết quả (thực tế vs chỉ tiêu, theo ngày). Lập / sửa / hủy
/// chỉ tiêu vẫn là việc của Quản đốc (<see cref="Manager.KpiTargetsController"/>).
/// </summary>
public sealed class KpiTargetsController : AdminBaseController
{
    private readonly IKpiService _kpi;
    private readonly IKpiCalendarService _calendar;

    public KpiTargetsController(IKpiService kpi, IKpiCalendarService calendar)
    {
        _kpi = kpi; _calendar = calendar;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] KpiTargetQuery query, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetByIdAsync(id, ct));

    [HttpGet("{id:int}/calendar")]
    public async Task<IActionResult> GetCalendar(int id, CancellationToken ct) =>
        this.ToResponse(await _calendar.GetAsync(id, ct));

    [HttpGet("{id:int}/daily-results")]
    public async Task<IActionResult> GetDailyResults(int id, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetDailyResultsAsync(id, ct));
}
