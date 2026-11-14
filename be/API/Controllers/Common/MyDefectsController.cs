using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC56 — công nhân chỉ xem phiếu lỗi của chính mình (không phản hồi / khiếu nại, DEV-ALL-106).</summary>
[Authorize]
public sealed class MyDefectsController : CommonBaseController
{
    private readonly IDefectService _defects;
    private readonly ICurrentUser _user;

    public MyDefectsController(IDefectService defects, ICurrentUser user)
    {
        _defects = defects;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> ListMine([FromQuery] DefectQuery query, CancellationToken ct) =>
        this.ToResponse(await _defects.ListMineAsync(_user.EmployeeId, query, ct));
}
