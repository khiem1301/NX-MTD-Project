using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Bảng lương tháng (import)
/// </summary>
public partial class Payroll
{
    public int PayrollId { get; set; }

    public int PeriodId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public virtual ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();

    public virtual AccountingPeriod Period { get; set; } = null!;
}
