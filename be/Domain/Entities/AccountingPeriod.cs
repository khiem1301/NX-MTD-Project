using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Kỳ kế toán (tháng), khóa / mở khóa
/// </summary>
public partial class AccountingPeriod
{
    public int PeriodId { get; set; }

    public string PeriodMonth { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? LockedAt { get; set; }

    public virtual ICollection<BreakEvenResult> BreakEvenResults { get; set; } = new List<BreakEvenResult>();

    public virtual ICollection<MonthlyCost> MonthlyCosts { get; set; } = new List<MonthlyCost>();

    public virtual Payroll? Payroll { get; set; }
}
