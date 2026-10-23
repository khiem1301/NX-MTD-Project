using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>So sánh BTP đã cắt – KPI ngày – thực tế tổ may, trừ dần theo lũy kế. Tổ trưởng / tổ phó may, chỉ KPI của tổ mình. Chỉ xem.</summary>
public sealed class KpiBtpController : TeamLeaderBaseController
{
    private readonly IKpiBtpService _kpiBtp;
    private readonly ICurrentUser _user;

    public KpiBtpController(IKpiBtpService kpiBtp, ICurrentUser user)
    {
        _kpiBtp = kpiBtp; _user = user;
    }

    private int? TeamId => _user.TeamId ?? 0;   // tổ trưởng chưa gắn tổ thì không thấy KPI nào

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        this.ToResponse(await _kpiBtp.GetOptionsAsync(TeamId, ct));

    [HttpGet("{kpiTargetId:int}")]
    public async Task<IActionResult> Get(int kpiTargetId, CancellationToken ct) =>
        this.ToResponse(await _kpiBtp.GetAsync(kpiTargetId, TeamId, ct));
}
