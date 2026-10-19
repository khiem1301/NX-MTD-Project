using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu lương
/// </summary>
public partial class Payslip
{
    public int PayslipId { get; set; }

    public int PayrollId { get; set; }

    public int EmployeeId { get; set; }

    public decimal? WorkDays { get; set; }

    public decimal TotalAdditions { get; set; }

    public decimal TotalDeductions { get; set; }

    public decimal Insurance { get; set; }

    public decimal NetPay { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Payroll Payroll { get; set; } = null!;

    public virtual ICollection<PayslipItem> PayslipItems { get; set; } = new List<PayslipItem>();
}
