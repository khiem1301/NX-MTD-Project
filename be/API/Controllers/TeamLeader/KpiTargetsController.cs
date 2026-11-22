using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>Chỉ tiêu KPI được giao cho tổ của mình (chỉ xem). Tổ lấy từ token, không theo tham số gửi lên.</summary>
public sealed class KpiTargetsController : TeamLeaderBaseController
{
    private readonly IKpiService _kpi;
    private readonly IKpiCalendarService _calendar;
    private readonly IEndOfOrderService _endOfOrder;
    private readonly ICurrentUser _user;

    public KpiTargetsController(
        IKpiService kpi, IKpiCalendarService calendar, IEndOfOrderService endOfOrder, ICurrentUser user)
    {
        _kpi = kpi; _calendar = calendar; _endOfOrder = endOfOrder; _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] KpiTargetQuery query, CancellationToken ct) =>
        this.ToResponse(await _kpi.GetPagedAsync(query with { SewingTeamId = _user.TeamId ?? 0 }, ct));

    /// <summary>Lịch KPI theo ngày của chỉ tiêu thuộc tổ mình (chỉ xem).</summary>
    [HttpGet("{id:int}/calendar")]
    public async Task<IActionResult> GetCalendar(int id, CancellationToken ct) =>
        this.ToResponse(await _calendar.GetForTeamAsync(id, _user.TeamId ?? 0, ct));

    /// <summary>Kết quả kết thúc đơn của tổ mình (mở từ thông báo lệch kiểm cuối đơn): chỉ xem, tổ khác trả 404.</summary>
    [HttpGet("{id:int}/finish-result")]
    public async Task<IActionResult> FinishResult(int id, CancellationToken ct) =>
        this.ToResponse(await _endOfOrder.PreviewAsync(id, _user.TeamId ?? 0, ct));
}
