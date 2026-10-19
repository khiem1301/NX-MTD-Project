using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwMaterialStock
{
    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = null!;

    public string MaterialName { get; set; } = null!;

    public string MaterialType { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public string? MaterialColor { get; set; }

    public decimal? OnHandQty { get; set; }

    public decimal? ReservedQty { get; set; }

    public decimal? AvailableQty { get; set; }
}
