using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwStockBalance
{
    public string WarehouseType { get; set; } = null!;

    public int? MaterialId { get; set; }

    public int? OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public decimal? OnHandQty { get; set; }

    public decimal? ReservedQty { get; set; }

    public decimal? AvailableQty { get; set; }
}
