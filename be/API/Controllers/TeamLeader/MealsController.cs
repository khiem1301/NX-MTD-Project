using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.TeamLeader;

/// <summary>UC30 — tổ trưởng/tổ phó xem gợi ý và xác nhận số suất ăn của tổ mình (GBR-01, FR 5.1).</summary>
public sealed class MealsController : TeamLeaderBaseController
{
    private readonly IMealService _meals;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public MealsController(IMealService meals, ICurrentUser user, IClock clock)
    {
        _meals = meals;
        _user = user;
        _clock = clock;
    }

    [HttpGet("suggestion")]
    public async Task<IActionResult> GetSuggestion([FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (_user.TeamId is not { } teamId) return NoTeam();
        return this.ToResponse(await _meals.GetTeamSuggestionAsync(teamId, date ?? _clock.Today, ct));
    }

    [HttpPost("confirmation")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmTeamMealDto dto, CancellationToken ct)
    {
        if (_user.TeamId is not { } teamId) return NoTeam();
        return this.ToResponse(await _meals.ConfirmTeamMealAsync(teamId, dto, ct));
    }

    private IActionResult NoTeam() =>
        this.ToResponse(Result.Failure(ErrorCodes.AuthForbidden, "Tài khoản chưa được gán tổ.", ResultKind.Forbidden));
}
