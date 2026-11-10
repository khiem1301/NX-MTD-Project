using Microsoft.Extensions.Logging;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Thêm MealRegistration cho người đã quẹt nhưng chưa có dòng.
/// Không đụng WillEat của dòng đã có (giữ bỏ tick của công nhân/tổ trưởng).
/// </summary>
public sealed class MealRegistrationSeeder : IMealRegistrationSeeder
{
    private readonly IMealRepository _meals;
    private readonly ILogger<MealRegistrationSeeder> _logger;

    public MealRegistrationSeeder(IMealRepository meals, ILogger<MealRegistrationSeeder> logger)
    {
        _meals = meals;
        _logger = logger;
    }

    public async Task SeedForDatesAsync(IEnumerable<DateOnly> dates, CancellationToken ct)
    {
        foreach (var date in dates.Distinct())
            await SeedForDateAsync(date, ct);
    }

    public async Task SeedForDateAsync(DateOnly date, CancellationToken ct)
    {
        var meal = await _meals.FindByDateAsync(date, ct);
        if (meal is not null && meal.Status != MealStatus.Open)
            return; // ngày đã chốt: trigger cũng chặn, bỏ qua sớm

        meal ??= await _meals.GetOrCreateAsync(date, ct);
        if (meal.Status != MealStatus.Open) return;

        var missing = await _meals.GetUnregisteredAttendeesAsync(meal.DailyMealId, date, ct);
        if (missing.Count == 0) return;

        foreach (var (employeeId, teamId) in missing)
        {
            _meals.AddRegistration(new MealRegistration
            {
                DailyMealId = meal.DailyMealId,
                EmployeeId = employeeId,
                TeamId = teamId,
                WillEat = true,
            });
        }

        await _meals.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed suất ăn ngày {MealDate}: thêm {Count} dòng từ chấm công", date, missing.Count);
    }
}
