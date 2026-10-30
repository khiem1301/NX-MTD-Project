using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

public partial class VwRecutFabricNeed
{
    public int WorkOrderLineId { get; set; }

    public int WorkOrderId { get; set; }

    public int OrderLineId { get; set; }

    public int? ProductComponentMaterialId { get; set; }

    public int ProductComponentId { get; set; }

    public int MaterialId { get; set; }

    public string Unit { get; set; } = null!;

    public int Pieces { get; set; }

    public decimal? PieceLengthCm { get; set; }

    public decimal? PieceWidthCm { get; set; }

    public decimal? FabricWidthCm { get; set; }

    public int? PiecesPerRow { get; set; }

    public decimal? NeedMeters { get; set; }

    public decimal? AvailableQty { get; set; }

    public bool? EnoughStock { get; set; }
}
