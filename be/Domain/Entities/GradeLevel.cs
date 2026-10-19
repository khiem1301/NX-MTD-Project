using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bậc thợ A–D và tiền bậc theo ngày hiệu lực
/// </summary>
public partial class GradeLevel
{
    public int GradeLevelId { get; set; }

    public string GradeCode { get; set; } = null!;

    public string GradeName { get; set; } = null!;

    public decimal MonthlyAmount { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
