namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Sinh dòng MealRegistration từ người đã có giờ vào trong ngày.
/// Chỉ thêm dòng mới (WillEat = true); không ghi đè người đã bỏ tick.
/// </summary>
public interface IMealRegistrationSeeder
{
    Task SeedForDateAsync(DateOnly date, CancellationToken ct);

    Task SeedForDatesAsync(IEnumerable<DateOnly> dates, CancellationToken ct);
}
