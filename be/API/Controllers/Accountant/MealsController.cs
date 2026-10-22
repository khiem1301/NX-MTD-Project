using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>UC30 — Kế toán nhập suất khách, xem tổng hợp suất ăn ngày, báo bếp và nhập đơn giá (FR 5.2–5.4).</summary>
public sealed class MealsController : AccountantBaseController
{
    private readonly IMealService _meals;
    private readonly IClock _clock;

    public MealsController(IMealService meals, IClock clock)
    {
        _meals = meals;
        _clock = clock;
    }

    /// <summary>Bảng tổng hợp suất ăn một ngày: theo tổ, văn phòng, suất khách, tổng, đơn giá, thành tiền.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateOnly? date, CancellationToken ct) =>
        this.ToResponse(await _meals.GetSummaryAsync(date ?? _clock.Today, ct));

    /// <summary>Kế toán nhập suất khách (DailyMeal.GuestCount, GuestNote).</summary>
    [HttpPost("guest-count")]
    public async Task<IActionResult> SetGuestMeal([FromBody] SetGuestMealDto dto, CancellationToken ct) =>
        this.ToResponse(await _meals.SetGuestMealAsync(dto, ct));

    /// <summary>Đánh dấu đã báo bếp (ngoài hệ thống); chỉ cho phép sau khi ngày đã chốt.</summary>
    [HttpPost("kitchen-notification")]
    public async Task<IActionResult> MarkKitchenNotified([FromQuery] DateOnly date, CancellationToken ct) =>
        this.ToResponse(await _meals.MarkKitchenNotifiedAsync(date, ct));

    /// <summary>Nhập đơn giá suất ăn cho một ngày đã chốt; TotalAmount do DB tự tính lại.</summary>
    [HttpPost("price")]
    public async Task<IActionResult> SetMealPrice([FromBody] SetMealPriceDto dto, CancellationToken ct) =>
        this.ToResponse(await _meals.SetMealPriceAsync(dto, ct));
}
