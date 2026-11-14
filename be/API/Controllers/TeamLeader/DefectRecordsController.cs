using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC56 (KI-08) — tổ trưởng / tổ phó may ghi hàng hỏng Rework / Scrap, sửa và hủy phiếu trong 24 giờ (hủy bắt buộc lý do).</summary>
public sealed class DefectRecordsController : TeamLeaderBaseController
{
    private readonly IDefectService _defects;

    public DefectRecordsController(IDefectService defects) => _defects = defects;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DefectQuery query, CancellationToken ct) =>
        this.ToResponse(await _defects.ListForTeamAsync(query, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDefectDto dto, CancellationToken ct) =>
        this.ToResponse(await _defects.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDefectDto dto, CancellationToken ct) =>
        this.ToResponse(await _defects.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelDefectDto dto, CancellationToken ct) =>
        this.ToResponse(await _defects.CancelAsync(id, dto, ct));
}
