using System;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Suất ăn theo nhân viên trong ngày (sinh từ người đã quẹt; bỏ tick trước giờ chốt).
/// </summary>
public partial class MealRegistration
{
    public int MealRegistrationId { get; set; }

    public int DailyMealId { get; set; }

    public int EmployeeId { get; set; }

    public int? TeamId { get; set; }

    public bool WillEat { get; set; }

    public int? UpdatedByEmployeeId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DailyMeal DailyMeal { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual Employee? UpdatedByEmployee { get; set; }
}
