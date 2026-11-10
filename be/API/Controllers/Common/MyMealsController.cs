using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Suất ăn của tôi — xem và bỏ tick nếu không ăn (chỉ khi đã quẹt vân tay).</summary>
[Authorize]
public sealed class MyMealsController : CommonBaseController
{
    private readonly IMealService _meals;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public MyMealsController(IMealService meals, ICurrentUser user, IClock clock)
    {
        _meals = meals;
        _user = user;
        _clock = clock;
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday([FromQuery] DateOnly? date, CancellationToken ct) =>
        this.ToResponse(await _meals.GetMyMealAsync(_user.EmployeeId, date ?? _clock.Today, ct));

    [HttpPatch("today")]
    public async Task<IActionResult> SetToday([FromBody] SetMyMealDto dto, CancellationToken ct) =>
        this.ToResponse(await _meals.SetMyMealAsync(_user.EmployeeId, dto, ct));
}
