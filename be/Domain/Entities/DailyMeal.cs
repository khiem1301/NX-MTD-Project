using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Suất ăn ngày (gồm suất khách)
/// </summary>
public partial class DailyMeal
{
    public int DailyMealId { get; set; }

    public DateOnly MealDate { get; set; }

    public string Status { get; set; } = null!;

    public int OfficeCount { get; set; }

    public int GuestCount { get; set; }

    public string? GuestNote { get; set; }

    public int TeamTotal { get; set; }

    public int? TotalMeals { get; set; }

    public decimal? MealPrice { get; set; }

    public decimal? TotalAmount { get; set; }

    public bool AttendanceIncomplete { get; set; }

    public DateTime? AutoClosedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? KitchenNotifiedAt { get; set; }

    public virtual ICollection<TeamMealConfirmation> TeamMealConfirmations { get; set; } = new List<TeamMealConfirmation>();
}
