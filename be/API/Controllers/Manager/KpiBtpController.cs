using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>So sánh BTP đã cắt – KPI ngày – thực tế tổ may, trừ dần theo lũy kế. Quản đốc. Chỉ xem.</summary>
public sealed class KpiBtpController : ManagerBaseController
{
    private readonly IKpiBtpService _kpiBtp;

    public KpiBtpController(IKpiBtpService kpiBtp) => _kpiBtp = kpiBtp;

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        this.ToResponse(await _kpiBtp.GetOptionsAsync(null, ct));

    [HttpGet("{kpiTargetId:int}")]
    public async Task<IActionResult> Get(int kpiTargetId, CancellationToken ct) =>
        this.ToResponse(await _kpiBtp.GetAsync(kpiTargetId, null, ct));
}
