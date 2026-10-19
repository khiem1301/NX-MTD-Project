using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Khoản trên phiếu lương
/// </summary>
public partial class PayslipItem
{
    public int PayslipItemId { get; set; }

    public int PayslipId { get; set; }

    public string ItemGroup { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal Amount { get; set; }

    public short SortOrder { get; set; }

    public virtual Payslip Payslip { get; set; } = null!;
}
