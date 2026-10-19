using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Xác nhận suất ăn của tổ
/// </summary>
public partial class TeamMealConfirmation
{
    public int TeamMealConfirmationId { get; set; }

    public int DailyMealId { get; set; }

    public int TeamId { get; set; }

    public int MealCount { get; set; }

    public int AttendanceCount { get; set; }

    public string Source { get; set; } = null!;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? RemindedAt { get; set; }

    public virtual DailyMeal DailyMeal { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}
