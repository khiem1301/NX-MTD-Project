using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwMonthlyRevenue
{
    public string? PeriodMonth { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int? FinishedQty { get; set; }

    public decimal? Revenue { get; set; }
}
