using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Tồn hiện tại theo mặt hàng và kho
/// </summary>
public partial class StockBalance
{
    public int StockBalanceId { get; set; }

    public string WarehouseType { get; set; } = null!;

    public int? MaterialId { get; set; }

    public string? MaterialColor { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public decimal OnHandQty { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Material? Material { get; set; }

    public virtual OrderLine? OrderLine { get; set; }

    public virtual ProductComponentMaterial? ProductComponentMaterial { get; set; }

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();
}
