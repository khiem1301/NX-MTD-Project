using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC59 — sản lượng + lương tạm tính của tôi. Chỉ dữ liệu của chính người đăng nhập (công nhân may khoán sản phẩm).</summary>
[Authorize]
public sealed class MyEstimatedPayController : CommonBaseController
{
    private readonly IWorkerPayService _pay;
    private readonly ICurrentUser _user;

    public MyEstimatedPayController(IWorkerPayService pay, ICurrentUser user)
    {
        _pay = pay;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] MyEstimatedPayQuery query, CancellationToken ct) =>
        this.ToResponse(await _pay.GetMyEstimatedPayAsync(_user.EmployeeId, query, ct));

    [HttpGet("days/{date}")]
    public async Task<IActionResult> GetDay(DateOnly date, CancellationToken ct) =>
        this.ToResponse(await _pay.GetMyDayDetailAsync(_user.EmployeeId, date, ct));
}
