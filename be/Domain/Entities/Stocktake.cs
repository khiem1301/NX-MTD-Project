using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Phiếu kiểm kê
/// </summary>
public partial class Stocktake
{
    public int StocktakeId { get; set; }

    public string StocktakeCode { get; set; } = null!;

    public string WarehouseType { get; set; } = null!;

    public string? Scope { get; set; }

    public DateOnly StocktakeDate { get; set; }

    public string Status { get; set; } = null!;

    public string? ReturnReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();
}
