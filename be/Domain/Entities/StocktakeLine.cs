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

    public int? MaterialId { get; set; }

    public string? MaterialColor { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentId { get; set; }

    public decimal BookQty { get; set; }

    public decimal? CountedQty { get; set; }

    public decimal? DifferenceQty { get; set; }

    public string? Note { get; set; }

    public virtual Material? Material { get; set; }

    public virtual OrderLine? OrderLine { get; set; }

    public virtual ProductComponent? ProductComponent { get; set; }

    public virtual Stocktake Stocktake { get; set; } = null!;
}
