using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Chi phí tháng theo khoản mục
/// </summary>
public partial class MonthlyCost
{
    public int MonthlyCostId { get; set; }

    public int PeriodId { get; set; }

    public string CostCategoryCode { get; set; } = null!;

    public string CostType { get; set; } = null!;

    public int? ProductId { get; set; }

    public decimal Amount { get; set; }

    public int? Quantity { get; set; }

    public string? Note { get; set; }

    public DateTime ImportedAt { get; set; }

    public virtual AccountingPeriod Period { get; set; } = null!;

    public virtual Product? Product { get; set; }
}
