using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwDailyMealTotal
{
    public int DailyMealId { get; set; }

    public DateOnly MealDate { get; set; }

    public string Status { get; set; } = null!;

    public int OfficeCount { get; set; }

    public int GuestCount { get; set; }

    public int TeamTotal { get; set; }

    public int? TotalMeals { get; set; }

    public decimal? MealPrice { get; set; }

    public decimal? TotalAmount { get; set; }
}
