using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwMaterialStock
{
    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = null!;

    public string MaterialName { get; set; } = null!;

    public string Color { get; set; } = null!;

    public string? MaterialType { get; set; }

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal? OnHandQty { get; set; }

    public decimal? ReservedQty { get; set; }

    public decimal? AvailableQty { get; set; }
}
