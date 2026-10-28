using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>
/// Quản đốc lập / sửa / hủy chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ.
/// Lưu ý: đây là "KPI tổ may" theo bảng phân công — khác với "UC19 (tổ cắt)" đã ghi ở
/// <see cref="WorkOrdersController"/>. Cả hai tính năng đều được một vài nguồn gọi là "UC19" (SRS báo cáo
/// đánh số khác với bảng phân công nội bộ của dự án); đây là lệch số UC có sẵn từ trước, không xử lý ở đây.
/// </summary>
public sealed class KpiTargetsController : ManagerBaseController
{
    private readonly IKpiService _kpi;

    public KpiTargetsController(IKpiService kpi) => _kpi = kpi;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] KpiTargetQuery query, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetByIdAsync(id, ct));

    /// <summary>Chỉ tiêu tối đa lập được (BTP khả dụng) cho form lập KPI.</summary>
    [HttpGet("available-btp")]
    public async Task<IActionResult> GetCapacity([FromQuery] int orderId, [FromQuery] int productId,
        [FromQuery] int sewingTeamId, [FromQuery] int? exceptKpiTargetId, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetCapacityAsync(orderId, productId, sewingTeamId, exceptKpiTargetId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveKpiTargetDto dto, CancellationToken ct) =>
        this.ToResponse(await _kpi.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveKpiTargetDto dto, CancellationToken ct) =>
        this.ToResponse(await _kpi.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        this.ToResponse(await _kpi.CancelAsync(id, ct));

    /// <summary>MI-18 "KPI theo ngày": kết quả từng ngày của một chỉ tiêu KPI (vw_KpiDailyResult).</summary>
    [HttpGet("{id:int}/daily-results")]
    public async Task<IActionResult> GetDailyResults(int id, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetDailyResultsAsync(id, ct));
}
