using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC55 (KI-04) — tổ trưởng / tổ phó may nhập sản lượng cuối ca; lưới nạp sẵn từ phân công.</summary>
public sealed class OutputRecordsController : TeamLeaderBaseController
{
    private readonly IOutputService _output;

    public OutputRecordsController(IOutputService output) => _output = output;

    [HttpGet("grid")]
    public async Task<IActionResult> GetGrid([FromQuery] DateOnly workDate, [FromQuery] string shiftCode,
        [FromQuery] int orderLineId, CancellationToken ct) =>
        this.ToResponse(await _output.GetGridAsync(workDate, shiftCode, orderLineId, null, ct));

    /// <summary>Tổ luôn lấy từ tài khoản đăng nhập; SewingTeamId client gửi lên bị bỏ qua.</summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveOutputDto dto, CancellationToken ct) =>
        this.ToResponse(await _output.SaveAsync(dto with { SewingTeamId = null }, ct));
}
