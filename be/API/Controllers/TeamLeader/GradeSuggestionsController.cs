using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>DEV-ALL-148: AI gợi ý bậc thợ cho cả tổ; tổ trưởng / tổ phó xem lại, sửa, tạo lại và xác nhận thành đề xuất bậc.</summary>
public sealed class GradeSuggestionsController : TeamLeaderBaseController
{
    private readonly IGradeSuggestionService _suggestions;
    private readonly ICurrentUser _user;

    public GradeSuggestionsController(IGradeSuggestionService suggestions, ICurrentUser user)
    {
        _suggestions = suggestions; _user = user;
    }

    private int TeamId => _user.TeamId ?? 0;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        this.ToResponse(await _suggestions.GetAsync(TeamId, ct));

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateGradeSuggestionDto dto, CancellationToken ct) =>
        this.ToResponse(await _suggestions.GenerateAsync(TeamId, dto, ct));

    [HttpPut("items/{workerId:int}")]
    public async Task<IActionResult> UpdateItem(int workerId, [FromBody] UpdateGradeSuggestionItemDto dto, CancellationToken ct) =>
        this.ToResponse(await _suggestions.UpdateItemAsync(TeamId, workerId, dto, ct));

    [HttpDelete("items/{workerId:int}")]
    public async Task<IActionResult> RemoveItem(int workerId, CancellationToken ct) =>
        this.ToResponse(await _suggestions.RemoveItemAsync(TeamId, workerId, ct));

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(CancellationToken ct) =>
        this.ToResponse(await _suggestions.ConfirmAsync(TeamId, ct));
}
