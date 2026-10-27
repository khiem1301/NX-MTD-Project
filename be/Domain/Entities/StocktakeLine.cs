using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Dòng kiểm kê
/// </summary>
public partial class StocktakeLine
{
    public int StocktakeLineId { get; set; }

    public int StocktakeId { get; set; }

    public int StockBalanceId { get; set; }

    public decimal BookQty { get; set; }

    public decimal? CountedQty { get; set; }

    public decimal? DifferenceQty { get; set; }

    public string? Note { get; set; }

    public virtual StockBalance StockBalance { get; set; } = null!;

    public virtual Stocktake Stocktake { get; set; } = null!;
}
