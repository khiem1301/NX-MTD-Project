using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Kết quả hòa vốn theo mã hàng
/// </summary>
public partial class BreakEvenResult
{
    public int BreakEvenResultId { get; set; }

    public int PeriodId { get; set; }

    public int ProductId { get; set; }

    public string AllocationBasis { get; set; } = null!;

    public int FinishedQty { get; set; }

    public decimal CmtUnitPrice { get; set; }

    public decimal Revenue { get; set; }

    public decimal VariableCost { get; set; }

    public decimal AllocatedFixedCost { get; set; }

    public decimal? BreakEvenQty { get; set; }

    public decimal? PercentReached { get; set; }

    public decimal SurplusValue { get; set; }

    public bool IsAtRisk { get; set; }

    public DateTime CalculatedAt { get; set; }

    public virtual AccountingPeriod Period { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
